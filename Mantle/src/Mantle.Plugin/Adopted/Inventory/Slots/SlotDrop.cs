using Mantle.Core;

namespace Mantle.Slots
{
    /// <summary>
    /// Whether a drag and drop keeps every slot's rule, checked before the game changes anything: the dropped item must
    /// suit the slot it lands in, and in a swap (the game's <c>InventoryGrid.DropItem</c> swaps whole stacks of
    /// different items) the item it displaces must suit the slot the dropped item came from. The game's swap removes
    /// the dropped item before it places the other, so refusing halfway would lose an item; refusing first loses nothing.
    /// </summary>
    public static class SlotDrop
    {
        public static bool Allowed(Inventory target, Inventory from, ItemDrop.ItemData item, int amount, Vector2i pos)
        {
            if (target == null || item == null)
                return true;
            ItemDrop.ItemData at = target.GetItemAt(pos.x, pos.y);
            if (at == item)
                return true;
            if (!Takes(target, pos, item))
                return false;
            return at == null || !Swaps(at, item, amount) || Takes(from, item.m_gridPos, at);
        }

        /// <summary>
        /// Whether a cell of an inventory may hold the item: any cell of an inventory the module does not lay out, a
        /// main cell, a slot that takes it; never a cell after the last slot (the gamepad can reach those).
        /// </summary>
        public static bool Takes(Inventory inventory, Vector2i pos, ItemDrop.ItemData item)
        {
            if (!InventoryState.Manages(inventory) || InventoryState.Layout.IsMain(pos))
                return true;
            Slot slot = InventoryState.Layout.SlotAt(pos);
            return slot != null && SlotRules.Accepts(slot, item);
        }

        /// <summary>The game's own swap condition in InventoryGrid.DropItem.</summary>
        private static bool Swaps(ItemDrop.ItemData at, ItemDrop.ItemData item, int amount)
        {
            bool different = at.m_shared.m_name != item.m_shared.m_name
                || (item.m_shared.m_maxQuality > 1 && at.m_quality != item.m_quality)
                || at.m_shared.m_maxStackSize == 1;
            return different && item.m_stack == amount;
        }
    }
}
