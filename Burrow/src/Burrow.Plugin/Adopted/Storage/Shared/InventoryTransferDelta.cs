using System;
using System.Collections.Generic;

namespace Burrow.Shared
{
    internal static class InventoryTransferDelta
    {
        internal static int Move(Inventory source, ItemDrop.ItemData item, int amount, Inventory destination,
            ItemDrop.ItemData moving, Action<ItemDrop.ItemData> add, string operation, out bool sourceChanged)
        {
            sourceChanged = false;
            int sourceBeforeTransfer = source.ContainsItem(item) ? item.m_stack : 0;
            AddedDelta destinationDelta = new AddedDelta(destination, moving, amount);
            try { add(moving); }
            catch (Exception) { }
            sourceChanged = !source.ContainsItem(item) || item.m_stack != sourceBeforeTransfer;

            // The add's own remainder says how many units arrived; the source always loses exactly that many. The
            // per-stack capture only locates them for a take-back, so a capture mismatch never skips the debit
            // (skipping it would leave the units in both inventories).
            if (!destinationDelta.Capture(out int added, out int expected))
            {
                Warn(operation);
                // Whichever count is larger is debited: under-debiting would leave units in both inventories.
                added = Math.Max(added, expected);
            }
            if (added <= 0)
                return 0;

            int before = source.ContainsItem(item) ? item.m_stack : 0;
            try { source.RemoveItem(item, added); }
            catch (Exception) { }
            sourceChanged = !source.ContainsItem(item) || item.m_stack != sourceBeforeTransfer;

            if (!SourceDebit(source, item, before, added, out int debited))
            {
                Warn(operation);
                if (!destinationDelta.TakeBack(added))
                    Warn(operation);
                return 0;
            }
            if (debited < added && !destinationDelta.TakeBack(added - debited))
                Warn(operation);
            return debited;
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

        // The game merges stacks on name, quality and world level only (durability, crafter or custom data may differ),
        // so the units an add placed are counted with the same rule, not with Burrow's exact-item comparison.
        private static bool Stacks(ItemDrop.ItemData item, ItemDrop.ItemData moving) =>
            item.m_shared != null && moving.m_shared != null && item.m_shared.m_name == moving.m_shared.m_name
            && item.m_quality == moving.m_quality && item.m_worldLevel == moving.m_worldLevel;

        private static void Warn(string operation) =>
            Plugin.Log.LogWarning("Burrow: " + operation + " could not finish exact compensation; live inventories were not replaced");

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
                    if (increase <= 0 || !Stacks(item, moving))
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
