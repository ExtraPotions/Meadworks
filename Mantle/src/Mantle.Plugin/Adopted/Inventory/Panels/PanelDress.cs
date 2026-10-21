using ExtraPotions.Mantle.Embedded.PlateColumn;
using Mantle.Look;
using UnityEngine.UI;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// Where the panels beside the inventory panel go and where the stat boxes sit. Left to right, SideGap apart: the
    /// inventory panel, the stat boxes' panel (<see cref="StatsPanel"/>), the slot panel (<see cref="SlotPanel"/>). While
    /// the inventory section lays the panel out, the PlateColumn library's column of boxes moves into the stats panel;
    /// otherwise it stays at the library's own place right of the inventory panel. The container stays the library's
    /// own (a child of the player panel named <c>PlateColumn_boxes</c>, a vertical column in rank order), so every copy
    /// of the library still finds it, adds its boxes and orders them; the library sets the container's place only when
    /// it makes it, so the move holds. Mantle makes the column itself when no other mod has, so the game's armour and
    /// weight are boxes too. Checked every frame the grid is drawn; nothing is written unless it changed.
    /// </summary>
    public static class PanelDress
    {
        /// <summary>Between a side panel and a pop-up under it, or a chest's panel beside a pop-up.</summary>
        public const float Gap = 12f;

        /// <summary>
        /// Between the panels side by side (inventory, stat boxes, slots): 12 until the user asked for them closer,
        /// 2026-09-28.
        /// </summary>
        public const float SideGap = 4f;
        // Timber sprites have transparent outer edges; overlap them to join the visible frame.
        public const float FrameOverlap = 0f;
        private static RectTransform joinedBackground;
        private static Vector2 backgroundSize, backgroundPosition;
        private const string Boxes = "PlateColumn_boxes";

        /// <summary>How far the panel's background reaches past the panel's rect (its stretched Bkg's size delta).</summary>
        public static float Overhang(InventoryGui gui)
        {
            Image background = GridSkin.Background(gui.m_player);
            if (background == null) return 0f;
            Remember(background.rectTransform);
            return Mathf.Max(0f, backgroundSize.x / 2f);
        }

        /// <summary>The first grid row's top under the panel's top: the grid's 2 unit inset plus the frame's pad.</summary>
        public static float FirstRowTop => 2f + GridSkin.TopPad;

        /// <summary>From a side panel's edge to its first cell: the inventory panel's own margin (overhang plus the first row's top).</summary>
        public static float Margin(InventoryGui gui) => Overhang(gui) + FirstRowTop;

        private static readonly Vector3[] corners = new Vector3[4];

        /// <summary>
        /// From the main grid's last column to the player panel's right rect edge, in the panel's units; -1 while the grid has
        /// no cells. Read from the first row's last cell (the main grid's cells keep the grid's parent).
        /// </summary>
        public static float GridMargin(InventoryGui gui)
        {
            InventoryGrid grid = gui.m_playerGrid;
            Mantle.Layout.InventoryLayout layout = Mantle.Core.InventoryState.Active ? Mantle.Core.InventoryState.Layout : null;
            if (grid == null || layout == null || layout.Width <= 0 || grid.m_elements.Count < layout.Width)
                return -1f;
            InventoryElement last = grid.m_elements[layout.Width - 1];
            if (last == null || !last.gameObject.activeInHierarchy)
                return -1f;
            ((RectTransform)last.transform).GetWorldCorners(corners);
            float right = gui.m_player.InverseTransformPoint(corners[2]).x;
            return Mathf.Max(0f, gui.m_player.rect.xMax - right);
        }

        /// <summary>Where the stats panel's left edge sits, right of the panel's rect edge.</summary>
        public static float StatsPanelLeft(InventoryGui gui) => Overhang(gui) - FrameOverlap;

        /// <summary>Where the slot panel's left edge sits: a gap past the stats panel, or where it would be without it.</summary>
        public static float SlotPanelLeft(InventoryGui gui) =>
            StatsPanelLeft(gui) + (StatsPanel.On ? StatsPanel.Width(gui) + SideGap : 0f);

        public static void Update(InventoryGui gui)
        {
            Image image = GridSkin.Background(gui.m_player);
            if (image != null)
            {
                Remember(image.rectTransform);
                float extra = StatsPanel.On ? StatsPanel.Width(gui) : 0f;
                // Every frame: written only when it differs, as it does only when the stat boxes change.
                Vector2 size = backgroundSize + new Vector2(extra, 0f), at = backgroundPosition + new Vector2(extra / 2f, 0f);
                if (image.rectTransform.sizeDelta != size) image.rectTransform.sizeDelta = size;
                if (image.rectTransform.anchoredPosition != at) image.rectTransform.anchoredPosition = at;
            }
            RectTransform boxes = gui.m_player.Find(Boxes) as RectTransform ?? Column.Boxes(gui);
            if (boxes == null)
                return;
            bool inPanel = StatsPanel.On && StatsPanel.Find(gui) != null;
            StatIconFrames.Apply(gui, boxes, inPanel);
            float width = inPanel ? StatsPanel.ContentWidth(gui) : Column.BoxSize;
            if (!Mathf.Approximately(boxes.sizeDelta.x, width))
            {
                boxes.sizeDelta = new Vector2(width, boxes.sizeDelta.y);
                SlotPanel.Refresh(gui);
            }
            Move(boxes, inPanel ? StatsPanel.ColumnStart(gui) : new Vector2(Column.Left, -Column.Top));
        }

        private static void Remember(RectTransform rect)
        {
            if (joinedBackground == rect) return;
            joinedBackground = rect; backgroundSize = rect.sizeDelta; backgroundPosition = rect.anchoredPosition;
        }

        private static void Move(RectTransform boxes, Vector2 at)
        {
            if ((boxes.anchoredPosition - at).sqrMagnitude > 0.01f)
                boxes.anchoredPosition = at;
        }
    }
}
