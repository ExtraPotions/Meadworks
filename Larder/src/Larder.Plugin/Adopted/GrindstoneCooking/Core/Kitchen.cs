// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Core/Kitchen.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
// Changed by expDARE, 2026: kitchens count only while the Cooking module is active; Clear empties the tables on shutdown.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Which stations are kitchens and which items they make. The game marks its own kitchens: a CookingStation
    /// whose m_skill is Cooking (cooking stations, oven) and a CraftingStation whose m_craftingSkill is Cooking
    /// (cauldron, mead cauldron, prep table). Every fermenter is a kitchen. Kitchen items are what those make:
    /// cooking-station and fermenter outputs and kitchen recipe outputs, intermediates (mead bases, dough, unbaked
    /// pies) included; feasts (items that place a piece) and unstackable items are left out. A modded kitchen
    /// that sets the same fields is found the same way. Filled by <see cref="KitchenDiscovery"/>.
    /// </summary>
    public static class Kitchen
    {
        private static readonly HashSet<string> itemNames = new HashSet<string>();
        private static readonly Dictionary<string, float> values = new Dictionary<string, float>();

        public static bool IsKitchen(CookingStation station) => NativeCooking.Active && station != null && station.m_skill == CookLevel.Skill;

        public static bool IsKitchen(CraftingStation station) => NativeCooking.Active && station != null && station.m_craftingSkill == CookLevel.Skill;

        /// <summary>Whether a kitchen makes the item. Keyed by the shared name.</summary>
        public static bool IsKitchenItem(ItemDrop.ItemData item) => item?.m_shared != null && itemNames.Contains(item.m_shared.m_name);

        /// <summary>
        /// The dish's food value for experience: health + stamina + eitr, or for an intermediate the value of what it
        /// becomes on a station or in a fermenter. 0 when unknown.
        /// </summary>
        public static float FoodValue(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
                return 0f;
            return values.TryGetValue(item.m_shared.m_name, out float value) ? value : Value(item.m_shared);
        }

        /// <summary>The ItemDrop of an item prefab by name, or null.</summary>
        public static ItemDrop ItemPrefab(string prefabName)
        {
            GameObject prefab = ObjectDB.instance == null || string.IsNullOrEmpty(prefabName) ? null : ObjectDB.instance.GetItemPrefab(prefabName);
            return prefab == null ? null : prefab.GetComponent<ItemDrop>();
        }

        internal static float Value(ItemDrop.ItemData.SharedData shared) => shared.m_food + shared.m_foodStamina + shared.m_foodEitr;

        /// <summary>Adds a kitchen product. Feasts and unstackable items are refused.</summary>
        internal static void AddItem(ItemDrop drop)
        {
            if (drop == null || drop.m_itemData?.m_shared == null || drop.GetComponent<Piece>() != null || drop.GetComponent<Feast>() != null)
                return;
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            if (shared.m_maxStackSize <= 1)
                return;
            itemNames.Add(shared.m_name);
        }

        /// <summary>Records that <paramref name="from"/> becomes <paramref name="to"/>, for the food value of intermediates.</summary>
        internal static void AddConversion(ItemDrop from, ItemDrop to)
        {
            if (from?.m_itemData?.m_shared == null || to?.m_itemData?.m_shared == null)
                return;
            ItemDrop.ItemData.SharedData shared = from.m_itemData.m_shared;
            if (Value(shared) <= 0f && !values.ContainsKey(shared.m_name))
                values[shared.m_name] = Value(to.m_itemData.m_shared);
        }

        internal static int Count => itemNames.Count;

        internal static void Clear()
        {
            itemNames.Clear();
            values.Clear();
        }
    }
}
