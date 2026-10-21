using System.Collections.Generic;
using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;

namespace Mantle.Backpacks
{
    /// <summary>
    /// A backpack and the grave. A grave made while a pack was worn holds the pack's slots as main cells and the slot
    /// rows that many rows further down; the player who wakes has neither, so the game's take all (which puts each item
    /// back at its old cell, <see cref="GravePatches"/>) would miss every slot. So before a take all from the player's own
    /// grave the pack that was worn (the one lying in the Backpack slot's cell of the grave's layout) goes back into its
    /// slot first and the layout grows to match; the grave's easy fit check (<see cref="GraveFit"/>) counts the slots
    /// and the carry weight it brings. A spare pack carried in the grid is not mistaken for it. With Backpack in Keep On Death
    /// the pack never reaches the grave.
    /// </summary>
    public static class BackpackGrave
    {
        /// <summary>
        /// Called by <see cref="GravePatches"/> before a take all from a grave (the take all's reply has made this client
        /// the grave's owner): the worn pack into its empty slot, then the layout.
        /// </summary>
        public static void WearFirst(Inventory inventory, Inventory grave)
        {
            ItemDrop.ItemData pack = WaitingPack(grave);
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(SlotKind.Backpack);
            if (pack == null || cells.Count == 0 || inventory.GetItemAt(cells[0].x, cells[0].y) != null)
                return;
            if (!inventory.AddItem(pack, pack.m_stack, cells[0].x, cells[0].y))
                return;
            grave.RemoveItem(pack);
            LayoutApply.Apply(InventoryState.Player, dropOverflow: true);
        }

        /// <summary>
        /// The rows the pack waiting in a grave adds, 0 when none waits: every slot cell of the grave lies that much lower
        /// than in the layout in use (<see cref="Tackle.TackleboxGrave"/> looks for its box there).
        /// </summary>
        public static int WaitingRows(Inventory grave) => RowsAdded(InventoryState.Layout, BackpackSettings.SlotsOf(WaitingPack(grave)));

        /// <summary>
        /// The main rows a pack of that many slots adds to the layout in use (which has none): its cells continue right
        /// after the base cells, so with the two hand cells of Inventory Rows 0 a 4 slot pack shares their row and adds
        /// none, and a 12 slot pack adds one (the layout's own rule, <see cref="LayoutBuilder.Wanted"/>).
        /// </summary>
        public static int RowsAdded(InventoryLayout layout, int slots)
        {
            if (layout == null || slots <= 0)
                return 0;
            return (layout.BaseCells + slots + layout.Width - 1) / layout.Width - layout.MainRows;
        }

        /// <summary>The pack in the grave that was worn, while the player now wears none, or null.</summary>
        public static ItemDrop.ItemData WaitingPack(Inventory grave)
        {
            if (grave == null || !InventoryState.Active || InventoryState.Layout.BackpackSlots > 0 || !BackpackSettings.Active)
                return null;
            foreach (ItemDrop.ItemData item in grave.GetAllItems())
            {
                if (BackpackCatalog.Of(item) != null && item.m_gridPos == SlotCellWearing(item))
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Where the Backpack slot was in the grave: the layout in use, pushed down by the rows this pack adds (its Deep
        /// Pockets included, as when the grave was made).
        /// </summary>
        private static Vector2i SlotCellWearing(ItemDrop.ItemData pack)
        {
            InventoryLayout layout = InventoryState.Layout;
            int index = layout.IndexOf(new Slot(SlotKind.Backpack, 1).Id);
            if (index < 0)
                return new Vector2i(-1, -1);
            int rows = RowsAdded(layout, BackpackSettings.SlotsOf(pack));
            Vector2i cell = layout.CellOf(index);
            return new Vector2i(cell.x, cell.y + rows);
        }
    }
}
