using System;
using System.Collections.Generic;

namespace Mantle.Slots
{
    internal static class InventoryTransfer
    {
        internal static bool Move(Inventory source, ItemDrop.ItemData item, int amount, Inventory destination,
            ItemDrop.ItemData copy, Func<ItemDrop.ItemData, bool> add)
        {
            if (amount <= 0 || !source.ContainsItem(item))
                return false;
            ItemDrop.ItemData moving = copy.Clone();
            moving.m_stack = amount;
            AddedDelta destinationDelta = new AddedDelta(destination, moving, amount);
            try { add(moving); }
            catch (Exception) { }

            // The add's own remainder says how many units arrived; the source always loses exactly that many. The
            // per-stack capture only locates them for a take-back, so a capture mismatch never skips the debit
            // (skipping it would leave the units in both inventories).
            if (!destinationDelta.Capture(out int added, out int expected))
            {
                Warn();
                // Whichever count is larger is debited: under-debiting would leave units in both inventories.
                added = Math.Max(added, expected);
            }
            if (added <= 0)
                return false;

            int before = source.ContainsItem(item) ? item.m_stack : 0;
            try { source.RemoveItem(item, added); }
            catch (Exception) { }
            if (!SourceDebit(source, item, before, added, out int debited))
            {
                Warn();
                if (!destinationDelta.TakeBack(added))
                    Warn();
                return false;
            }
            if (debited < added && !destinationDelta.TakeBack(added - debited))
                Warn();
            return debited > 0;
        }

        private static bool SourceDebit(Inventory source, ItemDrop.ItemData item, int before, int limit, out int debited)
        {
            debited = 0;
            if (before <= 0)
                return true;
            if (!source.ContainsItem(item))
            {
                debited = before;
                return debited <= limit;
            }
            if (item.m_stack < 0 || item.m_stack > before)
                return false;
            debited = before - item.m_stack;
            return debited <= limit;
        }

        private static void Warn() =>
            Plugin.Log?.LogWarning("Mantle inventory transfer could not finish exact compensation; live inventories were not replaced");

        private sealed class AddedDelta
        {
            private readonly Inventory inventory;
            private readonly ItemDrop.ItemData moving;
            private readonly int limit;
            private readonly Dictionary<ItemDrop.ItemData, int> before = new Dictionary<ItemDrop.ItemData, int>();
            private readonly List<Entry> entries = new List<Entry>();

            internal AddedDelta(Inventory inventory, ItemDrop.ItemData moving, int limit)
            {
                this.inventory = inventory;
                this.moving = moving;
                this.limit = limit;
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                    before[item] = item.m_stack;
            }

            internal bool Capture(out int added, out int expected)
            {
                entries.Clear();
                added = 0;
                bool movingInserted = !before.ContainsKey(moving) && inventory.ContainsItem(moving);
                expected = limit - moving.m_stack + (movingInserted ? moving.m_stack : 0);
                if (expected < 0 || expected > limit)
                {
                    expected = Math.Max(0, Math.Min(limit, expected));
                    return false;
                }
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    int old = before.TryGetValue(item, out int count) ? count : 0;
                    int increase = item.m_stack - old;
                    if (increase <= 0 || !item.IsSameType(moving))
                        continue;
                    entries.Add(new Entry(item, old, increase));
                    added += increase;
                }
                return added == expected;
            }

            internal bool TakeBack(int amount)
            {
                if (amount <= 0)
                    return true;
                List<Removal> removals = new List<Removal>();
                for (int index = entries.Count - 1; index >= 0 && amount > 0; index--)
                {
                    Entry entry = entries[index];
                    int take = Math.Min(amount, entry.Added);
                    if (!inventory.ContainsItem(entry.Item) || entry.Item.m_stack < entry.Before + entry.Added)
                        return false;
                    removals.Add(new Removal(entry.Item, take));
                    amount -= take;
                }
                if (amount > 0)
                    return false;
                foreach (Removal removal in removals)
                {
                    int old = removal.Item.m_stack;
                    try { inventory.RemoveItem(removal.Item, removal.Amount); }
                    catch (Exception) { }
                    int current = inventory.ContainsItem(removal.Item) ? removal.Item.m_stack : 0;
                    int wanted = old - removal.Amount;
                    if (current == wanted)
                        continue;
                    // This is the transfer's captured addition, so a hook may not strand it by refusing compensation.
                    if (wanted <= 0)
                    {
                        inventory.GetAllItems().Remove(removal.Item);
                    }
                    else
                    {
                        removal.Item.m_stack = wanted;
                        if (!inventory.ContainsItem(removal.Item))
                            inventory.GetAllItems().Add(removal.Item);
                    }
                    try { inventory.Changed(); }
                    catch (Exception) { }
                    current = inventory.ContainsItem(removal.Item) ? removal.Item.m_stack : 0;
                    if (current != wanted)
                        return false;
                }
                return true;
            }

            private sealed class Entry
            {
                internal readonly ItemDrop.ItemData Item;
                internal readonly int Before;
                internal readonly int Added;
                internal Entry(ItemDrop.ItemData item, int before, int added) { Item = item; Before = before; Added = added; }
            }

            private sealed class Removal
            {
                internal readonly ItemDrop.ItemData Item;
                internal readonly int Amount;
                internal Removal(ItemDrop.ItemData item, int amount) { Item = item; Amount = amount; }
            }
        }
    }
}
