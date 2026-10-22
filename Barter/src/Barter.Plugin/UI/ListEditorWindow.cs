// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ExtraPotions.Barter.Lists;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Meadworks.Crucible;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Barter.Runtime
{
    internal static class ListEditorWindow
    {
        private static KitWindow window;
        private static BarterModalInput input;
        private static KitTextField content;
        private static TMP_Text heading, status;
        private static string[] names;
        private static int index;
        private static string revision, originalText;
        private static bool busy;
        private static Button save;
        private static long generation;
        private static long session;

        internal static bool IsOpen => window != null && window.IsOpen;
        internal static void Close() { generation++; window?.Close(); }

        internal static void Open()
        {
            if (!ListEditor.CanEdit || IsOpen || TraderServicesWindow.IsOpen) return;
            if (window == null) Create();
            if (window == null) return;
            session = ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation;
            names = TradeLists.Files.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            index = 0; busy = false; Load();
            StoreUi.ModalOpened(); input.Reset(); window.Open();
        }

        internal static void HandleInput() => input?.Tick();

        private static void Create()
        {
            window = KitWindow.Create("barter_lists", new Vector2(760f, 600f), typing: true);
            if (window == null) return;
            input = new BarterModalInput(window);
            heading = KitCanvas.Text(window.Panel, "file", "", 20f, Color.white, TextAlignmentOptions.TopLeft, false);
            Place((RectTransform)heading.transform, 16, 16, 728, 30);
            content = KitTextField.Create(window.Panel, "content", KitFieldMode.Text, new Vector2(16, 62), new Vector2(728, 420),
                BarterText.Text("editor_title"), BarterText.Text("editor_title"));
            if (content?.Input != null)
            {
                content.Input.characterLimit = ListEditCodec.TextLimit;
                content.Input.lineType = TMP_InputField.LineType.MultiLineNewline;
                content.Input.textComponent.alignment = TextAlignmentOptions.TopLeft;
                content.Input.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
                input.Add(content.Input, 0, 1);
            }
            status = KitCanvas.Text(window.Panel, "status", "", 15f, Color.white, TextAlignmentOptions.TopLeft, true);
            Place((RectTransform)status.transform, 16, 492, 728, 52);
            Add("previous", 16, () => Move(-1), 0);
            Add("next", 166, () => Move(1), 1);
            save = Add("editor_save", 316, Save, 2);
            Add("editor_reload", 466, Load, 3);
            Add("editor_close", 606, () => window.Close(), 4);
        }

        private static Button Add(string key, float x, Action action, int column)
        {
            var button = KitButton.Create(window.Panel, key, BarterText.Text(key), action, new Vector2(x, 550), new Vector2(130, 34));
            input.Add(button, column, 2); return button;
        }

        private static void Move(int direction)
        {
            if (busy || names == null || names.Length == 0) return;
            // Changing file reloads only after the player explicitly saved or discarded a draft.
            if (content.Text != originalText) { status.text = BarterText.Text("editor_unsaved"); return; }
            index = (index + direction + names.Length) % names.Length; Load();
        }

        private static void Load()
        {
            if (busy) return;
            generation++;
            revision = ListEditRules.Revision(TradeLists.Files);
            bool have = names != null && names.Length > 0;
            heading.text = BarterText.Text("editor_title") + (have ? ": " + names[index] : "");
            string original = "";
            if (have && !TradeLists.Files.TryGetValue(names[index], out original))
            { save.interactable = false; status.text = BarterText.Text("editor_failed"); return; }
            originalText = original;
            bool fits = !have || ListEditCodec.FitsRequest(names[index], revision, original);
            content.Text = fits ? original : "";
            content.SetUsable(fits);
            status.text = BarterText.Text("editor_help");
            save.interactable = have && fits;
            if (!save.interactable && have) status.text = BarterText.Text("editor_large");
        }

        private static void Save()
        {
            if (busy || names == null || names.Length == 0) return;
            if (session != ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation)
            { status.text = BarterText.Text("editor_failed"); save.interactable = false; return; }
            if (!ListEditCodec.FitsRequest(names[index], revision, content.Text))
            { status.text = BarterText.Text("editor_large"); return; }
            var errors = ListEditor.ValidateSnapshot(names[index], content.Text);
            if (errors.Count > 0) { status.text = BarterText.Text("editor_invalid"); return; }
            long opened = generation;
            string submitted = content.Text;
            busy = true; save.interactable = false; status.text = BarterText.Text("editor_pending");
            content.SetUsable(false);
            if (!ListEditor.Submit(names[index], revision, submitted, success =>
            {
                if (opened != generation || window == null) return;
                content.SetUsable(true);
                busy = false; save.interactable = true;
                status.text = BarterText.Text(success ? "editor_saved" : "editor_failed");
                if (success) { originalText = submitted; revision = ListEditRules.Revision(TradeLists.Files); }
            })) { content.SetUsable(true); busy = false; save.interactable = true; status.text = BarterText.Text("editor_failed"); }
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }

        internal static void Dispose()
        { generation++; content?.Destroy(); content = null; window?.Destroy(); window = null; input = null; busy = false; }
    }
}
