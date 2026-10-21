using ExtraPotions.Mantle.Embedded.PlateColumn;
using Mantle.Core;
using Mantle.Look;
using UnityEngine.UI;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The stat boxes' own panel (the user's call, 2026-09-28): a narrow panel between the inventory panel and the slot
    /// panel holding the PlateColumn library's column of boxes, top to bottom by rank: armour, weight, Elite Creatures
    /// Reborn's world tier, and any other mod's box. A child of the player panel like the slot panel, anchored to its
    /// top-right corner flush with its background, as tall as its boxes (<see cref="Height"/>; the user's call,
    /// 2026-10-02 - it was as tall as the inventory panel's background) and one box wide with a narrow
    /// <see cref="SideMargin"/> on each side and the inventory panel's margin on top, so its first box is level with the grid's first row
    /// (<see cref="PanelDress"/> moves the column there). Drawn before the player panel's background, so the boxes, which
    /// the library keeps right after it, sit on top; its background takes clicks, so a dragged item let go there is not
    /// dropped. Shown while the inventory section lays the panel out.
    /// </summary>
    public static class StatsPanel
    {
        public const string Name = "Mantle_stats";

        public static RectTransform Find(InventoryGui gui) => gui != null ? gui.m_player.Find(Name) as RectTransform : null;

        /// <summary>Whether the panel is part of the layout: whenever the inventory section lays the panel out.</summary>
        public static bool On => InventoryState.Active;

        /// <summary>
        /// Left and right of the boxes (the user asked for a narrower column, 2026-09-28): less than the inventory
        /// panel's margin, still clear of the brown frame and the timber rim. The top keeps that margin, so the first box
        /// stays level with the grid's first row.
        /// </summary>
        public const float SideMargin = 10f;

        /// <summary>One box with <see cref="SideMargin"/> on each side.</summary>
        public static float Width(InventoryGui gui) => SideMargin * 2f + ContentWidth(gui);

        /// <summary>
        /// The width of each box in the column: the compact square, or with Burrow's Quick stack, Store all, Top up and Sort
        /// stacked under the weight plate (they take the plate's width) wide enough for "Quick stack" on one line
        /// (play test P1, 2026-10-13: the 48-unit buttons wrapped every caption).
        /// </summary>
        public static float BoxWidth => BurrowRow.Present ? StowBoxWidth : StatIconLayout.Size;

        /// <summary>Burrow's own row button width (ButtonRowStyle.RowWidth), where its captions fit on one line.</summary>
        public const float StowBoxWidth = 100f;

        /// <summary>Fit the boxes, but leave room for any wider plates another mod adds.</summary>
        public static float ContentWidth(InventoryGui gui)
        {
            float width = BoxWidth;
            Transform boxes = gui.m_player.Find("PlateColumn_boxes");
            // Indexed, not foreach: a Transform's enumerator is garbage, and this runs every frame the grid is drawn.
            for (int i = 0; boxes != null && i < boxes.childCount; i++)
                if (boxes.GetChild(i) is RectTransform rect && rect.gameObject.activeSelf)
                    width = Mathf.Max(width, rect.rect.width);
            return width;
        }

        /// <summary>
        /// The inventory panel's margin above the first box (keeping it level with the grid's first row), the boxes
        /// shown, and the same margin under the last one: the panel grows and shrinks with the boxes other mods add.
        /// </summary>
        public static float Height(InventoryGui gui) => PanelDress.Margin(gui) * 2f + ColumnHeight(gui);

        /// <summary>The active boxes one under the other, the library's spacing between them.</summary>
        public static float ColumnHeight(InventoryGui gui)
        {
            Transform boxes = gui.m_player.Find("PlateColumn_boxes");
            float height = 0f;
            int count = 0;
            for (int i = 0; boxes != null && i < boxes.childCount; i++)
                if (boxes.GetChild(i) is RectTransform rect && rect.gameObject.activeSelf)
                {
                    height += rect.rect.height;
                    count++;
                }
            return height + Mathf.Max(0, count - 1) * Column.Spacing;
        }

        /// <summary>
        /// The first box's top-left, from the player panel's top-right corner (where the column's container is anchored):
        /// centred between the grid's last column and the background's right edge (play test P1, 2026-10-13: the gap on the
        /// grid side was the panel's own margin plus the frame's reach plus <see cref="SideMargin"/>, three times the right one).
        /// With the grid's margin not known yet, <see cref="SideMargin"/> past the panel's frame as before.
        /// </summary>
        public static Vector2 ColumnStart(InventoryGui gui)
        {
            float x = PanelDress.StatsPanelLeft(gui) + SideMargin;
            float grid = PanelDress.GridMargin(gui);
            if (grid >= 0f)
                x = PanelMath.CentredColumn(grid, PanelDress.StatsPanelLeft(gui), Width(gui), ContentWidth(gui));
            return new Vector2(x, PanelDress.Overhang(gui) - PanelDress.Margin(gui));
        }


        public static void Refresh(InventoryGui gui)
        {
            RectTransform panel = Find(gui);
            if (panel == null && On)
                panel = SlotPanel.MakePanel(gui, Name);
            if (panel == null)
                return;
            panel.GetComponent<UnityEngine.UI.Image>().enabled = false;
            panel.gameObject.SetActive(On);
            if (On)
                Fit(gui, panel);
            ScreenLayout.ReportStatus(On ? panel.sizeDelta : Vector2.zero);
        }

        private static void Fit(InventoryGui gui, RectTransform panel)
        {
            float overhang = PanelDress.Overhang(gui);
            panel.sizeDelta = new Vector2(Width(gui), Height(gui));
            panel.anchoredPosition = new Vector2(PanelDress.StatsPanelLeft(gui), overhang);
            GridSkin.GamePanel(panel.GetComponent<Image>());
            SlotPanel.BehindBackground(gui, panel);
        }
    }
}
