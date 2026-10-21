using Mantle.Core;
using RoundedFill = ExtraPotions.Meadworks.Crucible.UI.RoundedFill;
using Mantle.Look;
using ExtraPotions.Mantle.Embedded.PlateColumn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Panels
{
    /// <summary>
    /// The Gear tab's stat sheet between the worn slots' two columns (the user's requests, 2026-09-28): a slightly darker
    /// inset with round corners (<see cref="RoundedFill"/>) holding every stat (<see cref="SheetLines"/>: the core numbers,
    /// the game's resistances, the gear's other modifiers and, with Epic Loot, its magic effects) as small rows
    /// (<see cref="StatRows"/>) in a list that scrolls with the mouse wheel (<see cref="SheetScroll"/>). A child of the
    /// slot panel, shown with the Gear tab. Worked out a few times a second while shown and drawn again only when a line
    /// changed.
    /// </summary>
    public static class GearStats
    {
        private const string Name = "Mantle_gearstats";
        private const float Inset = 6f;
        private const float Every = 0.25f;
        private static readonly Color Fill = new Color(0.05f, 0.035f, 0.02f, 0.45f);

        private static readonly StatSheet sheet = new StatSheet();
        private static RectTransform area;
        private static RectTransform content;
        private static float rowsHeight;
        private static TMP_FontAsset font;
        private static float width;
        private static string shown;
        private static float next;

        /// <summary>Over the columns between the first and the last, at most as tall as the tab's rows; hidden on the other tab.</summary>
        public static void Place(InventoryGui gui, RectTransform panel, SlotPanelLayout plan, float step, TMP_FontAsset textFont)
        {
            area = panel.Find(Name) as RectTransform ?? Make(gui, panel);
            content = (RectTransform)area.Find("viewport/content");
            font = textFont;
            area.gameObject.SetActive(plan.Tab == SlotTab.Gear);
            float trim = step - Column.BoxSize;
            area.anchoredPosition = SlotPanel.CellPosition(new Vector2Int(1, 0), step);
            rowsHeight = plan.ContentRows * step - trim;
            area.sizeDelta = new Vector2(plan.SheetColumns * step - trim, rowsHeight);
            width = area.sizeDelta.x - Inset * 2f;
            shown = null;
            next = 0f;
        }

        /// <summary>Every frame the grid is drawn: the sheet worked out again while shown, a few times a second.</summary>
        public static void Refresh()
        {
            if (content == null || !content.gameObject.activeInHierarchy || Time.unscaledTime < next)
                return;
            next = Time.unscaledTime + Every;
            SheetLines.Fill(InventoryState.Player, sheet);
            string key = sheet.Key;
            if (key == shown)
                return;
            shown = key;
            StatRows.Write(content, sheet, width, font);
            // The user, 2026-10-12 17:41: the sheet ends under its last line; a longer list scrolls in the rows' height.
            area.sizeDelta = new Vector2(area.sizeDelta.x, Mathf.Min(rowsHeight, content.sizeDelta.y + Inset * 2f));
        }

        private static RectTransform Make(InventoryGui gui, RectTransform panel)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform), typeof(Image));
            RectTransform area = (RectTransform)go.transform;
            area.SetParent(panel, false);
            area.anchorMin = area.anchorMax = area.pivot = new Vector2(0f, 1f);
            Image fill = go.GetComponent<Image>();
            fill.sprite = RoundedFill.Sprite;
            fill.type = Image.Type.Sliced;
            fill.color = Fill;
            fill.raycastTarget = false;
            SheetScroll.Make(gui, area, Inset, Inset);
            return area;
        }
    }
}
