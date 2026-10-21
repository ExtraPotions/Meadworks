// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Yield/YieldCatalog.cs and YieldDiscovery.cs, with
// the scene walk of GrindstoneSkills/GrindstoneSkills/Core/PrefabIndex.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 9da28eb58adbe065be1e4baef779a448acd38e0f (GrindstoneSkills 0.16.0)
// Changed by expDARE, 2026: namespace; eggs are left to Larder; discovery also runs when Shepherd's Husbandry patches
// install late.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// What animal yield knows about items, discovered from the scene's prefabs rather than listed, so modded stations
    /// count the same way: the cooking inputs, the m_from of every CookingStation conversion (any station), which
    /// produce never gives. Keyed by the item's shared name (m_shared.m_name). Discovery runs last after
    /// ZNetScene.Awake and ObjectDB.Awake, and when the patches install; it forgets the cached produce tables. Adding
    /// never removes anything.
    /// </summary>
    public static class YieldCatalog
    {
        private static readonly HashSet<string> cookingInputs = new HashSet<string>(StringComparer.Ordinal);

        public static bool IsCookingInput(ItemDrop.ItemData.SharedData shared) => shared != null && cookingInputs.Contains(shared.m_name);

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        private static class SceneAwake
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix() => NativeHusbandry.Run("husbandry yield discovery", Discover);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        private static class DatabaseAwake
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix() => NativeHusbandry.Run("husbandry yield discovery", Discover);
        }

        /// <summary>Reads the scene's cooking stations; does nothing before ZNetScene exists.</summary>
        public static void Discover()
        {
            if (ZNetScene.instance == null)
                return;
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab != null)
                    AddStation(prefab.GetComponent<CookingStation>());
            }
            ProduceTables.Clear();
        }

        private static void AddStation(CookingStation station)
        {
            if (station == null || station.m_conversion == null)
                return;
            foreach (CookingStation.ItemConversion conversion in station.m_conversion)
            {
                string? name = SharedName(conversion?.m_from);
                if (name != null)
                    cookingInputs.Add(name);
            }
        }

        private static string? SharedName(ItemDrop? item) =>
            item != null && item.m_itemData?.m_shared != null && !string.IsNullOrEmpty(item.m_itemData.m_shared.m_name)
                ? item.m_itemData.m_shared.m_name : null;
    }
}
