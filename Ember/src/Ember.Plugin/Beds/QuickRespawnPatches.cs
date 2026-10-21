// OpenKeep. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Adapted from Homestead BedLoadPatch at 684671f98de54db6ea6652092ec041f44016c37d.
// Changed by expDARE, 2026: original-call request tracking and exception-safe reversible load thresholds.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using HarmonyLib;
namespace ExtraPotions.Ember.Runtime
{
    [HarmonyPatch(typeof(Game), nameof(Game.RequestRespawn))]
    internal static class QuickRespawnRequestPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static void Before(Game __instance) => QuickRespawn.RequestBeginning(__instance);
        [HarmonyFinalizer] static void Finally(Game __instance, Exception? __exception) { if (__exception != null) QuickRespawn.RequestFailed(__instance); }
        [HarmonyPostfix] static void After(Game __instance, float delay, bool afterDeath, bool __runOriginal) => QuickRespawn.Requested(__instance, delay, afterDeath, __runOriginal);
    }
    [HarmonyPatch(typeof(Game), "_RequestRespawn")]
    internal static class QuickRespawnRequestFiredPatch
    {
        [HarmonyPostfix] static void After(Game __instance, bool __runOriginal) => QuickRespawn.RequestFired(__instance, __runOriginal);
    }
    [HarmonyPatch(typeof(Game), nameof(Game.FindSpawnPoint))]
    internal static class QuickRespawnLoadPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.Last)]
        static void Before(Game __instance, float dt, bool __runOriginal, out QuickRespawn.LoadCall? __state) => __state = QuickRespawn.BeforeLoad(__instance, dt, __runOriginal);
        [HarmonyPostfix] static void After(QuickRespawn.LoadCall? __state, bool __runOriginal) => QuickRespawn.AfterLoad(__state, __runOriginal);
        [HarmonyFinalizer] static void Finally(QuickRespawn.LoadCall? __state, Exception? __exception) { if (__exception != null) QuickRespawn.AfterLoad(__state, false, true); }
    }
}
