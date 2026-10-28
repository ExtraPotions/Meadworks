// Barter - ExtraPotions Meadworks. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Barter.Runtime
{
    internal enum ExchangeResult { Completed, RolledBack, Unresolved, InventoryHeld }

    /// <summary>A synchronous exchange. Rollback reverses owned stack deltas, never an inventory snapshot.</summary>
    internal static class SafeTradeInventory
    {
        private static readonly Dictionary<Inventory, List<Delta>> unresolved = new Dictionary<Inventory, List<Delta>>();
        private static readonly HashSet<Inventory> recovering = new HashSet<Inventory>();
        private const int RecoveryLimit = 64;
        private static PendingAdd adding;

        internal static bool IsBlocked(Inventory inventory)
        {
            if (inventory == null) return false;
            Recover(inventory);
            return unresolved.ContainsKey(inventory) || unresolved.Count >= RecoveryLimit;
        }

        private sealed class PendingAdd
        {
            internal Inventory Inventory;
            internal ItemDrop.ItemData Template, Item;
            internal Vector2i Position;
            internal int Amount;
        }

        /// <summary>Called by the first Inventory.Changed prefix, before native change callbacks can move the clone.</summary>
        internal static void ObserveBeforeChanged(Inventory inventory)
        {
            var pending = adding;
            if (pending == null || pending.Inventory != inventory || pending.Item != null) return;
            var item = inventory.GetItemAt(pending.Position.x, pending.Position.y);
            if (item == null || item.m_shared != pending.Template.m_shared || item.m_quality != pending.Template.m_quality ||
                item.m_dropPrefab != pending.Template.m_dropPrefab) return;
            pending.Item = item;
            pending.Amount = item.m_stack;
        }

        // Keep removed objects when a synchronous observer prevents compensation; no automatic manufacture or refund.
        internal static IReadOnlyList<Take> RecoveryItems(Inventory inventory) =>
            unresolved.TryGetValue(inventory, out var pending)
                ? pending.Where(delta => !delta.Added && delta.Amount > 0).Select(delta => new Take(delta.Item, delta.Amount)).ToArray()
                : Array.Empty<Take>();

        internal static bool Recover(Inventory inventory)
        {
            if (inventory == null || !unresolved.TryGetValue(inventory, out var deltas)) return true;
            if (!recovering.Add(inventory)) return false;
            try
            {
                if (Restored(deltas) || Rollback(inventory, deltas) == ExchangeResult.RolledBack)
                {
                    unresolved.Remove(inventory);
                    return true;
                }
                return false;
            }
            finally { recovering.Remove(inventory); }
        }

        internal readonly struct Take
        {
            internal Take(ItemDrop.ItemData item, int amount) { Item = item; Amount = amount; }
            internal ItemDrop.ItemData Item { get; }
            internal int Amount { get; }
        }

        private sealed class Delta
        {
            internal ItemDrop.ItemData Item;
            internal int Amount;
            internal Vector2i Position;
            internal bool Added;
            internal bool Repaired;
            internal float BeforeDurability, AfterDurability;
        }

        internal static List<Take> Currency(Inventory inventory, ItemDrop currency, int amount)
        {
            var result = new List<Take>();
            if (inventory == null || currency == null || amount < 0) return result;
            foreach (var item in inventory.GetAllItems().ToArray())
            {
                // CountItems and the native payment path exclude currency from an older world level.
                if (item.m_dropPrefab == null || Utils.GetPrefabName(item.m_dropPrefab) != Utils.GetPrefabName(currency.gameObject) ||
                    item.m_shared.m_name != currency.m_itemData.m_shared.m_name || item.m_worldLevel < Game.m_worldLevel ||
                    item.m_equipped || OptionalTradeIntegrations.ProtectedItem(item, Player.m_localPlayer)) continue;
                int take = Math.Min(amount, item.m_stack);
                if (take > 0) result.Add(new Take(item, take));
                amount -= take;
                if (amount == 0) break;
            }
            return amount == 0 ? result : new List<Take>();
        }

        internal static ItemDrop.ItemData Template(ItemDrop prefab, int quality, bool cheated)
        {
            var item = prefab.m_itemData.Clone();
            item.m_dropPrefab = prefab.gameObject;
            item.m_quality = quality > 0 ? quality : item.m_quality;
            item.m_durability = item.GetMaxDurability();
            item.m_worldLevel = Game.m_worldLevel;
            item.m_cheated = cheated;
            item.m_equipped = false;
            item.m_stack = 1;
            return item;
        }

        /// <summary>Checks the empty slots Exchange needs after its exact source removals, without changing the inventory.</summary>
        internal static bool CanFitExchange(Inventory inventory, IReadOnlyList<Take> payment, ItemDrop.ItemData output, int amount)
        {
            if (inventory == null || payment == null || output == null || amount < 0) return false;
            long slots = inventory.GetEmptySlots();
            var distinct = new HashSet<ItemDrop.ItemData>();
            foreach (var take in payment)
            {
                if (take.Item == null || take.Amount <= 0 || !distinct.Add(take.Item) ||
                    !inventory.ContainsItem(take.Item) || take.Item.m_stack < take.Amount ||
                    take.Item.m_equipped || OptionalTradeIntegrations.ProtectedItem(take.Item, Player.m_localPlayer)) return false;
                if (take.Amount == take.Item.m_stack) slots++;
            }
            // Exchange uses separate explicit-slot stacks; existing currency stacks are never merged.
            long required = ((long)amount + Math.Max(1, output.m_shared.m_maxStackSize) - 1) /
                Math.Max(1, output.m_shared.m_maxStackSize);
            return slots >= required;
        }

        /// <summary>
        /// Uses boolean removal overloads so a Burrow hold cannot queue a payment for later. Explicit-slot adds
        /// preserve metadata and are bounded to one stack; a refused or partial write is measured, not trusted.
        /// </summary>
        internal static ExchangeResult Exchange(Inventory inventory, IReadOnlyList<Take> payment, ItemDrop.ItemData output, int amount)
            => ExchangeMany(inventory, payment, output == null ? null : new[] { new Take(output, amount) });

        internal static ExchangeResult Repair(Inventory inventory, IReadOnlyList<Take> payment, ItemDrop.ItemData item)
            => ExchangeMany(inventory, payment, Array.Empty<Take>(), item);

        internal static ExchangeResult ExchangeMany(Inventory inventory, IReadOnlyList<Take> payment, IReadOnlyList<Take> outputs, ItemDrop.ItemData repair = null)
        {
            if (inventory == null || outputs == null || outputs.Any(o => o.Item == null || o.Amount < 0) || payment == null) return ExchangeResult.RolledBack;
            if (IsBlocked(inventory)) return ExchangeResult.Unresolved;
            if (OptionalTradeIntegrations.InventoryHeld(inventory)) return ExchangeResult.InventoryHeld;
            if (repair != null && (!inventory.ContainsItem(repair) || repair.m_stack != 1 || payment.Any(t => ReferenceEquals(t.Item, repair)))) return ExchangeResult.RolledBack;
            float repairBefore = repair?.m_durability ?? 0f;
            var distinct = new HashSet<ItemDrop.ItemData>();
            foreach (var take in payment)
                if (take.Item == null || take.Amount <= 0 || !distinct.Add(take.Item) ||
                    !inventory.ContainsItem(take.Item) || take.Item.m_stack < take.Amount ||
                    take.Item.m_equipped || OptionalTradeIntegrations.ProtectedItem(take.Item, Player.m_localPlayer))
                    return ExchangeResult.RolledBack;

            var deltas = new List<Delta>();
            bool unobservedWrite = false;
            try
            {
                foreach (var take in payment)
                {
                    if (take.Item.m_equipped || OptionalTradeIntegrations.ProtectedItem(take.Item, Player.m_localPlayer))
                        return Rollback(inventory, deltas);
                    int before = take.Item.m_stack;
                    var delta = new Delta { Item = take.Item, Position = take.Item.m_gridPos };
                    try { inventory.RemoveItem(take.Item, take.Amount); }
                    finally
                    {
                        delta.Amount = Math.Max(0, before - (inventory.ContainsItem(take.Item) ? take.Item.m_stack : 0));
                        if (delta.Amount > 0) deltas.Add(delta);
                    }
                    if (delta.Amount != take.Amount) return Refused(inventory, deltas);
                }

                foreach (var delivery in outputs)
                for (int left = delivery.Amount; left > 0;)
                {
                    var output = delivery.Item;
                    var position = inventory.FindEmptySlot(false);
                    if (position.x < 0) return Rollback(inventory, deltas);
                    int count = Math.Min(left, Math.Max(1, output.m_shared.m_maxStackSize));
                    var part = output.Clone();
                    part.m_stack = count;
                    int arrived = 0;
                    var previous = adding;
                    var pending = new PendingAdd { Inventory = inventory, Position = position, Template = part };
                    adding = pending;
                    bool accepted = false, returned = false;
                    try { accepted = inventory.AddItem(part, count, position.x, position.y); returned = true; }
                    finally
                    {
                        adding = previous;
                        if (pending.Item != null)
                        {
                            int owned = Math.Min(count, pending.Amount);
                            arrived = inventory.ContainsItem(pending.Item) ? Math.Min(owned, pending.Item.m_stack) : 0;
                            deltas.Add(new Delta { Item = pending.Item, Amount = owned, Added = true });
                        }
                        else if (accepted || !returned || inventory.GetItemAt(position.x, position.y) != null) unobservedWrite = true;
                    }
                    // Without the observation hook we cannot attribute a replacement in the destination slot.
                    if (unobservedWrite)
                        return KeepUnresolved(inventory, deltas);
                    if (arrived != count) return Refused(inventory, deltas);
                    left -= count;
                }
                if (repair != null)
                {
                    if (!inventory.ContainsItem(repair) || repair.m_durability != repairBefore) return Rollback(inventory, deltas);
                    var delta = new Delta { Item = repair, Repaired = true, BeforeDurability = repair.m_durability, AfterDurability = repair.GetMaxDurability() };
                    deltas.Add(delta);
                    repair.m_durability = delta.AfterDurability;
                    inventory.Changed();
                    if (!inventory.ContainsItem(repair) || repair.m_durability != delta.AfterDurability) return KeepUnresolved(inventory, deltas);
                }
                return ExchangeResult.Completed;
            }
            catch (Exception)
            {
                return unobservedWrite ? KeepUnresolved(inventory, deltas) : Rollback(inventory, deltas);
            }
        }

        private static ExchangeResult Rollback(Inventory inventory, List<Delta> deltas)
        {
            // Normal writes respect holds. Compensation is an immediate inverse of this call's writes: sending
            // it through AddItem could merge away custom data, or be refused after the original removal worked.
            // Never yield or run this after another trade. No unrelated stack or metadata is restored.
            try
            {
                foreach (var delta in deltas.Where(d => d.Repaired).Reverse())
                {
                    if (!inventory.ContainsItem(delta.Item) || delta.Item.m_durability != delta.AfterDurability) return KeepUnresolved(inventory, deltas);
                    delta.Item.m_durability = delta.BeforeDurability;
                    delta.Repaired = false;
                }
                foreach (var delta in deltas.Where(d => d.Added).Reverse())
                {
                    if (delta.Amount == 0) continue;
                    if (!inventory.ContainsItem(delta.Item) || delta.Item.m_stack < delta.Amount)
                        return KeepUnresolved(inventory, deltas);
                    delta.Item.m_stack -= delta.Amount;
                    if (delta.Item.m_stack == 0) inventory.m_inventory.Remove(delta.Item);
                    delta.Amount = 0;
                }
                foreach (var delta in deltas.Where(d => !d.Added).Reverse())
                {
                    if (delta.Amount == 0) continue;
                    if (inventory.ContainsItem(delta.Item)) delta.Item.m_stack += delta.Amount;
                    else
                    {
                        var position = inventory.GetItemAt(delta.Position.x, delta.Position.y) == null
                            ? delta.Position : inventory.FindEmptySlot(false);
                        if (position.x < 0) return KeepUnresolved(inventory, deltas);
                        delta.Item.m_stack = delta.Amount;
                        delta.Item.m_gridPos = position;
                        inventory.m_inventory.Add(delta.Item);
                    }
                    delta.Amount = 0;
                }
                inventory.Changed();
                return ExchangeResult.RolledBack;
            }
            catch (Exception) { return KeepUnresolved(inventory, deltas); }
        }

        private static ExchangeResult Refused(Inventory inventory, List<Delta> deltas)
        {
            bool held = OptionalTradeIntegrations.InventoryHeld(inventory);
            ExchangeResult result = Rollback(inventory, deltas);
            return held && result == ExchangeResult.RolledBack ? ExchangeResult.InventoryHeld : result;
        }

        private static ExchangeResult KeepUnresolved(Inventory inventory, List<Delta> deltas)
        {
            unresolved[inventory] = deltas;
            return ExchangeResult.Unresolved;
        }

        private static bool Restored(IEnumerable<Delta> deltas) =>
            deltas.All(delta => !delta.Repaired && delta.Amount == 0);
    }
}
