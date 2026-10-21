// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Reflection;
using GUIFramework;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>Presentation on the game's two dialog hosts; native controls and callbacks remain in place.</summary>
    public static class KitNativeDialogs
    {
        static class Native
        {
            internal static readonly AccessTools.FieldRef<UnifiedPopup> Instance = AccessTools.StaticFieldRefAccess<UnifiedPopup>(
                typeof(UnifiedPopup).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic));
            internal static readonly AccessTools.FieldRef<UnifiedPopup, GameObject> Root = AccessTools.FieldRefAccess<UnifiedPopup, GameObject>("popupUIParent");
            internal static readonly AccessTools.FieldRef<UnifiedPopup, GameObject> Cover = AccessTools.FieldRefAccess<UnifiedPopup, GameObject>("fullScreenBackgroundCover");
            internal static readonly AccessTools.FieldRef<UnifiedPopup, Button> Left = AccessTools.FieldRefAccess<UnifiedPopup, Button>("buttonLeft");
            internal static readonly AccessTools.FieldRef<UnifiedPopup, Button> Center = AccessTools.FieldRefAccess<UnifiedPopup, Button>("buttonCenter");
            internal static readonly AccessTools.FieldRef<UnifiedPopup, Button> Right = AccessTools.FieldRefAccess<UnifiedPopup, Button>("buttonRight");
            internal static readonly AccessTools.FieldRef<UnifiedPopup, Button> Confirm = AccessTools.FieldRefAccess<UnifiedPopup, Button>("buttonConfirm");
            internal static readonly AccessTools.FieldRef<UnifiedPopup, TMP_Text> Body = AccessTools.FieldRefAccess<UnifiedPopup, TMP_Text>("bodyText");
            internal static readonly AccessTools.FieldRef<UnifiedPopup, GuiInputField> Input = AccessTools.FieldRefAccess<UnifiedPopup, GuiInputField>("textEntryField");
        }

        static readonly HashSet<Image> owned = new HashSet<Image>();
        static UnifiedPopup popupHost;
        static TextInput textHost;
        static Surface popup, textInput;
        static bool initialized, dirty;

        internal static bool Owns(Image image) => image != null && owned.Contains(image);

        public static void Initialize()
        {
            if (initialized || !MeadworksHeadless.AllowsUi) return;
            initialized = true;
            dirty = true;
            KitLook.Changed += Changed;
        }

        static void Changed() => dirty = true;

        public static void Tick()
        {
            if (!initialized || !CrucibleUiPlugin.Active || !MeadworksHeadless.AllowsUi) return;
            UnifiedPopup nextPopup = Native.Instance();
            TextInput nextText = TextInput.instance;
            if (popupHost != nextPopup)
            {
                popup?.Dispose(); popup = null; popupHost = nextPopup;
            }
            if (textHost != nextText)
            {
                textInput?.Dispose(); textInput = null; textHost = nextText;
            }
            GameObject popupRoot = popupHost != null ? Native.Root(popupHost) : null;
            GameObject textRoot = textHost != null ? textHost.m_panel : null;
            if (popup != null && popup.Root != popupRoot) { popup.Dispose(); popup = null; }
            if (textInput != null && textInput.Root != textRoot) { textInput.Dispose(); textInput = null; }
            if (popup == null && popupRoot != null && popupRoot.activeInHierarchy)
            {
                popup = new Surface(popupRoot);
                GameObject cover = Native.Cover(popupHost);
                if (cover != null) popup.Protect(cover.GetComponent<Image>());
                // Native capture: the host is a fullscreen shade; its bounded dialog is Popup/bkg.
                var window = popupRoot.transform.Find("Popup");
                var back = window != null ? window.Find("bkg")?.GetComponent<Image>() : null;
                var body = Native.Body(popupHost);
                var action = Native.Right(popupHost);
                if (back != null && body != null && action != null && body.transform.IsChildOf(window)
                    && action.transform.IsChildOf(window)) popup.Panel(back);
                popup.Add(Native.Left(popupHost)); popup.Add(Native.Center(popupHost));
                popup.Add(Native.Right(popupHost)); popup.Add(Native.Confirm(popupHost));
                popup.Add(Native.Body(popupHost)); popup.Add(Native.Input(popupHost));
            }
            if (textInput == null && textRoot != null && textRoot.activeInHierarchy)
            {
                textInput = new Surface(textRoot);
                textInput.Panel(textRoot.GetComponent<Image>());
                textInput.Add(textHost.m_topic); textInput.Add(textHost.m_inputField);
                // TextInput's native action buttons are serialized to its own handlers, not to arbitrary child scripts.
                foreach (Button button in textRoot.GetComponentsInChildren<Button>(true))
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        if (button.onClick.GetPersistentTarget(i) == textHost)
                        {
                            string method = button.onClick.GetPersistentMethodName(i);
                            if (method == nameof(TextInput.OnInput) || method == nameof(TextInput.OnEnter) || method == nameof(TextInput.OnCancel))
                            { textInput.Add(button); break; }
                        }
            }
            popup?.Refresh(dirty); textInput?.Refresh(dirty);
            dirty = false;
        }

        public static void Shutdown()
        {
            KitLook.Changed -= Changed;
            popup?.Dispose(); textInput?.Dispose();
            popup = textInput = null; popupHost = null; textHost = null;
            owned.Clear(); initialized = dirty = false;
        }

        sealed class Surface
        {
            internal readonly GameObject Root;
            readonly List<ImageLook> images = new List<ImageLook>();
            readonly List<ButtonLook> buttons = new List<ButtonLook>();
            readonly Dictionary<TMP_Text, FontLook> fonts = new Dictionary<TMP_Text, FontLook>();
            Image panel;

            bool fresh = true, visible;

            internal Surface(GameObject root)
            {
                Root = root;
                // Host roots can be full-screen modal shades, not dialog panel backgrounds.
                Protect(root.GetComponent<Image>());
            }

            internal void Protect(Image image)
            {
                if (image == null || owned.Contains(image)) return;
                TimberBackground.Apply(image, false);
                KitForged.GamePanel(image, false);
                images.Add(new ImageLook(image)); owned.Add(image);
            }

            internal void Panel(Image image)
            {
                if (image == null) return;
                Protect(image);
                panel = image;
            }

            internal void Add(TMP_Text label)
            {
                if (label != null && label.transform.IsChildOf(Root.transform) && !fonts.ContainsKey(label))
                    fonts.Add(label, new FontLook(label));
            }

            internal void Add(GuiInputField input)
            {
                if (input == null || !input.transform.IsChildOf(Root.transform)) return;
                Add(input.textComponent); Add(input.placeholder as TMP_Text);
            }

            internal void Add(Button button)
            {
                if (button == null || !button.transform.IsChildOf(Root.transform)) return;
                Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
                if (image != null)
                {
                    var look = new ImageLook(image);
                    images.Add(look); owned.Add(image); buttons.Add(new ButtonLook(button, look));
                }
                Add(button.GetComponentInChildren<TMP_Text>(true));
            }

            internal void Refresh(bool changed)
            {
                bool nowVisible = Root != null && Root.activeInHierarchy;
                if (fresh || changed || nowVisible && !visible)
                {
                    bool on = KitLook.On && KitLook.ThemeGamePanels;
                    if (panel != null)
                    {
                        if (on) TimberBackground.RegisterForged(panel);
                        TimberBackground.Apply(panel, on);
                        KitForged.GamePanel(panel, on);
                    }
                    foreach (ButtonLook button in buttons) button.Apply(on);
                    TMP_FontAsset font = on ? KitCanvas.Font : null;
                    foreach (var pair in fonts)
                        if (on && font != null && pair.Key != null) pair.Key.font = font;
                        else pair.Value.Restore();
                    fresh = false;
                }
                visible = nowVisible;
            }

            internal void Dispose()
            {
                if (panel != null) { TimberBackground.Apply(panel, false); KitForged.GamePanel(panel, false); }
                foreach (ButtonLook button in buttons) button.Apply(false);
                foreach (ImageLook image in images) { image.Restore(); owned.Remove(image.Image); }
                foreach (FontLook font in fonts.Values) font.Restore();
            }
        }

        sealed class ImageLook
        {
            internal readonly Image Image;
            readonly Sprite sprite;
            readonly Material material;
            readonly Color color;
            readonly Image.Type type;
            readonly float ppu;
            internal ImageLook(Image image)
            {
                Image = image; sprite = image.sprite; material = image.material; color = image.color;
                type = image.type; ppu = image.pixelsPerUnitMultiplier;
            }
            internal void Restore()
            {
                if (Image == null) return;
                Image.sprite = sprite; Image.material = material; Image.color = color;
                Image.type = type; Image.pixelsPerUnitMultiplier = ppu;
            }
        }

        sealed class ButtonLook
        {
            readonly Button button;
            readonly ImageLook image;
            readonly Selectable.Transition transition;
            readonly SpriteState state;
            internal ButtonLook(Button button, ImageLook image)
            { this.button = button; this.image = image; transition = button.transition; state = button.spriteState; }
            internal void Apply(bool on)
            {
                if (button == null || image.Image == null) return;
                if (!on || KitForgedArt.Button == null)
                { image.Restore(); button.transition = transition; button.spriteState = state; return; }
                image.Image.sprite = KitForgedArt.Button; image.Image.material = null; image.Image.type = Image.Type.Sliced;
                Fit(image.Image, KitForgedArt.WindowPixelsPerUnit);
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState { highlightedSprite = KitForgedArt.ButtonHover, selectedSprite = KitForgedArt.ButtonHover,
                    pressedSprite = KitForgedArt.ButtonPressed, disabledSprite = KitForgedArt.ButtonDisabled ?? KitForgedArt.Button };
            }
        }

        sealed class FontLook
        {
            readonly TMP_Text text;
            readonly TMP_FontAsset font;
            readonly Material material;
            internal FontLook(TMP_Text text) { this.text = text; font = text.font; material = text.fontSharedMaterial; }
            internal void Restore() { if (text != null) { text.font = font; text.fontSharedMaterial = material; } }
        }

        static void Fit(Image image, float pixelsPerUnit) => KitAppearance.Fit(image, pixelsPerUnit);
    }
}
