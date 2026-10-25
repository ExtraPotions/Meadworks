using System.Collections.Generic;
using Mantle.Slots;

namespace Mantle.Layout
{
    /// <summary>
    /// The main grid's free cells, the game's rules limited to it: the game looks for room over the whole inventory,
    /// which now includes the slot rows, so a picked up stone would land in the helmet slot. Stacking onto a partial
    /// stack still reaches the slots (arrows join the ammo slot's arrows), as the game's own search does.
    /// </summary>
    public static class MainCells
    {
        /// <summary>The first free main cell, top row first or bottom row first as the game asks; (-1, -1) when full.</summary>
        public static Vector2i FindEmpty(Inventory inventory, InventoryLayout layout, bool topFirst)
        {
            for (int i = 0; i < layout.MainRows; i++)
            {
                int y = (i + 1) % layout.MainRows;
                for (int column = 0; column < layout.Width; column++)
                {
                    int x = y == 0 && layout.Width > 8 ? (column + 8) % layout.Width : column;
                    if (layout.IsMain(new Vector2i(x, y)) && inventory.GetItemAt(x, y) == null)
                        return new Vector2i(x, y);
                }
            }
            return new Vector2i(-1, -1);
        }

        /// <summary>
        /// The first free main cell bottom row first that is none of a worn backpack's own cells: where the pack itself goes
        /// when it comes off, since its cells go with it; (-1, -1) when there is none.
        /// </summary>
        public static Vector2i FindEmptyOffPack(Inventory inventory, InventoryLayout layout)
        {
            for (int row = 0; row < layout.MainRows; row++)
            {
                int y = (row + 1) % layout.MainRows;
                for (int column = 0; column < layout.Width; column++)
                {
                    int x = y == 0 && layout.Width > 8 ? (column + 8) % layout.Width : column;
                    Vector2i cell = new Vector2i(x, y);
                    if (layout.IsMain(cell) && !layout.IsPackCell(cell) && inventory.GetItemAt(x, y) == null)
                        return cell;
                }
            }
            return new Vector2i(-1, -1);
        }

        public static int CountEmpty(Inventory inventory, InventoryLayout layout)
        {
            int empty = 0;
            for (int y = 0; y < layout.MainRows; y++)
                for (int x = 0; x < layout.Width; x++)
                    if (layout.IsMain(new Vector2i(x, y)) && inventory.GetItemAt(x, y) == null) empty++;
            return empty;
        }

        public static List<Vector2i> LegalExtraCells(InventoryLayout layout, ItemDrop.ItemData item)
        {
            var cells = new List<Vector2i>();
            if (item == null)
                return cells;   // an item-less room question (HaveEmptySlot, GetEmptySlots) means any ordinary item: slot cells refuse those
            for (int i = 0; i < layout.Slots.Count; i++)
                if (layout.Slots[i].Kind != SlotKind.Retired && SlotRules.Accepts(layout.Slots[i], item)) cells.Add(layout.CellOf(i));
            return cells;
        }

        public static int CountEmptyFor(Inventory inventory, InventoryLayout layout, ItemDrop.ItemData item)
        {
            int count = CountEmpty(inventory, layout);
            foreach (var cell in LegalExtraCells(layout, item))
                if (inventory.GetItemAt(cell.x, cell.y) == null) count++;
            return count;
        }

        /// <summary>Calculates remaining stack room for an item matching name, quality, and world level.</summary>
        public static int FreeStackSpace(Inventory inventory, ItemDrop.ItemData item)
        {
            if (inventory == null || item?.m_shared == null)
                return 0;
            int room = 0;
            var items = inventory.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                var existing = items[i];
                if (existing != null && existing.m_shared != null &&
                    existing.m_shared.m_name == item.m_shared.m_name &&
                    existing.m_quality == item.m_quality &&
                    existing.m_worldLevel == item.m_worldLevel &&
                    existing.m_stack < existing.m_shared.m_maxStackSize)
                {
                    room += existing.m_shared.m_maxStackSize - existing.m_stack;
                }
            }
            return room;
        }

        /// <summary>The game's CanAddItem over the main grid: room on partial stacks plus whole free cells.</summary>
        public static bool CanAdd(Inventory inventory, InventoryLayout layout, ItemDrop.ItemData item, int stack)
        {
            if (item == null)
                return false;
            if (stack <= 0)
                stack = item.m_stack;
            int room = FreeStackSpace(inventory, item);
            return room + CountEmptyFor(inventory, layout, item) * item.m_shared.m_maxStackSize >= stack;
        }

    }
}
