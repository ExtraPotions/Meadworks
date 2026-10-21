// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// Registers the stone prefabs with the game (prefabs.md section 2, game notes Q18), on every peer: server, host,
    /// clients and the main menu, before any inventory loads or any ZDO arrives. Both databases fill their lookup with
    /// <c>Dictionary.Add</c> and throw on a duplicate, and the main-menu object database shares the prefab asset's item
    /// list, so every addition is checked by name first (idempotent).
    /// </summary>
    [HarmonyPatch]
    internal static class StoneRegistrationPatches
    {
        // Every peer. The main menu's first Awake runs on empty lists: nothing to clone from yet, nothing added.
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void ObjectDbAwake(ObjectDB __instance) => Register(__instance);

        // Every peer: the main menu's database copies the prefab asset's lists, then rebuilds its lookups.
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void ObjectDbCopy(ObjectDB __instance) => Register(__instance);

        // Every peer: appended before Awake runs, so the game's own loop registers them in its lookup.
        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        [HarmonyPrefix]
        private static void ZNetSceneAwake(ZNetScene __instance)
        {
            if (StonePrefabs.EnsureBuilt(__instance.m_prefabs, ObjectDB.instance?.m_items))
            {
                StonePrefabs.AddMissing(__instance.m_prefabs);
            }
        }

        // Every peer, for every item that wakes up (world drop, inventory load, craft): one dictionary lookup.
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
        [HarmonyPostfix]
        private static void ItemDropAwake(ItemDrop __instance) => StonePrefabs.LinkShared(__instance.m_itemData);

        private static void Register(ObjectDB db)
        {
            List<GameObject>? scene = ZNetScene.instance != null ? ZNetScene.instance.m_prefabs : null;
            if (StonePrefabs.EnsureBuilt(db.m_items, scene) && StonePrefabs.AddMissing(db.m_items))
            {
                db.UpdateRegisters();
            }
            StackableGearWarning.Scan(db);
        }
    }
}
