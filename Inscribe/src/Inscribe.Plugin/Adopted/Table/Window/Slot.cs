// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Display.Backdrops;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// One of the game's requirement slots (<c>res_bkg</c>: a framed icon, a name at the top, an amount at the bottom, a
    /// tooltip), as the crafting panel shows a recipe's materials. The window's cost row uses the copied ones; its rune
    /// and essence rows use smaller copies that can be clicked and glow when chosen.
    /// </summary>
    internal sealed class Slot
    {

        public Slot(GameObject root)
        {
            Root = root;
            Background = root.GetComponent<Image>();
            Icon = root.transform.Find("res_icon")?.GetComponent<Image>();
            Name = root.transform.Find("res_name")?.GetComponent<TMP_Text>();
            Amount = root.transform.Find("res_amount")?.GetComponent<TMP_Text>();
            Tip = HoverOf(root);
        }

        // The game's tooltip follows the pointer; the table's hover text shows beside the slot (HoverBox), so the copy's
        // UITooltip gives way to a TipHover (once: copies of a converted slot carry the TipHover already).
        private static TipHover HoverOf(GameObject root)
        {
            UITooltip? game = root.GetComponent<UITooltip>();
            if (game != null)
            {
                HoverBox.Remember(game.m_tooltipPrefab);
                Object.DestroyImmediate(game);
            }
            TipHover? tip = root.GetComponent<TipHover>();
            return tip != null ? tip : root.AddComponent<TipHover>();
        }

        public GameObject Root { get; }
        public Image? Background { get; }
        public Image? Icon { get; }
        public TMP_Text? Name { get; }
        public TMP_Text? Amount { get; }
        public TipHover Tip { get; }
        public Image? Mark { get; private set; }

        /// <summary>
        /// Makes it a button with a chosen-mark: the mod's own rarity backdrop art (a soft glow inside a bright rim, the
        /// look the user chose for magic items on 2026-10-05) behind the icon, tinted in the chosen thing's colour (user,
        /// 2026-10-07: the orange outline "should look cooler").
        /// </summary>
        public Button Clickable()
        {
            Button button = Root.AddComponent<Button>();
            button.targetGraphic = Background;
            var go = new GameObject("ecf_pick", typeof(RectTransform), typeof(Image));
            go.layer = Root.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(Root.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Mark = go.GetComponent<Image>();
            Mark.sprite = BackdropArt.Sprite;
            Mark.raycastTarget = false;
            Mark.enabled = false;
            return button;
        }

        /// <summary>Shows an icon and an amount, with the game's red for an amount not met.</summary>
        public void Show(Sprite? icon, string? name, string amount, bool enough, bool dim)
        {
            Set(Icon, icon != null);
            if (Icon != null)
            {
                Icon.sprite = icon;
                Icon.color = dim ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            }
            Set(Name, name != null);
            if (Name != null)
            {
                Name.text = name ?? "";
            }
            Set(Amount, amount.Length > 0);
            if (Amount != null)
            {
                Amount.text = amount;
                Amount.color = enough ? Color.white : Color.red;
            }
        }

        public void Tooltip(string topic, string text) => Tip.Set(topic, text);

        /// <summary>Shows the chosen-mark in the tone given (an essence's or a rune's colour), or hides it.</summary>
        public void Choose(bool chosen, Color tone = default)
        {
            if (Mark == null)
            {
                return;
            }
            Mark.enabled = chosen;
            if (chosen)
            {
                Mark.color = tone == default ? Color.white : tone;
            }
        }

        /// <summary>An empty slot: the frame alone, as the game shows an unused requirement.</summary>
        public void Clear()
        {
            Set(Icon, false);
            Set(Name, false);
            Set(Amount, false);
            Tooltip("", "");
            Choose(false);
        }

        private static void Set(Component? part, bool on)
        {
            if (part != null && part.gameObject.activeSelf != on)
            {
                part.gameObject.SetActive(on);
            }
        }
    }
}
