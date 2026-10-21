using Mantle.Core;
using Mantle.Layout;
using Mantle.Look;
using Mantle.Ring;
using Mantle.Slots;
using Mantle.Tackle;
using TMPro;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// Puts each element of the player grid where it belongs (see <see cref="SlotElements"/> for when): main cells stay
    /// in the grid, the hotbar numbers stop at 8 (the game numbers every cell of row 0, its keys are 1 to 8), slot cells
    /// go to their place in the slot panel with their caption, ring cells to the key ring's pop-up (<see cref="KeyRingCells"/>)
    /// beside the ring's button, the tacklebox's cells to its pop-up with their caption (<see cref="TackleCells"/>), and
    /// the cells after the last slot are hidden. With the module off only the skin changes.
    /// </summary>
    public static class ElementPlacer
    {
        public static InventoryElement PurseElement { get; private set; }

        public static InventoryElement TackleboxElement { get; private set; }

        public static void PlaceAll(InventoryGui gui, InventoryGrid grid)
        {
            PurseElement = null;
            TackleboxElement = null;
            SlotLabels.Clear();
            KeyRingCells.Clear();
            TackleCells.Clear();
            InventoryLayout layout = InventoryState.Active ? InventoryState.Layout : null;
            if (layout != null)
                SlotPanel.Refresh(gui);
            RectTransform panel = SlotPanel.Find(gui);
            SlotPanelLayout plan = layout != null ? SlotPanel.Plan() : null;
            KeyRingPopup.Place(gui, panel, plan, grid.m_elementSpace);
            TacklePopup.Place(gui, panel, plan, grid.m_elementSpace);
            foreach (InventoryElement element in grid.m_elements)
            {
                GridSkin.Cell(element);
                if (layout != null)
                    Place(gui, element, layout, panel, plan, grid.m_elementSpace);
            }
            KeyRingButton.Place(gui, panel, plan, grid.m_elementSpace, ButtonFont(gui));
        }

        private static void Place(InventoryGui gui, InventoryElement element, InventoryLayout layout, RectTransform panel, SlotPanelLayout plan, float step)
        {
            Vector2i pos = element.Position;
            BlockedCell.Mark(element, pos.y < layout.MainRows && layout.IsBlocked(pos));   // a backpack's partly used last row
            if (pos.y < layout.MainRows)
            {
                element.gameObject.SetActive(true);
                if (pos.y == 0 && pos.x >= InventorySettings.GameWidth)
                    HideBinding(element);
                return;
            }
            int index = (pos.y - layout.MainRows) * layout.Width + pos.x;
            Slot slot = index < layout.Slots.Count ? layout.Slots[index] : null;
            element.gameObject.SetActive(slot != null);
            if (slot != null)
                PlaceSlot(gui, element, slot, index, panel, plan, step);
        }

        private static void PlaceSlot(InventoryGui gui, InventoryElement element, Slot slot, int index, RectTransform panel, SlotPanelLayout plan, float step)
        {
            if (slot.Kind == SlotKind.Purse)
                PurseElement = element;
            if (slot.Kind == SlotKind.Tacklebox)
                TackleboxElement = element;
            if (slot.Kind == SlotKind.Key && KeyRingCells.Adopt(gui, element, slot))
                return;
            if (slot.Kind == SlotKind.Tackle && TackleCells.Adopt(gui, element, slot))
            {
                SlotLabels.Set(element, slot, ButtonFont(gui));
                return;
            }
            if (panel == null || !plan.Cells.TryGetValue(index, out Vector2Int cell))
            {
                element.gameObject.SetActive(false);
                return;
            }
            MoveInto(panel, element, SlotPanel.CellPosition(cell, step));
            SlotLabels.Set(element, slot, ButtonFont(gui));
        }

        /// <summary>The game's serif, the font of its buttons (the take-all button's text).</summary>
        internal static TMPro.TMP_FontAsset ButtonFont(InventoryGui gui)
        {
            TMPro.TMP_Text text = gui.m_takeAllButton != null ? gui.m_takeAllButton.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
            return text != null ? text.font : null;
        }

        private static void MoveInto(RectTransform panel, InventoryElement element, Vector2 position)
        {
            RectTransform rect = (RectTransform)element.transform;
            rect.SetParent(panel, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            // The grid reuses its elements: one the key ring or tacklebox pop-up took before keeps that pop-up's smaller scale.
            rect.localScale = Vector3.one;
            UnityEngine.UI.Image background = element.GetComponent<UnityEngine.UI.Image>();
            if (background != null)
                background.color = Color.white;
        }

        private static void HideBinding(InventoryElement element)
        {
            Transform binding = element.transform.Find("binding");
            TMP_Text text = binding != null ? binding.GetComponent<TMP_Text>() : null;
            if (text != null)
                text.enabled = false;
        }
    }
}
