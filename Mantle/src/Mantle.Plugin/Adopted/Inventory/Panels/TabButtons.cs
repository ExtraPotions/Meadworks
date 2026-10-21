using System;
using Mantle.Core;
using ExtraPotions.Mantle.Embedded.PatchGuard;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Mantle.Panels
{
    /// <summary>
    /// The slot panel's two tab buttons across its top, Gear and Consumables (<see cref="SlotTabs"/>), each half the
    /// panel's inner width. Copies of the game's take-all button like OpenKeep's buttons, so they keep the game's
    /// sprite, font, sound and hover in every panel theme. Taken off the copy: the gamepad key the original answers to
    /// (and its hint), and the components that would undo our changes every frame or on a language change (the game's
    /// button text and image colours, its localizer). The shown tab is lit, the other dimmed; Consumables cannot be
    /// clicked while there are no consumable slots.
    /// </summary>
    public static class TabButtons
    {
        public const float Height = 26f;

        /// <summary>From the buttons' bottom to the first row of cells.</summary>
        public const float Gap = 8f;

        private const float Between = 6f;
        private static readonly Color LitText = new Color(1f, 0.86f, 0.55f, 1f);
        private static readonly Color Lit = Color.white;
        private static readonly Color Dim = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color DimText = new Color(0.78f, 0.74f, 0.66f, 0.85f);

        /// <summary>Both buttons at the panel's top, from its first cell's left edge across <paramref name="width"/>.</summary>
        public static void Place(InventoryGui gui, RectTransform panel, SlotPanelLayout plan, float width)
        {
            float each = (width - Between) / 2f;
            Vector2 at = new Vector2(SlotPanel.Pad, -SlotPanel.Pad);
            Set(Find(gui, panel, "gear", Words.GearTab, () => SlotTabs.Show(SlotTab.Gear)), at, each, plan.Tab == SlotTab.Gear, true);
            at.x += each + Between;
            Button consumables = Find(gui, panel, "consumables", Words.ConsumablesTab, () => SlotTabs.Show(SlotTab.Consumables));
            Set(consumables, at, each, plan.Tab == SlotTab.Consumables, plan.HasConsumables);
        }

        private static string NameOf(string id) => "Mantle_tab_" + id;

        private static Button Find(InventoryGui gui, RectTransform panel, string id, string word, Action click)
        {
            Transform found = panel.Find(NameOf(id));
            if (found != null)
            {
                Caption(found.gameObject, word);   // in the language of this placing (a language change places again)
                return found.GetComponent<Button>();
            }
            return gui.m_takeAllButton != null ? Make(gui, panel, id, word, click) : null;
        }

        private static void Set(Button button, Vector2 at, float width, bool lit, bool usable)
        {
            if (button == null)
                return;
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = at;
            rect.sizeDelta = new Vector2(width, Height);
            rect.localScale = Vector3.one;
            button.interactable = usable;
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = lit ? Lit : Dim;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.color = lit ? LitText : DimText;
        }

        private static Button Make(InventoryGui gui, RectTransform panel, string id, string word, Action click)
        {
            GameObject go = Object.Instantiate(gui.m_takeAllButton.gameObject, panel);
            go.name = NameOf(id);
            go.SetActive(true);
            Strip(go);
            Button button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => Guard.Run("slot panel button " + id, click));
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Caption(go, word);
            return button;
        }

        /// <summary>Off the copy: the original's gamepad key and hint, and what would reset its colours and caption.</summary>
        private static void Strip(GameObject go)
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

        private static void Caption(GameObject go, string word)
        {
            TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
                return;
            text.text = Language.Localize(word);
            text.enableAutoSizing = true;
            text.fontSizeMin = 9f;
            text.fontSizeMax = 16f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;   // the game fonts have no ellipsis glyph (Crucible.UI 0.3.9)
            text.margin = new Vector4(4f, 1f, 4f, 1f);
        }
    }
}
