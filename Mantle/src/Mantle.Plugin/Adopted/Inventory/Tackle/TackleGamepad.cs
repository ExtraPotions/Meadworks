using System.Collections.Generic;
using Mantle.Core;
using Mantle.Ring;
using Mantle.Slots;

namespace Mantle.Tackle
{
    /// <summary>
    /// The gamepad and the tacklebox's pop-up. X on the box in its slot opens it (the game's right click,
    /// <see cref="TackleboxUse"/>) and selects the first cell; B shuts it before it would close the inventory, and the box is
    /// selected again (<see cref="KeyRingGamepad"/>, which also carries a move past the shut pop-up's cells).
    /// </summary>
    public static class TackleGamepad
    {
        /// <summary>The pop-up opened or closed: the gamepad goes to its first cell, or back to the box.</summary>
        public static void Follow(bool open)
        {
            InventoryGrid grid = InventoryGui.instance != null ? InventoryGui.instance.m_playerGrid : null;
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(SlotKind.Tackle);
            IReadOnlyList<Vector2i> box = InventoryState.CellsOf(SlotKind.Tacklebox);
            if (grid == null || cells.Count == 0 || box.Count == 0 || !InventoryState.Active || !ZInput.IsExclusiveGamepadActive())
                return;
            if (open)
            {
                KeyRingGamepad.Select(grid, cells[0]);
                return;
            }
            Slot slot = InventoryState.Layout.SlotAt(grid.m_selected);
            if (slot != null && slot.Kind == SlotKind.Tackle)
                KeyRingGamepad.Select(grid, box[0]);
        }
    }
}
