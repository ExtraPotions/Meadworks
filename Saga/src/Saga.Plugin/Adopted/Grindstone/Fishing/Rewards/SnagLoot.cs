// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Rewards/SnagLoot.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Puts a snag's find into the angler's inventory, the way the game gives a landed fish's bonus item
    /// (FishingFloat.Catch): each item of the find, rolled from its min to its max, added in stacks of the item's stack
    /// size, and dropped at the angler's feet when the inventory has no room ("inventory full" top left, as the game says
    /// it). Item names are looked up as the Finds files' are (<see cref="FindPrefabs"/>: a typo is logged once and skipped).
    /// On the angler's client; a dropped stack is a networked object every client sees.
    /// </summary>
    public static class SnagLoot
    {
        /// <summary>Gives every item of the find; returns how many units came, 0 when none did.</summary>
        public static int Give(Player angler, FindEntry find)
        {
            int given = 0;
            foreach (FindItem item in find.Items)
            {
                ItemDrop prefab = FindPrefabs.Item(item.Prefab);
                if (prefab != null)
                    given += GiveItem(angler, prefab, Random.Range(item.Min, item.Max + 1));
            }
            return given;
        }

        private static int GiveItem(Player angler, ItemDrop prefab, int amount)
        {
            int maxStack = Mathf.Max(1, prefab.m_itemData.m_shared.m_maxStackSize);
            int given = 0;
            for (int drops = 0; amount > 0 && drops < FindSpawner.MaxDrops; drops++)
            {
                int stack = Mathf.Min(amount, maxStack);
                // Checked first: AddItem fills existing stacks unit by unit and keeps them even when it fails.
                if (angler.GetInventory().CanAddItem(prefab.gameObject, stack))
                    angler.GetInventory().AddItem(prefab.gameObject, stack);
                else
                    DropAtFeet(angler, prefab, stack);
                given += stack;
                amount -= stack;
            }
            return given;
        }

        private static void DropAtFeet(Player angler, ItemDrop prefab, int stack)
        {
            Vector3 at = angler.transform.position + Vector3.up;
            ItemDrop drop = Object.Instantiate(prefab.gameObject, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)).GetComponent<ItemDrop>();
            ItemDrop.OnCreateNew(drop);
            drop.SetStack(stack);
            angler.Message(MessageHud.MessageType.TopLeft, FishInfo.Localize("$inventory_full"));
        }
    }
}
