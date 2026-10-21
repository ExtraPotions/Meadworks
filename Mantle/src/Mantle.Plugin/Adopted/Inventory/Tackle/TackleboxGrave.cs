using System.Collections.Generic;
using Mantle.Backpacks;
using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;

namespace Mantle.Tackle
{
    /// <summary>
    /// A tacklebox and the grave, as for a backpack (<see cref="BackpackGrave"/>). A grave made while a box lay in its slot
    /// holds the box at the Tacklebox slot's cell and its bait at the cells after it; the player who wakes has no cells, so
    /// the game's take all (which puts each item back at its old cell) would miss them. So before a take all from the
    /// player's own grave the box that was carried goes back into its slot first and the layout grows to match; the
    /// grave's easy fit check (<see cref="GraveFit"/>) sends the box and its bait to their own cells. A spare box carried
    /// in the grid is not mistaken for it. With Keep Slots On Death the box stays with the player and only its bait goes
    /// to the grave, into cells that are still there.
    /// </summary>
    public static class TackleboxGrave
    {

        /// <summary>
        /// Called by <see cref="GravePatches"/> before a take all from a grave, after the backpack went back on (so the
        /// cells line up): the box into its empty slot, then the layout.
        /// </summary>
        public static void PutBackFirst(Inventory inventory, Inventory grave)
        {
            ItemDrop.ItemData box = Waiting(grave);
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(SlotKind.Tacklebox);
            if (box == null || cells.Count == 0 || inventory.GetItemAt(cells[0].x, cells[0].y) != null)
                return;
            if (!inventory.AddItem(box, box.m_stack, cells[0].x, cells[0].y))
                return;
            grave.RemoveItem(box);
            LayoutApply.Apply(InventoryState.Player, dropOverflow: true);
        }

        /// <summary>The box in the grave that was carried, while the player now carries none, or null.</summary>
        private static ItemDrop.ItemData Waiting(Inventory grave)
        {
            if (grave == null || !InventoryState.Active || InventoryState.CellsOf(SlotKind.Tackle).Count > 0 || !TackleboxSettings.Active)
                return null;
            Vector2i cell = SlotCellInGrave(grave);
            foreach (ItemDrop.ItemData item in grave.GetAllItems())
            {
                if (item.m_gridPos == cell && TackleboxCatalog.Of(item) != null)
                    return item;
            }
            return null;
        }

        /// <summary>Where the Tacklebox slot was in the grave: the layout in use, pushed down by a waiting backpack's rows.</summary>
        private static Vector2i SlotCellInGrave(Inventory grave)
        {
            InventoryLayout layout = InventoryState.Layout;
            int index = layout.IndexOf(new Slot(SlotKind.Tacklebox, 1).Id);
            if (index < 0)
                return new Vector2i(-1, -1);
            Vector2i cell = layout.CellOf(index);
            return new Vector2i(cell.x, cell.y + BackpackGrave.WaitingRows(grave));
        }
    }
}
