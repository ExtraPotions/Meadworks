using ExtraPotions.Mantle.Embedded.PlateColumn;
using Mantle.Core;
using Mantle.Look;
using Mantle.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Tackle
{
    /// <summary>
    /// The tacklebox's pop-up: a small panel hanging under the slot panel like the key ring's (<see cref="PopupPlace"/>),
    /// centred under the Tacklebox slot, with the box's cells in rows of four (<see cref="TackleCells"/>), 1 to 8 with
    /// Mantle's boxes. The panel wears the slot panel's look in every theme (<see cref="GridSkin.GamePanel"/>: in Timber the
    /// forged wood and iron frame of the gear panel above it, playtest 2026-10-20). A child of the player panel, so it moves
    /// and closes with it; its background takes clicks, so a dragged item let go on it is not dropped on the ground. Shown
    /// while <see cref="TackleState.Open"/> and a box lies in the slot.
    /// </summary>
    public static class TacklePopup
    {
        public const string Name = "Mantle_tacklebox";
        private static float slotLeft;
        private static float boxCentre;
        private static float top;
        private static float step = 70f;
        private static bool placed;
        private static bool fresh;

        public static RectTransform Find(InventoryGui gui) => gui != null ? gui.m_player.Find(Name) as RectTransform : null;

        /// <summary>
        /// Where the pop-up hangs, under the slot panel and the Tacklebox slot, worked out when the grid is placed; it is
        /// sized on the next frame. Hidden without a Tacklebox slot.
        /// </summary>
        public static void Place(InventoryGui gui, RectTransform slotPanel, SlotPanelLayout plan, float gridStep)
        {
            RectTransform popup = Find(gui);
            placed = slotPanel != null && plan?.TackleboxCell != null;
            fresh = true;
            if (!placed)
            {
                popup?.gameObject.SetActive(false);
                return;
            }
            if (popup == null)
                Make(gui);
            step = gridStep;
            slotLeft = slotPanel.anchoredPosition.x;
            boxCentre = slotLeft + SlotPanel.CellPosition(plan.TackleboxCell.Value, step).x + Column.BoxSize / 2f;
            top = slotPanel.anchoredPosition.y - slotPanel.sizeDelta.y - PanelDress.Gap;
        }

        /// <summary>Every frame the grid is drawn: shown while open with cells, sized when the grid was placed again.</summary>
        public static void Refresh(InventoryGui gui)
        {
            RectTransform popup = Find(gui);
            if (popup == null)
                return;
            bool show = placed && TackleState.Open && Tacklebox.Active && TackleCells.Count > 0;
            if (popup.gameObject.activeSelf != show)
                popup.gameObject.SetActive(show);
            if (!show)
                return;
            if (fresh)
                Arrange(popup);
            PopupPlace.Under(gui, popup, slotLeft, boxCentre, top);
            TackleCells.Refresh(InventoryState.Player.GetInventory());
        }

        /// <summary>Just big enough for its rows of cells and the slot panel's margin round them.</summary>
        private static void Arrange(RectTransform popup)
        {
            fresh = false;
            float scaled = step * TackleCells.CellScale;
            float trim = (step - Column.BoxSize) * TackleCells.CellScale;
            int columns = Mathf.Min(TackleCells.Columns, TackleCells.Count);
            int rows = (TackleCells.Count + TackleCells.Columns - 1) / TackleCells.Columns;
            float pad = SlotPanel.Pad;
            popup.sizeDelta = new Vector2(pad * 2f + columns * scaled - trim, pad * 2f + rows * scaled - trim);
            GridSkin.GamePanel(popup.GetComponent<Image>());
            TackleCells.Arrange(pad, scaled);
        }

        private static void Make(InventoryGui gui)
        {
            RectTransform popup = SlotPanel.MakePanel(gui, Name);
            popup.pivot = new Vector2(0f, 1f);
            GridSkin.GamePanel(popup.GetComponent<Image>());   // remembers the game's sprite while it still shows it
        }
    }
}
