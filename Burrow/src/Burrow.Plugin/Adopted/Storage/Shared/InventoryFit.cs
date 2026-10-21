using System.Collections.Generic;

namespace Burrow.Shared
{
    /// <summary>
    /// A dry run of <c>Inventory.AddItem</c> over a snapshot of an inventory: stackable units first fill the
    /// partial stacks with the same complete saved identity, then fills empty slots. Every <see cref="Reserve"/> books what
    /// it returns, so a series of takes can be checked before any request is sent.
    /// </summary>
    public sealed class InventoryFit
    {
        private readonly Dictionary<string, int> space = new Dictionary<string, int>();
        private int freeSlots;

        public InventoryFit(Inventory inventory)
        {
            freeSlots = inventory.GetEmptySlots();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                int room = item.m_shared.m_maxStackSize - item.m_stack;
                string key = ItemCompatibility.Key(item);
                if (room > 0 && key != null)
                    space[key] = Room(key) + room;
            }
        }

        /// <summary>How many of <paramref name="amount"/> units of the item fit; those units are booked.</summary>
        public int Reserve(ItemDrop.ItemData item, int amount)
        {
            if (item == null || amount <= 0)
                return 0;
            int maxStack = item.m_shared.m_maxStackSize;
            string key = ItemCompatibility.Key(item);
            if (maxStack < 1 || key == null)
                return 0;
            int room = Room(key);
            int merged = room < amount ? room : amount;
            space[key] = room - merged;
            int rest = amount - merged;
            if (rest <= 0)
                return amount;
            while (rest > 0 && TakeSlot())
            {
                int added = rest < maxStack ? rest : maxStack;
                rest -= added;
                space[key] += maxStack - added;
            }
            return amount - rest;
        }

        private bool TakeSlot()
        {
            if (freeSlots <= 0)
                return false;
            freeSlots--;
            return true;
        }

        private int Room(string key) => space.TryGetValue(key, out int room) ? room : 0;
    }
}
