// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Core/Stars.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// An egg's stars. The game keeps the laying hen's level in the egg's quality (a one-star hen lays quality 2 eggs,
    /// which hatch one-star chicks) but shows it nowhere, since eggs have a maximum quality of 1; the mod shows it as
    /// stars and keeps eggs of different levels in different stacks. Eggs join while Husbandry is on
    /// in GrindstoneSkills; Saga adds none.
    /// </summary>
    public static class Stars
    {
        public const int Max = 3;

        private static readonly HashSet<string> itemNames = new HashSet<string>();

        /// <summary>Whether the item shows stars. Keyed by the shared name, which is also what stacks compare.</summary>
        public static bool IsStarItem(ItemDrop.ItemData item) => item?.m_shared != null && itemNames.Contains(item.m_shared.m_name);

        /// <summary>Whether items with this shared name (m_shared.m_name, e.g. "$item_egg") show stars.</summary>
        public static bool IsStarName(string sharedName) => sharedName != null && itemNames.Contains(sharedName);

        /// <summary>The item's stars; 0 for anything that is not a star item.</summary>
        public static int Get(ItemDrop.ItemData item)
        {
            // Quality first: nearly every item has quality 1 (0 stars), which needs no lookup.
            if (item == null || item.m_quality <= 1 || !IsStarItem(item))
                return 0;
            return Mathf.Clamp(item.m_quality - 1, 0, Max);
        }

        /// <summary>Lets an item prefab show its quality as stars. Unstackable items are refused. Idempotent.</summary>
        public static void Add(ItemDrop drop)
        {
            ItemDrop.ItemData.SharedData shared = drop == null ? null : drop.m_itemData?.m_shared;
            if (shared != null && shared.m_maxStackSize > 1)
                itemNames.Add(shared.m_name);
        }

        internal static int Count => itemNames.Count;
    }
}
