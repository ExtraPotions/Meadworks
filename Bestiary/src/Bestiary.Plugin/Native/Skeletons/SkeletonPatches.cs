// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Skeletons
{
    /// <summary>
    /// A wild skeleton's spawn may come as another skeleton (<see cref="SkeletonDraw.Wild"/>): the spawn is made again
    /// with its data, through the same method, so levels, hunting and despawning work as for the skeleton. When another
    /// mod's prefix has already skipped the spawn, this one lets it be. A failure is reported and the skeleton spawns as
    /// usual.
    /// </summary>
    [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
    public static class SkeletonWildSpawnPatch
    {
        private static bool Prefix(SpawnSystem __instance, SpawnSystem.SpawnData critter, Vector3 spawnPoint, bool eventSpawner, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return false;
            }
            SpawnSystem.SpawnData? instead = SafeCall.Run("SpawnSystem.Spawn skeleton draw", static data => SkeletonDraw.Wild(data), critter, null);
            if (instead == null)
            {
                return true;
            }
            __instance.Spawn(instead, spawnPoint, eventSpawner);
            return false;
        }
    }

    /// <summary>
    /// A fixed spawner (burial chambers, graves, ruins, cabins) may put another skeleton where its skeleton would stand
    /// (<see cref="SkeletonDraw.Instead"/>): its creature is swapped for this one spawn and put back afterwards,
    /// whatever happens, so the spawner itself never changes and keeps tracking what it spawned.
    /// </summary>
    [HarmonyPatch(typeof(CreatureSpawner), "Spawn")]
    public static class SkeletonCryptSpawnPatch
    {
        private static void Prefix(CreatureSpawner __instance, out GameObject? __state)
        {
            GameObject? swap = SafeCall.Run("CreatureSpawner.Spawn skeleton draw",
                static prefab => SkeletonDraw.Instead(prefab), __instance.m_creaturePrefab, null);
            __state = swap == null ? null : __instance.m_creaturePrefab;
            if (swap != null)
            {
                __instance.m_creaturePrefab = swap;
            }
        }

        private static void Finalizer(CreatureSpawner __instance, GameObject? __state)
        {
            if (__state != null)
            {
                __instance.m_creaturePrefab = __state;
            }
        }
    }

    /// <summary>A bone pile's pick may be another skeleton instead (<see cref="SkeletonDraw.BonePile"/>).</summary>
    [HarmonyPatch(typeof(SpawnArea), "SelectWeightedPrefab")]
    public static class SkeletonBonePileSpawnPatch
    {
        private static void Postfix(ref SpawnArea.SpawnData __result)
        {
            __result = SafeCall.Run("SpawnArea.SelectWeightedPrefab skeleton draw",
                static picked => SkeletonDraw.BonePile(picked), __result, __result);
        }
    }
}
