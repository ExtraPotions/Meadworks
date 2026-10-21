// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// Puts spawned items into the local player's inventory, dropping what does not fit at the player's feet
    /// (console-commands.md section 3, the vanilla <c>spawn</c> behaviour). Spawned items get the world's world level,
    /// as vanilla spawns do, or stones would not stack with dropped ones (game notes pitfall 15). Runs on the caller's
    /// machine; the inventory is the player's own and dropped items are ordinary world objects.
    /// </summary>
    internal static class InventorySpawn
    {
        /// <summary>Adds <paramref name="count"/> of a stackable prefab; returns how many went into the inventory.</summary>
        public static int GiveStack(Player player, GameObject prefab, int count)
        {
            ItemDrop.ItemData template = prefab.GetComponent<ItemDrop>().m_itemData;
            Inventory inventory = player.GetInventory();
            int maxStack = Math.Max(1, template.m_shared.m_maxStackSize);
            int room = inventory.FindFreeStackSpace(template.m_shared.m_name, Game.m_worldLevel) + inventory.GetEmptySlots() * maxStack;
            int left = Math.Min(count, room);
            while (left > 0)
            {
                int chunk = Math.Min(left, maxStack);
                if (!inventory.AddItem(prefab, chunk))
                {
                    break;
                }
                left -= chunk;
            }
            int added = Math.Min(count, room) - left;
            DropStack(player, prefab, count - added);
            return added;
        }

        /// <summary>Adds one prepared item (custom data already written); true = inventory, false = dropped at the feet.</summary>
        public static bool GiveItem(Player player, ItemDrop.ItemData item)
        {
            Inventory inventory = player.GetInventory();
            if (inventory.HaveEmptySlot() && inventory.AddItem(item))
            {
                return true;
            }
            ItemDrop.DropItem(item, 1, FeetOf(player), Quaternion.identity);
            return false;
        }

        /// <summary>
        /// A fresh copy of a prefab's item data as a gear drop builds one (drops.md section 8): one piece, upgrade level 1,
        /// the world's world level, no crafter, no custom data.
        /// </summary>
        public static ItemDrop.ItemData NewItem(GameObject prefab)
        {
            ItemDrop.ItemData item = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = 1;
            item.m_quality = 1;
            item.m_worldLevel = Game.m_worldLevel;
            item.m_crafterID = 0L;
            item.m_crafterName = "";
            item.m_pickedUp = false;
            item.m_equipped = false;
            item.m_customData.Clear();
            return item;
        }

        private static void DropStack(Player player, GameObject prefab, int count)
        {
            ItemDrop.ItemData template = prefab.GetComponent<ItemDrop>().m_itemData;
            int maxStack = Math.Max(1, template.m_shared.m_maxStackSize);
            while (count > 0)
            {
                int chunk = Math.Min(count, maxStack);
                ItemDrop.ItemData item = NewItem(prefab);
                item.m_stack = chunk;
                ItemDrop.DropItem(item, chunk, FeetOf(player), Quaternion.identity);
                count -= chunk;
            }
        }

        private static Vector3 FeetOf(Player player) =>
            player.transform.position + Vector3.up + UnityEngine.Random.insideUnitSphere * 0.3f;
    }
}
