using System;
using Burrow.Core;

namespace Burrow.Stacks
{
    /// <summary>
    /// The items of every loaded container. Each of them carries its own copy of the shared data (the game
    /// instantiates the item prefab when an inventory loads), so it has to be written separately from the prefab.
    /// The ItemCopies library reaches the world drops, the local player's inventory, the eaten foods and the open
    /// container; the other tracked containers are visited here.
    /// </summary>
    public static class LiveItems
    {
        public static void ForEachInContainers(Action<ItemDrop.ItemData> action)
        {
            foreach (Container container in ContainerScan.All())
            {
                if (container != null)
                    Visit(container.GetInventory(), action);
            }
        }

        /// <summary>Applies the action to every item of the inventory and refreshes its cached total weight.</summary>
        private static void Visit(Inventory inventory, Action<ItemDrop.ItemData> action)
        {
            if (inventory == null)
                return;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item != null && item.m_shared != null)
                    action(item);
            }
            inventory.UpdateTotalWeight();
        }
    }
}
