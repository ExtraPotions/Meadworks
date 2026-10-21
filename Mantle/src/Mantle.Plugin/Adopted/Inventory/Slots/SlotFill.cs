using System;
using System.Collections.Generic;

namespace Mantle.Slots
{
    /// <summary>
    /// Putting an item into a group of slot cells the way the game's own add stacks: onto a stack of the same item in the
    /// cells (the game's <c>IsSameType</c> and the same quality) up to the stack size, in cell order, then into the first
    /// empty cell whole. Shared by the routes that send an item to its slots first (<see cref="AmmoRouting"/>, the
    /// tacklebox's <see cref="Tackle.TackleRouting"/>).
    /// </summary>
    public static class SlotFill
    {
        /// <summary>
        /// Moves what fits of an item into the cells; true when none is left outside them. The item is either not in the
        /// inventory yet (an add) or in a main cell (a take all): an empty cell takes it whole, as the same item.
        /// </summary>
        public static bool TakeIn(Inventory inventory, ItemDrop.ItemData item, IReadOnlyList<Vector2i> cells)
        {
            foreach (Vector2i cell in cells)
            {
                if (Stack(inventory, item, cell))
                    return true;
            }
            foreach (Vector2i cell in cells)
            {
                if (inventory.GetItemAt(cell.x, cell.y) == null)
                {
                    Place(inventory, item, cell);
                    return true;
                }
            }
            return false;
        }

        /// <summary>The room in the cells' empty ones, in items: what <c>CanAddItem</c> may count besides the main grid.</summary>
        public static int EmptyRoom(Inventory inventory, IReadOnlyList<Vector2i> cells, ItemDrop.ItemData item)
        {
            int empty = 0;
            foreach (Vector2i cell in cells)
                empty += inventory.GetItemAt(cell.x, cell.y) == null ? 1 : 0;
            return empty * item.m_shared.m_maxStackSize;
        }

        /// <summary>Onto the stack in a cell when it is the same item (quality, so stars, included); true when all of it went.</summary>
        private static bool Stack(Inventory inventory, ItemDrop.ItemData item, Vector2i cell)
        {
            ItemDrop.ItemData there = inventory.GetItemAt(cell.x, cell.y);
            if (there == null || there == item || !there.IsSameType(item) || there.m_quality != item.m_quality)
                return false;
            int moved = Math.Min(there.m_shared.m_maxStackSize - there.m_stack, item.m_stack);
            if (moved <= 0)
                return false;
            there.m_stack += moved;
            item.m_stack -= moved;
            inventory.Changed();
            return item.m_stack <= 0;
        }

        private static void Place(Inventory inventory, ItemDrop.ItemData item, Vector2i cell)
        {
            item.m_gridPos = cell;
            if (!inventory.ContainsItem(item))
                inventory.m_inventory.Add(item);
            inventory.Changed();
        }
    }
}
