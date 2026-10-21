// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Slinger
{
    /// <summary>
    /// A wild Greydwarf spawn may come as a slinger (<see cref="SlingerSpawns.Wild"/>): the spawn is made again with the
    /// slinger's data, through the same method, so levels, hunting and night despawn work as for the greydwarf. When
    /// another mod's prefix has already skipped the spawn, this one lets it be. A failure is reported and the greydwarf
    /// spawns as usual.
    /// </summary>
    [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
    public static class SlingerWildSpawnPatch
    {
        private static bool Prefix(SpawnSystem __instance, SpawnSystem.SpawnData critter, Vector3 spawnPoint, bool eventSpawner, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return false;
            }
            SpawnSystem.SpawnData? slinger = SafeCall.Run("SpawnSystem.Spawn slinger",
                static (data, point) => SlingerSpawns.Wild(data, point), critter, spawnPoint, null);
            if (slinger == null)
            {
                return true;
            }
            __instance.Spawn(slinger, spawnPoint, eventSpawner);
            return false;
        }
    }

    /// <summary>Each slingshot stone is aimed on its arc just before it leaves (<see cref="SlingerAim.Lob"/>).</summary>
    [HarmonyPatch(typeof(Attack), "FireProjectileBurst")]
    public static class SlingerAimPatch
    {
        private static void Prefix(Attack __instance)
        {
            if (__instance.m_attackOriginJoint == SlingerShot.Muzzle)   // every projectile passes here: bows, spells
            {
                SafeCall.Run("Attack.FireProjectileBurst slinger aim", static attack => SlingerAim.Lob(attack), __instance);
            }
        }
    }

    /// <summary>A greydwarf nest's pick may be a slinger instead (<see cref="SlingerSpawns.Nest"/>).</summary>
    [HarmonyPatch(typeof(SpawnArea), "SelectWeightedPrefab")]
    public static class SlingerNestSpawnPatch
    {
        private static void Postfix(SpawnArea __instance, ref SpawnArea.SpawnData __result)
        {
            __result = SafeCall.Run("SpawnArea.SelectWeightedPrefab slinger",
                static (picked, at) => SlingerSpawns.Nest(picked, at), __result, __instance.transform.position, __result);
        }
    }
}
