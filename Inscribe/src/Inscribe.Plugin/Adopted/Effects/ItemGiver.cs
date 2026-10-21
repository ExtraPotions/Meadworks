// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Gives the local player items the effects hand out (a kept bait, a refunded craft, an extra crafted item): into
    /// the inventory stack by stack through the game's own <c>Inventory.AddItem</c>, and what does not fit is dropped
    /// at the player's feet through <c>ItemDrop.DropItem</c>, so nothing is ever lost to a full inventory. The local
    /// player's own client only (it owns its inventory; a dropped item is a normal networked item).
    /// </summary>
    internal static class ItemGiver
    {
        /// <summary>A fresh item of a prefab (stack 1) at a quality, as the game makes one for an inventory.</summary>
        public static ItemDrop.ItemData? FromPrefab(GameObject? prefab, int quality)
        {
            ItemDrop? drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return null;
            }
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = 1;
            item.m_quality = Mathf.Max(1, quality);
            item.m_worldLevel = (byte)Game.m_worldLevel;
            item.m_durability = item.GetMaxDurability();
            return item;
        }

        /// <summary><paramref name="count"/> copies of <paramref name="template"/>, split into stacks the item allows.</summary>
        public static void Give(Player player, ItemDrop.ItemData template, int count)
        {
            int max = Mathf.Max(1, template.m_shared.m_maxStackSize);
            while (count > 0)
            {
                int stack = Mathf.Min(count, max);
                count -= stack;
                ItemDrop.ItemData item = template.Clone();
                item.m_stack = stack;
                item.m_equipped = false;
                if (!player.GetInventory().AddItem(item))
                {
                    DropRest(player, item);
                }
            }
        }

        // A failed AddItem leaves the part it could not place in the item's stack.
        private static void DropRest(Player player, ItemDrop.ItemData rest)
        {
            if (rest.m_stack > 0 && rest.m_dropPrefab != null)
            {
                Transform at = player.transform;
                ItemDrop.DropItem(rest, rest.m_stack, at.position + at.forward + Vector3.up, Quaternion.identity);
            }
        }
    }
}
