// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Crafting/CraftRecord.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// What a craft at a kitchen crafting station used up. InventoryGui.DoCrafting pays after the crafted item is
    /// added: Player.ConsumeResources calls Inventory.RemoveItem(name, amount, -1) per requirement, and a
    /// single-ingredient recipe calls it directly with its stack's quality. While <see cref="Recording"/> is on, the
    /// patches below note every lot those removals take: by name, the stacks the game is about to take from, in its own
    /// order; by reference with Inventory.RemoveItem(item, amount), which is how OpenKeep pays a shortfall from nearby
    /// chests, the stack itself. Any inventory is noted, so a mod that pays from a chest the game's way is noted too.
    /// The lots are the pool the ingredient-save perk draws from.
    /// </summary>
    public static class CraftRecord
    {
        /// <summary>Units taken from one stack: a copy of the stack (quality, world level, crafter) and how many.</summary>
        public sealed class Lot
        {
            public ItemDrop.ItemData Item;
            public int Count;
        }

        private static readonly List<Lot> lots = new List<Lot>();

        public static bool Recording { get; private set; }

        public static List<Lot> Lots => lots;

        public static void Start()
        {
            lots.Clear();
            Recording = true;
        }

        public static void Stop() => Recording = false;

        public static void Clear()
        {
            Recording = false;
            lots.Clear();
        }

        /// <summary>Notes that <paramref name="count"/> units of this stack were used up, while recording.</summary>
        public static void Note(ItemDrop.ItemData stack, int count)
        {
            if (!Recording || stack == null || count <= 0)
                return;
            ItemDrop.ItemData copy = stack.Clone();
            copy.m_stack = count;
            copy.m_equipped = false;
            lots.Add(new Lot { Item = copy, Count = count });
        }

        /// <summary>Notes what the game's RemoveItem by name will take, walking the stacks as it does.</summary>
        private static void NoteByName(Inventory inventory, string name, int amount, int quality, bool worldLevelBased)
        {
            foreach (ItemDrop.ItemData item in inventory.m_inventory)
            {
                if (amount <= 0)
                    break;
                if (item.m_shared.m_name != name || (quality >= 0 && item.m_quality != quality)
                    || (worldLevelBased && item.m_worldLevel < Game.m_worldLevel))
                    continue;
                int take = Mathf.Min(item.m_stack, amount);
                Note(item, take);
                amount -= take;
            }
        }

        // Low priority: runs after other mods' prefixes, so OpenKeep first takes the shortfall from chests.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
        private static class NameTaken
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.Low)]
            private static void Prefix(Inventory __instance, string name, int amount, int itemQuality, bool worldLevelBased, bool __runOriginal)
            {
                if (Recording && __runOriginal)
                    NoteByName(__instance, name, amount, itemQuality, worldLevelBased);
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(ItemDrop.ItemData), typeof(int) })]
        private static class StackTaken
        {
            [HarmonyPrefix]
            private static void Prefix(Inventory __instance, ItemDrop.ItemData item, int amount)
            {
                if (Recording && item != null && __instance.ContainsItem(item))
                    Note(item, Mathf.Min(item.m_stack, amount));
            }
        }
    }
}
