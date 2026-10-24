// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// Arranges the copied crafting panel for the Rune Table (rune-table.md section 6), once per copy: the tabs side by
    /// side as the game spaces its Craft and Upgrade tabs, the list's top level with the description's (another mod may
    /// have pushed it down for its own bar), and, between the description text and the requirement slots, two labelled
    /// rows of small slots: the runes and the essences. The small "Style" button sits under the name.
    /// </summary>
    internal static class PanelLayout
    {
        public const int TabCount = 3;
        private const float LabelHeight = 22f;
        private const float SlotSize = 44f;
        private const float Gap = 6f;
        private const float CostScale = 0.8f;

        public static void Arrange(PanelParts parts, InventoryGui gui, Action<int> tab, Action<int> rune, Action<int> essence)
        {
            MakeTabs(parts, tab);
            AlignList(parts);
            parts.List = new ListRows(gui.m_recipeElementPrefab, parts.Scroll, gui.m_recipeListSpace);
            parts.Cost = new CostRow(parts.Requirements);
            MakeRows(parts, rune, essence);
            PlaceCost(parts);
            PlaceExtra(parts);
        }

        private static void MakeTabs(PanelParts parts, Action<int> clicked)
        {
            Button? template = parts.TabTemplate;
            if (template == null)
            {
                return;
            }
            var rect = (RectTransform)template.transform;
            float step = rect.rect.width * 1.07f;
            var tabs = new Button[TabCount];
            for (int i = 0; i < TabCount; i++)
            {
                Button tab = i == 0 ? template : Object.Instantiate(template, rect.parent, false);
                ((RectTransform)tab.transform).anchoredPosition = rect.anchoredPosition + new Vector2(i * step, 0f);
                tab.onClick = new Button.ButtonClickedEvent();
                int index = i;
                tab.onClick.AddListener(() => clicked(index));
                tabs[i] = tab;
            }
            parts.Tabs = tabs;
            parts.TabOrigin = rect.anchoredPosition;
            parts.TabStep = step;
        }

        /// <summary>The shown tabs side by side from the first tab's place, so a hidden one leaves no gap.</summary>
        public static void PlaceTabs(PanelParts parts, Func<int, bool> shown)
        {
            int slot = 0;
            for (int i = 0; i < parts.Tabs.Length; i++)
            {
                if (!shown(i))
                {
                    continue;
                }
                var rect = (RectTransform)parts.Tabs[i].transform;
                Vector2 at = parts.TabOrigin + new Vector2(slot * parts.TabStep, 0f);
                if (rect.anchoredPosition != at)
                {
                    rect.anchoredPosition = at;
                }
                slot++;
            }
        }

        private static void AlignList(PanelParts parts)
        {
            var list = parts.Scroll != null ? parts.Scroll.transform.parent as RectTransform : null;
            if (list == null || parts.Description == null || list.parent != parts.Description.parent)
            {
                return;
            }
            float shift = Rects.FromTopLeft(list).y - Rects.FromTopLeft(parts.Description).y;
            if (shift > 0f)
            {
                list.offsetMax += new Vector2(0f, shift);
            }
        }

        // Text, then "Rune" and its row, then "Essence" and its row, ending just above the requirement slots.
        private static void MakeRows(PanelParts parts, Action<int> rune, Action<int> essence)
        {
            GameObject? template = parts.Cost?.Template;
            if (parts.Text == null || parts.Requirements == null || parts.Description == null || template == null)
            {
                return;
            }
            Rect text = Rects.Pin(parts.Text.rectTransform);
            float bottom = RowsBottom(parts);
            float top = bottom - 2f * (LabelHeight + SlotSize + Gap);
            parts.TextArea = new Rect(text.x, text.y, text.width, Mathf.Max(60f, top - text.y - Gap));
            Rects.Place(parts.Text.rectTransform, text.x, text.y, text.width, parts.TextArea.height);
            parts.Text.overflowMode = TextOverflowModes.Ellipsis;
            parts.Pills = new PillStrip(parts.Description, parts.Text);
            float y = top;
            parts.RuneLabel = Label(parts, text.x, y, text.width);
            // Room for the runes, or on the Sockets tab the Dvergr Chisel and every gem.
            int stones = Mathf.Max(Rules.StoneCatalog.BuiltInIds.Length, 1 + Rules.StoneCatalog.GemIds.Length);
            parts.Runes = new SlotRow(parts.Description, template, stones,
                new Rect(text.x, y + LabelHeight, text.width, SlotSize), rune);
            y += LabelHeight + SlotSize + Gap;
            parts.EssenceLabel = Label(parts, text.x, y, text.width);
            // The pure essence item first (what you have), then the essences to choose from.
            parts.Essences = new SlotRow(parts.Description, template, Tables.Essences.All.Length + 1,
                new Rect(text.x, y + LabelHeight, text.width, SlotSize), essence);
            parts.Recasting = new RecastingControls(parts.Description, new Rect(text.x, y + LabelHeight + 4f, text.width, SlotSize - 8f));
        }

        // The rows end above the button's row: the cost slots move onto it (PlaceCost), so their old row is free.
        private static float RowsBottom(PanelParts parts)
        {
            Transform? buttonRow = parts.Action != null ? parts.Action.transform.parent : null;
            RectTransform above = buttonRow as RectTransform ?? parts.Requirements!;
            return Rects.FromTopLeft(above).y - Gap;
        }

        // A copy of the description text, small, in the colour of the item name above it.
        private static TMP_Text Label(PanelParts parts, float x, float y, float width)
        {
            TMP_Text label = Object.Instantiate(parts.Text!, parts.Description, false);
            label.name = "ECF_Label";
            Rects.Place(label.rectTransform, x, y, width, LabelHeight);
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.fontSize = Mathf.Min(label.fontSize, LabelHeight - 4f);
            if (parts.Name != null)
            {
                label.color = parts.Name.color;
            }
            return label;
        }

        // The cost slots on the button's row, left of a narrower button, smaller (user 2026-10-07: "put that next to the
        // button on the button, make them smaller ... make the button smaller"): room for CostRow.Most of them.
        private static void PlaceCost(PanelParts parts)
        {
            RectTransform? button = parts.Action != null ? (RectTransform)parts.Action.transform : null;
            if (button == null || parts.Cost == null)
            {
                return;
            }
            Rect at = Rects.Pin(button);
            float size = at.height * CostScale;
            float room = CostRow.Most * (size + Gap);
            Rects.Place(button, at.x + room, at.y, at.width - room, at.height);
            for (int i = 0; i < parts.Cost.Slots.Count; i++)
            {
                Shrink((RectTransform)parts.Cost.Slots[i].Root.transform, button.parent, at.x + i * (size + Gap),
                    at.y + (at.height - size) / 2f, size);
            }
        }

        // A slot moved under the button's row and scaled down to the size given, its top left at x, y.
        private static void Shrink(RectTransform slot, Transform row, float x, float y, float size)
        {
            Vector2 full = slot.rect.size;
            slot.SetParent(row, false);
            Rects.Place(slot, x, y, full.x, full.y);
            slot.localScale = Vector3.one * (size / Mathf.Max(1f, full.x));
        }

        private static void PlaceExtra(PanelParts parts)
        {
            if (parts.Extra == null)
            {
                return;
            }
            // The game's own listener (the style dialog) goes first, whatever else is missing.
            parts.Extra.onClick = new Button.ButtonClickedEvent();
            if (parts.Name != null)
            {
                Rect name = Rects.FromTopLeft(parts.Name.rectTransform);
                Rects.Place((RectTransform)parts.Extra.transform, name.x, name.y + name.height, 160f, 26f);
            }
        }
    }
}
