// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using GUIFramework;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public enum KitDialogKind { Confirm, Message, Input, Choice }

    public sealed class KitDialogChoice
    {
        public string Caption { get; }
        internal readonly Action Callback;
        public KitDialogChoice(string caption, Action callback)
        { Caption = caption ?? throw new ArgumentNullException(nameof(caption)); Callback = callback; }
    }

    /// <summary>One owned request. Answers are accepted only once, while its source and scene remain live.</summary>
    public sealed class KitDialogRequest : IDisposable
    {
        public ProductRegistration Source { get; }
        public KitDialogKind Kind { get; }
        public string Title { get; }
        public string Text { get; }
        public bool LocalizeText { get; }
        public string Value { get; }
        public string Placeholder { get; }
        /// <summary>Maximum accepted length (zero is unlimited); the native input's shared typing configuration is preserved.</summary>
        public int CharacterLimit { get; }
        public IReadOnlyList<KitDialogChoice> Choices { get; }
        public int CancelIndex { get; }
        public bool IsOpen { get; private set; } = true;
        public event Action Closed;
        readonly Action accept, cancel;
        readonly Action<string> submit;
        readonly Func<string, bool> validate;
        internal readonly int Generation;
        internal IKitDialogProvider Provider;
        internal PopupBase NativePopup;
        internal UnifiedPopup NativeHost;
        internal KitWindow ChoiceWindow;
        internal string InputDraft;
        internal UnityAction<string> InputChanged;

        internal KitDialogRequest(ProductRegistration source, KitDialogKind kind, string title, string text, Action accept,
            Action cancel, string value, string placeholder, int limit, Func<string, bool> validate, Action<string> submit,
            KitDialogChoice[] choices, int cancelIndex, int generation, bool localizeText)
        {
            Source = source; Kind = kind; Title = title ?? ""; Text = text ?? ""; this.accept = accept; this.cancel = cancel;
            LocalizeText = localizeText;
            Value = value ?? ""; InputDraft = Value; Placeholder = placeholder ?? ""; CharacterLimit = limit; this.validate = validate; this.submit = submit;
            Choices = Array.AsReadOnly(choices ?? Array.Empty<KitDialogChoice>()); CancelIndex = cancelIndex; Generation = generation;
        }

        bool Live => IsOpen && Source.IsActive && Generation == KitDialogs.Generation && KitDialogs.ProviderLive(this);

        public bool IsValid(string value)
        {
            if (!Live || value == null || CharacterLimit > 0 && value.Length > CharacterLimit) return false;
            try { return validate == null || validate(value); }
            catch (Exception error) { KitDialogs.Failure("Dialog validation", error); return false; }
        }

        public bool TryAccept() => (Kind == KitDialogKind.Confirm || Kind == KitDialogKind.Message) && Answer(accept);
        public bool TrySubmit(string value) => Kind == KitDialogKind.Input && IsValid(value) && Answer(() => submit?.Invoke(value));
        public bool TryChoose(int index) => Kind == KitDialogKind.Choice && index >= 0 && index < Choices.Count && Answer(Choices[index].Callback);
        public bool TryCancel() => Kind == KitDialogKind.Choice && CancelIndex >= 0 ? TryChoose(CancelIndex) : Answer(cancel);

        bool Answer(Action callback)
        {
            if (!Live) { Dispose(); return false; }
            Dispose();
            if (!Source.IsActive || Generation != KitDialogs.Generation) return false;
            try { callback?.Invoke(); }
            catch (Exception error) { KitDialogs.Failure("Dialog callback", error); }
            return true;
        }

        /// <summary>Close without executing a gameplay answer, including owner/scene teardown.</summary>
        public void Dispose()
        {
            if (!IsOpen) return;
            IsOpen = false;
            KitDialogs.CloseNative(this);
            if (ChoiceWindow != null) { ChoiceWindow.Destroy(); ChoiceWindow = null; }
            var handlers = Closed; Closed = null;
            if (handlers != null)
                foreach (Action handler in handlers.GetInvocationList())
                    try { handler(); } catch (Exception error) { KitDialogs.Failure("Dialog close", error); }
        }
    }

    public interface IKitDialogProvider
    {
        /// <summary>On acceptance, close presentation when request.Closed fires. Declining must leave no UI behind.</summary>
        bool TryShow(KitDialogRequest request);
    }

    public static class KitDialogs
    {
        public const string CapabilityId = "meadworks.ui.dialogs";
        public static readonly System.Version ContractVersion = new System.Version(1, 0);
        static readonly CapabilityLink<IKitDialogProvider> provider = new CapabilityLink<IKitDialogProvider>(CapabilityId, ContractVersion);
        static readonly List<KitDialogRequest> requests = new List<KitDialogRequest>();
        static bool initialized, presenting;
        internal static int Generation { get; private set; }
        static class Native
        {
            internal static readonly AccessTools.FieldRef<UnifiedPopup> Instance = AccessTools.StaticFieldRefAccess<UnifiedPopup>(
                typeof(UnifiedPopup).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic));
            internal static readonly AccessTools.FieldRef<UnifiedPopup, Stack<PopupBase>> Stack = AccessTools.FieldRefAccess<UnifiedPopup, Stack<PopupBase>>("popupStack");
            internal static readonly AccessTools.FieldRef<UnifiedPopup, GuiInputField> Input = AccessTools.FieldRefAccess<UnifiedPopup, GuiInputField>("textEntryField");
            internal static readonly AccessTools.FieldRef<UnifiedPopup, UnityEngine.UI.Button> Confirm = AccessTools.FieldRefAccess<UnifiedPopup, UnityEngine.UI.Button>("buttonConfirm");
        }

        public static void Initialize()
        {
            if (initialized || !MeadworksHeadless.AllowsUi) return;
            initialized = true; SceneManager.activeSceneChanged += SceneChanged;
        }

        static void SceneChanged(Scene before, Scene after)
        {
            Generation++;
            for (int i = requests.Count - 1; i >= 0; i--) requests[i].Dispose();
        }

        public static KitDialogRequest Confirm(ProductRegistration source, string title, string text, Action yes, Action no = null, bool localizeText = true) =>
            Open(source, KitDialogKind.Confirm, title, text, yes, no, localizeText: localizeText);
        public static KitDialogRequest Message(ProductRegistration source, string title, string text, Action okay = null) =>
            Open(source, KitDialogKind.Message, title, text, okay, null);
        public static KitDialogRequest Input(ProductRegistration source, string title, string text, string value,
            Action<string> submit, Action cancel = null, int characterLimit = 0, Func<string, bool> validate = null, string placeholder = "") =>
            Open(source, KitDialogKind.Input, title, text, null, cancel, value, placeholder, characterLimit, validate, submit);
        public static KitDialogRequest Choose(ProductRegistration source, string title, string text, KitDialogChoice[] choices, int cancelIndex = -1)
        {
            if (choices == null || choices.Length == 0) throw new ArgumentException("At least one choice is required.", nameof(choices));
            foreach (var choice in choices) if (choice == null) throw new ArgumentException("Null choice.", nameof(choices));
            if (cancelIndex < -1 || cancelIndex >= choices.Length) throw new ArgumentOutOfRangeException(nameof(cancelIndex));
            return Open(source, KitDialogKind.Choice, title, text, null, null, choices: (KitDialogChoice[])choices.Clone(), cancelIndex: cancelIndex);
        }

        static KitDialogRequest Open(ProductRegistration source, KitDialogKind kind, string title, string text, Action accept, Action cancel,
            string value = "", string placeholder = "", int limit = 0, Func<string, bool> validate = null, Action<string> submit = null,
            KitDialogChoice[] choices = null, int cancelIndex = -1, bool localizeText = true)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            if (!source.IsActive || !MeadworksHeadless.AllowsUi || !CrucibleUiPlugin.Active) return null;
            Initialize();
            var request = new KitDialogRequest(source, kind, title, text, accept, cancel, value, placeholder, limit, validate, submit, choices, cancelIndex, Generation, localizeText);
            requests.Add(request);
            if (!presenting && CruciblePlugin.IsReady && provider.TryGet(CruciblePlugin.Services.Capabilities, out var current))
            {
                bool handled = false;
                presenting = true; request.Provider = current;
                try { handled = current.TryShow(request); }
                catch (Exception error) { Failure("Dialog provider", error); }
                finally { presenting = false; }
                if (handled || !request.IsOpen) return request;
                request.Provider = null;
            }
            bool shown = false;
            try
            {
                shown = kind == KitDialogKind.Choice ? ShowChoices(request) : ShowNative(request);
            }
            catch (Exception error) { Failure("Native dialog", error); request.Dispose(); }
            return shown ? request : null;
        }

        static bool ShowNative(KitDialogRequest request)
        {
            if (!UnifiedPopup.IsAvailable()) { request.Dispose(); return false; }
            PopupBase popup;
            switch (request.Kind)
            {
                case KitDialogKind.Confirm:
                    popup = new YesNoPopup(request.Title, request.Text, () => { request.TryAccept(); }, () => { request.TryCancel(); }, localizeText: request.LocalizeText);
                    break;
                case KitDialogKind.Message:
                    popup = new WarningPopup(request.Title, request.Text, () => { request.TryAccept(); });
                    break;
                default:
                    popup = new TextEntryPopup(request.Title, request.Text, request.Placeholder, () => { request.TryCancel(); }, request.IsValid,
                        value => { request.TrySubmit(value); });
                    break;
            }
            request.NativeHost = Native.Instance(); request.NativePopup = popup;
            UnifiedPopup.Push(popup);
            return true;
        }

        // Native Show resets the shared field even when uncovering an existing popup.
        [HarmonyPatch(typeof(UnifiedPopup), "Show")]
        static class InputShown
        {
            static void Postfix(UnifiedPopup __instance, PopupBase popup)
            {
                for (int i = requests.Count - 1; i >= 0; i--)
                {
                    var request = requests[i];
                    if (request.IsOpen && request.NativeHost == __instance && ReferenceEquals(request.NativePopup, popup))
                    { RestoreInput(request); break; }
                }
            }
        }

        static void RestoreInput(KitDialogRequest request)
        {
            if (request.Kind != KitDialogKind.Input || !request.IsOpen || !Top(request)) return;
            var input = Native.Input(request.NativeHost);
            if (request.InputChanged == null)
                request.InputChanged = value =>
                {
                    if (request.IsOpen && Top(request)) request.InputDraft = value;
                };
            input.onValueChanged.RemoveListener(request.InputChanged);
            input.SetTextWithoutNotify(request.InputDraft);
            request.InputDraft = input.text;
            Native.Confirm(request.NativeHost).interactable = request.IsValid(input.text);
            input.onValueChanged.AddListener(request.InputChanged);
        }

        static bool ShowChoices(KitDialogRequest request)
        {
            // The native Yes/No fallback cannot represent a third choice; use the existing kit window without dropping actions.
            const int pageSize = 6;
            int pages = (request.Choices.Count + pageSize - 1) / pageSize, page = 0;
            float width = 520f, height = 140f + Math.Min(pageSize, request.Choices.Count) * 38f + (pages > 1 ? 38 : 0);
            var window = KitWindow.Create("dialog_choice", new Vector2(width, height));
            if (window == null) { request.Dispose(); return false; }
            request.ChoiceWindow = window;
            var title = KitCanvas.Text(window.Panel, "Title", KitNotices.Localize(request.Title), 22, KitUnity.Colour(KitPalette.TabLitText), TextAlignmentOptions.Center, true);
            Place(title.rectTransform, 16, -12, width - 32, 32);
            var body = KitCanvas.Text(window.Panel, "Body", KitNotices.Localize(request.Text), 16, Color.white, TextAlignmentOptions.TopLeft, true);
            Place(body.rectTransform, 16, -50, width - 32, 52);
            var rows = new UnityEngine.UI.Button[Math.Min(pageSize, request.Choices.Count)];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = KitButton.Create(window.Panel, "Choice_" + i, "", null,
                    new Vector2(16, -108 - i * 38), new Vector2(width - 32, 32));
            }
            UnityEngine.UI.Button previous = null, next = null;
            Action draw = () =>
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    var button = rows[i]; int index = page * pageSize + i;
                    window.Unfocusable(button); button.onClick.RemoveAllListeners();
                    button.gameObject.SetActive(index < request.Choices.Count);
                    if (index >= request.Choices.Count) continue;
                    GameButtons.Caption(button.gameObject, KitNotices.Localize(request.Choices[index].Caption));
                    button.onClick.AddListener(() => { request.TryChoose(index); });
                    window.Focusable(button, 0, i);
                }
                if (previous != null) previous.interactable = page > 0;
                if (next != null) next.interactable = page + 1 < pages;
            };
            if (pages > 1)
            {
                previous = KitButton.Create(window.Panel, "Previous", "Previous",
                    () => { if (page > 0) { page--; draw(); } }, new Vector2(16, -height + 44), new Vector2(230, 32));
                next = KitButton.Create(window.Panel, "Next", "Next",
                    () => { if (page + 1 < pages) { page++; draw(); } }, new Vector2(258, -height + 44), new Vector2(246, 32));
                window.Focusable(previous, 0, rows.Length); window.Focusable(next, 1, rows.Length);
            }
            draw();
            window.Closed += () => { request.TryCancel(); };
            window.Open();
            return true;
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }

        static bool Top(KitDialogRequest request)
        {
            if (request.NativePopup == null || request.NativeHost == null || Native.Instance() != request.NativeHost) return false;
            var stack = Native.Stack(request.NativeHost);
            return stack.Count > 0 && ReferenceEquals(stack.Peek(), request.NativePopup);
        }

        internal static bool ProviderLive(KitDialogRequest request) => request.Provider == null || CruciblePlugin.IsReady
            && provider.TryGet(CruciblePlugin.Services.Capabilities, out var current) && ReferenceEquals(current, request.Provider);

        internal static void CloseNative(KitDialogRequest request)
        {
            if (request.NativePopup == null) return;
            if (request.NativeHost != null && request.InputChanged != null)
                Native.Input(request.NativeHost).onValueChanged.RemoveListener(request.InputChanged);
            request.InputChanged = null;
            if (Top(request)) UnifiedPopup.Pop();
            else if (request.NativeHost != null && Native.Instance() == request.NativeHost)
            {
                var stack = Native.Stack(request.NativeHost);
                if (stack.Contains(request.NativePopup))
                {
                    // Remove only this owned entry; the foreign top and its native display never change.
                    var above = new Stack<PopupBase>();
                    while (stack.Count > 0 && !ReferenceEquals(stack.Peek(), request.NativePopup)) above.Push(stack.Pop());
                    if (stack.Count > 0) stack.Pop();
                    while (above.Count > 0) stack.Push(above.Pop());
                }
            }
            request.NativePopup = null; request.NativeHost = null;
        }

        public static void Tick()
        {
            if (!initialized || !MeadworksHeadless.AllowsUi) return;
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                var request = requests[i];
                if (!request.Source.IsActive || request.Generation != Generation) request.Dispose();
                if (request.IsOpen && !ProviderLive(request)) request.Dispose();
                if (request.NativePopup != null && (request.NativeHost == null || Native.Instance() != request.NativeHost
                    || !Native.Stack(request.NativeHost).Contains(request.NativePopup)))
                { request.NativePopup = null; request.NativeHost = null; request.Dispose(); }
                if (!request.IsOpen)
                {
                    CloseNative(request);
                    if (request.NativePopup == null) requests.RemoveAt(i);
                }
            }
        }

        public static void Shutdown()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            Generation++;
            for (int i = requests.Count - 1; i >= 0; i--) requests[i].Dispose();
            requests.Clear(); initialized = false;
        }

        internal static void Failure(string operation, Exception error) => CrucibleUiPlugin.Log?.Warning(operation + ": " + error.GetType().Name);
    }
}
