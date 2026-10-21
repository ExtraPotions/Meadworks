// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Panels/TabButtons.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: renamed TabButtons to GameButtons and generalised from the
// slot panel's two tabs to any copied game button under any panel (KitTabs lays out strips of them); colours and
// caption sizes from Core KitPalette/KitButtonText; clicks guarded with a log line instead of PatchGuard.
// Modified by expDARE, 2026-10-18: all copied-button paths share the reversible kit art and reading font.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Copies of the game's own buttons, with the kit's reversible art and reading font, retaining the game's sound and
    /// button behavior. Taken off the copy: the gamepad key the original answers to
    /// (and its hint), and the components that would undo our changes every frame or on a language change (the game's
    /// button text and image colours, its localizer). Lit buttons are white with warm text, dimmed ones grey.
    /// </summary>
    public static class GameButtons
    {
        private static readonly Color LitText = KitUnity.Colour(KitPalette.TabLitText);
        private static readonly Color Lit = KitUnity.Colour(KitPalette.TabLit);
        private static readonly Color Dim = KitUnity.Colour(KitPalette.TabDim);
        private static readonly Color DimText = KitUnity.Colour(KitPalette.TabDimText);

        /// <summary>The game button to copy: the inventory's take-all button while a world is loaded, else none.</summary>
        public static Button Source
        {
            get
            {
                InventoryGui gui = InventoryGui.instance;
                return gui != null && gui.m_takeAllButton != null ? gui.m_takeAllButton : null;
            }
        }

        /// <summary>The panel's existing copy named <paramref name="name"/>, or a new one when a game button can be copied.</summary>
        public static Button Find(RectTransform panel, string name, string word, Action click)
        {
            Transform found = panel.Find(name);
            if (found != null)
            {
                Button existing = found.GetComponent<Button>();
                KitForged.Button(existing);
                return existing;
            }
            Button source = Source;
            return source != null ? Make(source, panel, name, word, click) : null;
        }

        /// <summary>Top-left at <paramref name="at"/>, this size, lit or dimmed, clickable or not.</summary>
        public static void Set(Button button, Vector2 at, float width, float height, bool lit, bool usable)
        {
            if (button == null)
                return;
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = at;
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            button.interactable = usable;
            Tint(button, lit);
            KitForged.Button(button);
        }

        public static void Tint(Button button, bool lit)
        {
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = lit ? Lit : Dim;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.color = lit ? LitText : DimText;
        }

        public static Button Make(Button source, RectTransform panel, string name, string word, Action click)
        {
            GameObject go = Object.Instantiate(source.gameObject, panel);
            go.name = name;
            go.SetActive(true);
            Strip(go);
            Button button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => Run(name, click));
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Caption(go, word);
            KitForged.Button(button);
            return button;
        }

        /// <summary>Off the copy: the original's gamepad key and hint, and what would reset its colours and caption.</summary>
        public static void Strip(GameObject go)
        {
            foreach (UIGamePad pad in go.GetComponentsInChildren<UIGamePad>(true))
            {
                if (pad.m_hint != null && pad.m_hint.transform.IsChildOf(go.transform))
                    Object.DestroyImmediate(pad.m_hint);
                Object.DestroyImmediate(pad);
            }
            foreach (ButtonTextColor colour in go.GetComponentsInChildren<ButtonTextColor>(true))
                Object.DestroyImmediate(colour);
            foreach (ButtonImageColor colour in go.GetComponentsInChildren<ButtonImageColor>(true))
                Object.DestroyImmediate(colour);
            foreach (Localize localize in go.GetComponentsInChildren<Localize>(true))
                Object.DestroyImmediate(localize);
        }

        public static void Caption(GameObject go, string word)
        {
            TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
                return;
            text.text = KitLocalize.Text(word);
            text.enableAutoSizing = true;
            text.fontSizeMin = KitButtonText.MinSize;
            text.fontSizeMax = KitButtonText.MaxSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.margin = KitUnity.Vector4(KitButtonText.Margin);
        }

        private static void Run(string name, Action click)
        {
            try
            {
                click?.Invoke();
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error($"Crucible.UI: button {name} failed: {e}");
            }
        }
    }
}
