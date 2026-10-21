// OpenKeep. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Adapted from OpenKeep at 684671f98de54db6ea6652092ec041f44016c37d, Homestead BedSeen/BedUse/BedDeath/BedRespawn/BedGone/BedSpawned patches.
// Changed by expDARE, 2026: local native ownership, original-call guards and no sleep/map/timing changes.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Ember.Beds;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Ember.Runtime
{
    [HarmonyPatch(typeof(Bed), nameof(Bed.Awake))]
    internal static class OwnedBedSeenPatch
    {
        [HarmonyPostfix] static void After(Bed __instance, bool __runOriginal) { if (__runOriginal) OwnedBedRespawn.Observe(__instance); }
    }
    [HarmonyPatch(typeof(Bed), nameof(Bed.Interact))]
    internal static class OwnedBedClaimPatch
    {
        [HarmonyPostfix] static void After(Bed __instance, Humanoid __0, bool __runOriginal)
        {
            if (__runOriginal && __0 == Player.m_localPlayer) OwnedBedRespawn.ClaimMayHaveChanged(__instance);
        }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    internal static class OwnedBedDeathPatch
    {
        [HarmonyPrefix] static void Before(Player __instance, out QuickRespawn.DeathCall? __state) => __state = QuickRespawn.BeforeDeath(__instance);
        [HarmonyPostfix] static void After(Player __instance, bool __runOriginal, QuickRespawn.DeathCall? __state)
        {
            if (__runOriginal) OwnedBedRespawn.Died(__instance);
            QuickRespawn.AfterDeath(__instance, __state, __runOriginal);
        }
    }
    [HarmonyPatch(typeof(Game), nameof(Game.FindSpawnPoint))]
    internal static class OwnedBedFallbackPatch
    {
        [HarmonyPrefix] static void Before(Game __instance, out Vector3? __state)
        {
            var profile = __instance.GetPlayerProfile();
            __state = OwnedBedRespawn.Active && __instance.m_respawnAfterDeath && profile != null && profile.HaveCustomSpawnPoint()
                ? profile.GetCustomSpawnPoint() : (Vector3?)null;
        }
        [HarmonyPostfix] static void After(Game __instance, bool __result, bool __runOriginal, Vector3? __state)
        {
            var profile = __instance.GetPlayerProfile();
            if (!__runOriginal || __result || !__state.HasValue || profile == null || profile.HaveCustomSpawnPoint()) return;
            var point = __state.Value;
            OwnedBedRespawn.FailedSpawn(new BedPoint(point.x, point.y, point.z));
        }
    }
    [HarmonyPatch(typeof(Game), nameof(Game.RemoveCustomSpawnPoint))]
    internal static class OwnedBedGonePatch
    {
        [HarmonyPostfix] static void After(Vector3 point, bool __runOriginal) { if (__runOriginal) OwnedBedRespawn.Gone(point); }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class OwnedBedSpawnedPatch
    {
        [HarmonyPostfix] static void After(Player __instance, bool __runOriginal) { if (__runOriginal) { OwnedBedRespawn.Spawned(__instance); if (__instance == Player.m_localPlayer) QuickRespawn.Reset(); } }
    }
}
