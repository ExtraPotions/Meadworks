// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Finds/FindPrefabs.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The item prefabs Saga.Finds.yml and Saga.MineFinds.yml name, looked up when a find drops
    /// (the files parse before the item database exists, and on a dedicated server before any world is loaded).
    /// ObjectDB.GetItemPrefab looks a name up by its stable hash, so the exact spelling; a miss is retried against
    /// ObjectDB.m_items ignoring case. A name that matches no item is logged once (again after either file is reloaded)
    /// and skipped, so a typo loses one item, not the find.
    /// </summary>
    public static class FindPrefabs
    {
        private static readonly HashSet<string> warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The item's ItemDrop on its prefab; null for an unknown name or before the item database exists.</summary>
        public static ItemDrop Item(string prefabName)
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || string.IsNullOrEmpty(prefabName))
                return null;
            GameObject prefab = db.GetItemPrefab(prefabName);
            if (prefab == null)
                prefab = IgnoringCase(db, prefabName);
            ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (item == null && warned.Add(prefabName))
                NativeSaga.Log.LogWarning($"Finds: there is no item named '{prefabName}' (Saga.Finds.yml or Saga.MineFinds.yml); it is skipped.");
            return item;
        }

        /// <summary>Lets unknown names be warned about again after the file changed.</summary>
        public static void ResetWarnings() => warned.Clear();

        private static GameObject IgnoringCase(ObjectDB db, string prefabName)
        {
            foreach (GameObject prefab in db.m_items)
            {
                if (prefab != null && string.Equals(prefab.name, prefabName, StringComparison.OrdinalIgnoreCase))
                    return prefab;
            }
            return null;
        }
    }
}
