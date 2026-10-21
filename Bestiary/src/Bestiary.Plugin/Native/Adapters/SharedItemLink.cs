// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Catalog;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native
{
    /// <summary>
    /// Inventory loads and crafts instantiate the item prefab, and Unity's Instantiate gives every item its own copy of
    /// the shared data, so a settings change applied to the prefab never reached items already made. Linking each
    /// Bestiary item back to its prefab's shared data when it wakes makes damage, reload and recipe-side numbers live
    /// for every existing item, the way Inscribe links its stones. Other mods' items are left alone.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
    internal static class SharedItemLink
    {
        private static readonly HashSet<string> Owned = new HashSet<string>(ItemCatalog.All.Select(i => i.Prefab), StringComparer.Ordinal);

        private static void Postfix(ItemDrop __instance)
        {
            var item = __instance.m_itemData;
            var prefab = item?.m_dropPrefab;
            if (prefab == null || prefab == __instance.gameObject || !Owned.Contains(prefab.name)) return;
            var shared = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            if (shared != null) item!.m_shared = shared;
        }
    }
}
