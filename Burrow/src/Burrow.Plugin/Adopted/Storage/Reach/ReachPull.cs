using System;
using System.Collections.Generic;
using System.Linq;
using Burrow.Core;
using Burrow.Shared;
using UnityEngine;

namespace Burrow.Reach
{
    /// <summary>
    /// Moves items from the reachable containers into the player's inventory (the Pull modifier and the unit a
    /// station borrows). Only as much as fits is moved; a copy is added with the game's <c>AddItem</c>, then the
    /// source loses exactly what landed. A short source debit takes back only this transfer's destination units.
    /// </summary>
    public static class ReachPull
    {
        /// <summary>Moves up to amount items the caller wants, nearest container first. Returns the amount moved.</summary>
        public static int Move(Inventory inventory, Func<ItemDrop.ItemData, bool> wanted, int amount)
        {
            int moved = 0;
            int drawn = 0;
            foreach (Container container in ReachCount.Containers())
            {
                if (drawn >= amount)
                    break;
                if (ReachCount.FirstIn(container, wanted) == null || !ContainerScan.Claim(container))
                    continue;
                moved += MoveFrom(container, inventory, wanted, amount - drawn, out int took);
                drawn += took;
            }
            return moved;
        }

        private static int MoveFrom(Container container, Inventory inventory, Func<ItemDrop.ItemData, bool> wanted, int amount, out int drawn)
        {
            ContainerRule rule = ReachRules.RuleFor(container);
            Inventory source = container.GetInventory();
            int moved = 0;
            drawn = 0;
            bool changed = false;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(source.GetAllItems()))
            {
                if (drawn >= amount)
                    break;
                if (!wanted(item) || !rule.Accepts(item))
                    continue;
                int take = Math.Min(Math.Min(item.m_stack, amount - drawn), Fits(inventory, item));
                if (take <= 0)
                    continue;
                moved += Transfer(source, inventory, item, take, out int keptOut, out bool removed);
                drawn += keptOut;
                changed |= removed;
            }
            if (changed)
                ContainerScan.Save(container);
            return moved;
        }

        /// <summary>Adds a copy to the target, then debits exactly what landed from the source. Returns the verified source debit.</summary>
        private static int Transfer(Inventory source, Inventory target, ItemDrop.ItemData item, int take, out int keptOut, out bool removed)
        {
            ItemDrop.ItemData moving = item.Clone();
            moving.m_stack = take;
            moving.m_equipped = false;
            int moved = InventoryTransferDelta.Move(source, item, take, target, moving,
                added => target.AddItem(added), "Reach pull", out removed);
            keptOut = moved;
            return moved;
        }

        /// <summary>The remainder is in neither inventory. Drop it in front of the player, the way a shared chest hands over what no longer fits.</summary>
        static void DropAtFeet(ItemDrop.ItemData item)
        {
            if (item == null || item.m_stack <= 0)
                return;
            int amount = item.m_stack;
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Plugin.Log.LogError($"Burrow: {amount} of {item.m_shared.m_name} fit neither inventory and no player was present to drop them");
                return;
            }
            Transform at = player.transform;
            ItemDrop drop;
            try
            {
                drop = ItemDrop.DropItem(item, amount, at.position + at.forward + at.up, at.rotation);
                if (drop == null)
                    drop = ItemDrop.DropItem(item, amount, at.position + at.forward + at.up, at.rotation);
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("Burrow: return drop may already exist: " + error.GetType().Name);
                return;
            }
            if (drop == null)
            {
                Plugin.Log.LogError($"Burrow: {amount} of {item.m_shared.m_name} fit neither inventory and could not be dropped");
                return;
            }
            try { drop.OnPlayerDrop(); }
            catch (Exception error) { Plugin.Log.LogWarning("Burrow: return drop notice: " + error.GetType().Name); }
            Plugin.Log.LogError($"Burrow: {amount} of {item.m_shared.m_name} fit neither inventory and were dropped at the player's feet");
        }

        /// <summary>How many of the item the inventory can still take: free stack space plus empty slots.</summary>
        public static int Fits(Inventory inventory, ItemDrop.ItemData item)
        {
            if (inventory == null || item?.m_shared == null)
                return 0;
            int perSlot = Math.Max(1, item.m_shared.m_maxStackSize);
            int room = 0;
            foreach (ItemDrop.ItemData existing in inventory.GetAllItems())
            {
                if (existing != null && existing.m_shared != null
                    && existing.m_shared.m_name == item.m_shared.m_name
                    && existing.m_quality == item.m_quality
                    && existing.m_worldLevel == item.m_worldLevel
                    && existing.m_stack < existing.m_shared.m_maxStackSize)
                {
                    room += existing.m_shared.m_maxStackSize - existing.m_stack;
                }
            }
            return room + inventory.GetEmptySlots() * perSlot;
        }

        /// <summary>
        /// Puts one unit that the inventory holds back into a reachable container that accepts it: the nearest one that
        /// already holds the item, else the nearest that accepts it; a full one is passed over for the next (the user's
        /// rule for every automatic store, 2026-09-29).
        /// </summary>
        public static bool ReturnOne(Inventory inventory, string name, int quality)
        {
            ItemDrop.ItemData item = FindStack(inventory, name, quality);
            if (item == null)
                return false;
            ItemDrop.ItemData unit = item.Clone();
            unit.m_stack = 1;
            unit.m_equipped = false;
            foreach (Container container in HoldersFirst(ReachCount.Containers(), name))
            {
                if (!ReachRules.RuleFor(container).Accepts(unit) || !container.GetInventory().CanAddItem(unit, 1) || !ContainerScan.Claim(container))
                    continue;
                int playerBefore = StackCount(inventory, unit);
                try { inventory.RemoveItem(item, 1); }
                catch (Exception) { }
                int debited = playerBefore - StackCount(inventory, unit);
                if (debited != 1)
                    return false;
                Inventory chest = container.GetInventory();
                int before = StackCount(chest, unit);
                try { chest.AddItem(unit); }
                catch (Exception) { }
                int added = StackCount(chest, unit) - before;
                if (added != 1)
                {
                    if (added > 0)
                        ContainerScan.Save(container);
                    unit.m_stack = 1;
                    int restoreBefore = StackCount(inventory, unit);
                    try { inventory.AddItem(unit); }
                    catch (Exception) { }
                    int restored = StackCount(inventory, unit) - restoreBefore;
                    if (restored < 1)
                    {
                        unit.m_stack = 1 - System.Math.Max(0, restored);
                        DropAtFeet(unit);
                    }
                    return false;
                }
                ContainerScan.Save(container);
                return true;
            }
            return false;
        }

        /// <summary>The containers holding the item first, then the others, each in the order given (nearest first).</summary>
        private static List<Container> HoldersFirst(List<Container> containers, string name)
        {
            List<Container> ordered = new List<Container>(containers.Count);
            List<Container> others = new List<Container>();
            foreach (Container container in containers)
                (container.GetInventory().ContainsItemByName(name) ? ordered : others).Add(container);
            ordered.AddRange(others);
            return ordered;
        }

        private static ItemDrop.ItemData FindStack(Inventory inventory, string name, int quality)
        {
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (ReachCount.Matches(item, name, quality, false) && !item.m_equipped)
                    return item;
            }
            return null;
        }

        private static int StackCount(Inventory inventory, ItemDrop.ItemData sample) => inventory.GetAllItems()
            .Where(item => item.m_shared != null && sample.m_shared != null && item.m_shared.m_name == sample.m_shared.m_name
                && item.m_quality == sample.m_quality && item.m_worldLevel == sample.m_worldLevel)
            .Sum(item => item.m_stack);
    }
}
