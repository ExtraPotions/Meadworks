// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Text;
using TMPro;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>Fills the description panel's fixed parts: icon, name, text, row labels, the main and the small button.</summary>
    internal static class PaneText
    {
        public static void Header(PanelParts parts, Sprite? icon, string name, ItemDrop.ItemData? item = null)
        {
            Pills(parts, null, default);
            if (parts.Icon != null)
            {
                parts.Icon.gameObject.SetActive(icon != null);
                parts.Icon.sprite = icon;
                Display.Backdrops.IconBackdrop.Set(parts.Icon, icon != null ? item : null);
            }
            if (parts.Name != null)
            {
                parts.Name.text = name;
            }
        }

        /// <summary>
        /// Shows pills under the text, in the given colour, the text giving up their height; null or none hides them and
        /// gives the text its whole area back (every pane's <see cref="Header"/> starts that way).
        /// </summary>
        public static void Pills(PanelParts parts, System.Collections.Generic.IReadOnlyList<Pill>? pills, Color tone)
        {
            if (parts.Text == null || parts.Pills == null)
            {
                return;
            }
            Rect area = parts.TextArea;
            bool show = pills != null && pills.Count > 0;
            float strip = show ? PillStrip.AreaHeight + 6f : 0f;
            Rects.Place(parts.Text.rectTransform, area.x, area.y, area.width, Mathf.Max(30f, area.height - strip));
            if (!show)
            {
                parts.Pills.Hide();
                return;
            }
            parts.Pills.Place(area.x, area.y + area.height - PillStrip.AreaHeight, area.width);
            parts.Pills.Show(pills!, tone);
        }

        public static void Body(PanelParts parts, string text)
        {
            if (parts.Text != null)
            {
                parts.Text.text = text;
            }
        }

        /// <summary>The two row labels; a null key hides that label (its row is hidden by the tab).</summary>
        public static void Labels(PanelParts parts, string? rune, string? essence)
        {
            Label(parts.RuneLabel, rune);
            Label(parts.EssenceLabel, essence);
        }

        public static void Button(PanelParts parts, string key, bool interactable)
        {
            if (parts.Action == null)
            {
                return;
            }
            parts.Action.interactable = interactable;
            TMP_Text? label = parts.LabelOf(parts.Action);
            if (label != null)
            {
                label.text = Words.Localize(key);
            }
        }

        /// <summary>The small button under the name; a null key hides it.</summary>
        public static void Extra(PanelParts parts, string? key, bool interactable)
        {
            if (parts.Extra == null)
            {
                return;
            }
            parts.Extra.gameObject.SetActive(key != null);
            parts.Extra.interactable = interactable;
            TMP_Text? label = parts.LabelOf(parts.Extra);
            if (label != null && key != null)
            {
                label.text = Words.Localize(key);
            }
        }

        private static void Label(TMP_Text? label, string? key)
        {
            if (label == null)
            {
                return;
            }
            label.gameObject.SetActive(key != null);
            label.text = key != null ? Words.Localize(key) : "";
        }
    }
}
