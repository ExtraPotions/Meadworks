using System;
using System.Collections.Generic;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Reach
{
    /// <summary>
    /// Moves items from the reachable containers into the player's inventory (the Pull modifier and the unit a
    /// station borrows). Only as much as fits is moved; a stack is removed from its container first and added to
    /// the inventory with the game's <c>AddItem</c>, and whatever that did not place goes straight back into the
    /// container. A remainder the container also refuses drops at the player's feet, so an item is never in two
    /// places and never nowhere.
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

        /// <summary>Takes from the source stack and adds a copy to the target. What the target did not place returns to the source; what the source also refuses drops at the player's feet. Returns how many landed in the target. <paramref name="keptOut"/> is how many did not return to the source.</summary>
        private static int Transfer(Inventory source, Inventory target, ItemDrop.ItemData item, int take, out int keptOut, out bool removed)
        {
            keptOut = 0;
            removed = false;
            ItemDrop.ItemData moving = item.Clone();
            moving.m_stack = take;
            moving.m_equipped = false;
            if (!source.RemoveItem(item, take))
                return 0;
            removed = true;
            if (target.AddItem(moving))
            {
                keptOut = take;
                return take;
            }
            int leftover = moving.m_stack;
            int unplaced = leftover;
            if (leftover <= 0 || source.AddItem(moving))
                unplaced = 0;
            else
                unplaced = moving.m_stack;
            if (unplaced > 0)
                DropAtFeet(moving);
            keptOut = take - (leftover - unplaced);
            return take - leftover;
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
            ItemDrop drop = ItemDrop.DropItem(item, amount, at.position + at.forward + at.up, at.rotation);
            if (drop != null)
                drop.OnPlayerDrop();
            Plugin.Log.LogError($"Burrow: {amount} of {item.m_shared.m_name} fit neither inventory and were dropped at the player's feet");
        }

        /// <summary>How many of the item the inventory can still take: free stack space plus empty slots.</summary>
        public static int Fits(Inventory inventory, ItemDrop.ItemData item)
        {
            int perSlot = Math.Max(1, item.m_shared.m_maxStackSize);
            return inventory.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel) + inventory.GetEmptySlots() * perSlot;
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
                if (!inventory.RemoveItem(item, 1))
                    return false;
                Inventory chest = container.GetInventory();
                int before = chest.CountItems(unit.m_shared.m_name, unit.m_quality, false);
                if (!chest.AddItem(unit))
                {
                    if (chest.CountItems(unit.m_shared.m_name, unit.m_quality, false) != before)
                        ContainerScan.Save(container);
                    if (unit.m_stack > 0 && !inventory.AddItem(unit) && unit.m_stack > 0)
                        DropAtFeet(unit);
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
    }
}
