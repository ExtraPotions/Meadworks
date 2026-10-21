using System;
using System.Collections.Generic;
using Burrow.Core;

namespace Burrow.Reach
{
    /// <summary>
    /// Moves items from the reachable containers into the player's inventory (the Pull modifier and the unit a
    /// station borrows). Only as much as fits is moved; a stack is removed from its container first and added to
    /// the inventory with the game's <c>AddItem</c>, and whatever that did not place goes straight back into the
    /// container, so an item is never in two places and never nowhere.
    /// </summary>
    public static class ReachPull
    {
        /// <summary>Moves up to amount items the caller wants, nearest container first. Returns the amount moved.</summary>
        public static int Move(Inventory inventory, Func<ItemDrop.ItemData, bool> wanted, int amount)
        {
            int moved = 0;
            foreach (Container container in ReachCount.Containers())
            {
                if (moved >= amount)
                    break;
                if (ReachCount.FirstIn(container, wanted) == null || !ContainerScan.Claim(container))
                    continue;
                moved += MoveFrom(container, inventory, wanted, amount - moved);
            }
            return moved;
        }

        private static int MoveFrom(Container container, Inventory inventory, Func<ItemDrop.ItemData, bool> wanted, int amount)
        {
            ContainerRule rule = ReachRules.RuleFor(container);
            Inventory source = container.GetInventory();
            int moved = 0;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(source.GetAllItems()))
            {
                if (moved >= amount)
                    break;
                if (!wanted(item) || !rule.Accepts(item))
                    continue;
                int take = Math.Min(Math.Min(item.m_stack, amount - moved), Fits(inventory, item));
                if (take <= 0)
                    continue;
                moved += Transfer(source, inventory, item, take);
            }
            if (moved > 0)
                ContainerScan.Save(container);
            return moved;
        }

        /// <summary>Takes from the source stack, adds a copy to the target; what the target did not place returns to the source.</summary>
        private static int Transfer(Inventory source, Inventory target, ItemDrop.ItemData item, int take)
        {
            ItemDrop.ItemData moving = item.Clone();
            moving.m_stack = take;
            moving.m_equipped = false;
            if (!source.RemoveItem(item, take))
                return 0;
            if (target.AddItem(moving))
                return take;
            int leftover = moving.m_stack;
            if (!source.AddItem(moving))
                Plugin.Log.LogError($"Burrow: {leftover} of {moving.m_shared.m_name} fit neither the inventory nor its container");
            return take - leftover;
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
                if (!inventory.RemoveItem(item, 1) || !container.GetInventory().AddItem(unit))
                    continue;
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
