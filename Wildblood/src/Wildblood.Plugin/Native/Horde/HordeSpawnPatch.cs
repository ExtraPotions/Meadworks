// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood authority, lifecycle and existing stat/loot seams.
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Horde
{
    /// <summary>
    /// The horde's hold on the game's world spawning, which runs once a second on the client that owns each zone with a
    /// player in it. <c>UpdateSpawnList</c>: while the horde is on and the pass will run, the list's groups are grown for the pass and put back
    /// after (<see cref="HordeGroups"/>); an event's list (the game's raids) is left alone. <c>Spawn</c>: every creature it
    /// makes outside an event is tagged wild (<see cref="WildSpawn"/>), horde on or off, so the horde can weaken it. Both
    /// finalizers run whatever happens, so nothing grown or flagged outlives the call.
    /// </summary>
    [HarmonyPatch]
    internal static class HordeSpawnPatch
    {
        [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawnList")]
        [HarmonyPrefix]
        private static void BeforeList(List<SpawnSystem.SpawnData> spawners, bool eventSpawners, out HordeGroups.Pass? __state)
        {
            __state = null;
            // A skipped pass must not grow. The finalizer only restores a pass this prefix grew.
            if (SpawnListPass.HordeGrows(eventSpawners, spawners != null, HordeSettings.Enabled, HordeSettings.Spawns, CreatureSpawnBlocker.CurrentListSkipsOriginal))
            {
                try { __state = HordeGroups.Grow(spawners, HordeSettings.Spawns); }
                catch (Exception error) { Guard.Report(error, "SpawnSystem.UpdateSpawnList horde"); }
            }
        }

        [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawnList")]
        [HarmonyFinalizer]
        private static Exception? AfterList(Exception? __exception, HordeGroups.Pass? __state)
        {
            __state?.Restore();
            return __exception;
        }

        [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
        [HarmonyPrefix]
        private static void BeforeSpawn(bool eventSpawner, out bool __state) => __state = WildSpawn.Begin(HordeSettings.Available && !eventSpawner);

        [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
        [HarmonyFinalizer]
        private static Exception? AfterSpawn(Exception? __exception, bool __state)
        {
            WildSpawn.End(__state);
            return __exception;
        }
    }
}
