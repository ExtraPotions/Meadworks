// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: inventory writes that report what actually happened (playtest 2026-10-20 item safety).
using System;
using UnityEngine;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// Inventory writes that never assume they worked. Another mod can refuse a write: Burrow's inventory hold skips
    /// <c>AddItem</c> and <c>RemoveItem</c> and answers false, and inventory layout mods can make <c>AddItem</c> stop
    /// part-way. So a removal counts only once the item has left, and an add is measured by the count before and after
    /// (<c>AddItem</c> can fill partial stacks and still answer false). Whatever must not vanish is dropped instead.
    /// </summary>
    internal static class SafeInventory
    {
        /// <summary>What an add did: how many arrived in the inventory and how many were dropped on the ground.</summary>
        internal readonly struct Delivery
        {
            public Delivery(int arrived, int dropped, int missing)
            {
                Arrived = arrived;
                Dropped = dropped;
                Missing = missing;
            }

            public int Arrived { get; }
            public int Dropped { get; }

            /// <summary>Neither arrived nor dropped: nothing was taken from anywhere for these, so nothing is lost.</summary>
            public int Missing { get; }
        }

        /// <summary>Removes the whole stack; true only when it actually left the inventory.</summary>
        public static bool RemoveStack(Inventory inventory, ItemDrop.ItemData item)
        {
            if (!inventory.ContainsItem(item)) return false;
            try { inventory.RemoveItem(item); }
            catch { }
            return !inventory.ContainsItem(item);
        }

        /// <summary>Removes up to <paramref name="amount"/> from this stack; the count that actually left.</summary>
        public static int RemoveFrom(Inventory inventory, ItemDrop.ItemData item, int amount)
        {
            if (amount <= 0 || !inventory.ContainsItem(item)) return 0;
            int before = item.m_stack;
            try { inventory.RemoveItem(item, Math.Min(amount, before)); }
            catch { }
            return inventory.ContainsItem(item) ? Math.Max(0, before - item.m_stack) : before;
        }

        /// <summary>Runs <paramref name="remove"/> and answers how much <paramref name="count"/> went down.</summary>
        public static int Removed(Func<int> count, Action remove)
        {
            int before = count();
            try { remove(); }
            catch { }
            return Math.Max(0, before - count());
        }

        /// <summary>
        /// Adds this item (its whole stack); whatever the inventory does not take is dropped at <paramref name="dropAt"/>.
        /// A refused add leaves the item's stack as it was; a part-way add leaves the rest in it. The count dropped.
        /// </summary>
        public static int AddOrDrop(Inventory inventory, ItemDrop.ItemData item, Vector3 dropAt)
        {
            if (inventory.AddItem(item) || item.m_stack <= 0 || item.m_dropPrefab == null) return 0;
            int rest = item.m_stack;
            ItemDrop.DropItem(item, rest, dropAt, Quaternion.identity);
            return rest;
        }

        /// <summary>
        /// Adds <paramref name="count"/> of the prefab's item in full stacks. What did not arrive is dropped at
        /// <paramref name="dropAt"/>, unless nothing arrived and <paramref name="noneMeansRetry"/> is set: then nothing is
        /// dropped, so a caller that has taken nothing yet can stop and try again later.
        /// </summary>
        public static Delivery Deliver(Inventory inventory, GameObject? prefab, int count, Vector3? dropAt, bool noneMeansRetry)
        {
            ItemDrop? drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null || count <= 0) return new Delivery(0, 0, Math.Max(0, count));
            var template = drop.m_itemData;
            string name = template.m_shared.m_name;
            int max = Math.Max(1, template.m_shared.m_maxStackSize);
            int before = inventory.CountItems(name, -1, false);
            for (int left = count; left > 0; left -= max)
                if (!inventory.AddItem(prefab, Math.Min(max, left))) break;
            int arrived = Math.Min(count, Math.Max(0, inventory.CountItems(name, -1, false) - before));
            int missing = count - arrived;
            if (missing == 0 || (arrived == 0 && noneMeansRetry) || dropAt == null) return new Delivery(arrived, 0, missing);
            var rest = template.Clone();
            rest.m_dropPrefab = prefab;
            rest.m_worldLevel = (byte)Game.m_worldLevel;
            for (int left = missing; left > 0; left -= max)
            {
                rest.m_stack = Math.Min(max, left);
                ItemDrop.DropItem(rest, rest.m_stack, dropAt.Value, Quaternion.identity);
            }
            return new Delivery(arrived, missing, 0);
        }

        /// <summary>Where a refused item is dropped: just in front of the character, a meter up so it falls to the ground.</summary>
        public static Vector3 AtFeet(Component? who) =>
            who != null ? who.transform.position + who.transform.forward * 0.7f + Vector3.up : Vector3.zero;
    }
}
