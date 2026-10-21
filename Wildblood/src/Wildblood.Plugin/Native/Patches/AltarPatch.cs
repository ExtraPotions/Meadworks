// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Aspects;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Gives every boss altar its <see cref="AltarAspect"/> as the bowl starts, on every machine. The bowl's network view
    /// is found in its own Start, so this runs after it; an altar that summons an item rather than a boss is left alone.
    /// </summary>
    [HarmonyPatch(typeof(OfferingBowl), "Start")]
    public static class AltarStartPatch
    {
        private static void Postfix(OfferingBowl __instance) =>
            Guard.Run("OfferingBowl.Start aspect", () => AltarAspect.Attach(__instance));
    }

    /// <summary>Appends the boss's stars and the altar's current aspect (a Bountiful one's extras with it), what the
    /// fight pays and when it shifts, below the bowl's own hover text.</summary>
    [HarmonyPatch(typeof(OfferingBowl), nameof(OfferingBowl.GetHoverText))]
    public static class AltarHoverPatch
    {
        private static void Postfix(OfferingBowl __instance, ref string __result) =>
            __result = Guard.Run("OfferingBowl.GetHoverText aspect", static (bowl, text) => text + Lines(bowl), __instance,
                __result);

        // The bowl last hovered and its aspect (null for an altar that summons an item): one bowl is hovered at a time,
        // so this is looked up once per bowl the player turns to, not every frame.
        private static OfferingBowl? _lastBowl;
        private static AltarAspect? _lastAltar;

        private static string Lines(OfferingBowl bowl)
        {
            if (!ReferenceEquals(bowl, _lastBowl))
            {
                _lastBowl = bowl;
                _lastAltar = bowl.GetComponent<AltarAspect>();
            }
            return _lastAltar != null ? _lastAltar.HoverLines() : "";
        }
    }

    /// <summary>
    /// The offering: the game has accepted it and is about to count down to the boss, on the bowl's owner. The stars and
    /// aspects on the bowl right now are the fight - locked here, before the countdown gives the altar a chance to shift.
    /// </summary>
    [HarmonyPatch(typeof(OfferingBowl), "SpawnBoss")]
    public static class AltarLockPatch
    {
        private static void Prefix(OfferingBowl __instance) =>
            SafeCall.Run("OfferingBowl.SpawnBoss aspect", () => Lock(__instance));

        private static void Lock(OfferingBowl bowl)
        {
            AltarAspect altar = bowl.GetComponent<AltarAspect>();
            if (altar != null)
            {
                altar.Lock();
            }
        }
    }

    /// <summary>
    /// The countdown's end, where the game instantiates the boss: the locked draw is held for exactly this one call, and
    /// the finalizer lets it go whatever happens, so no later spawn can inherit it.
    /// </summary>
    [HarmonyPatch(typeof(OfferingBowl), "DelayedSpawnBoss")]
    public static class AltarSpawnPatch
    {
        private static void Prefix(OfferingBowl __instance) =>
            SafeCall.Run("OfferingBowl.DelayedSpawnBoss aspect", () => Hold(__instance));

        private static void Hold(OfferingBowl bowl)
        {
            AltarAspect altar = bowl.GetComponent<AltarAspect>();
            AltarSummon.Begin(altar != null ? altar.TakeLocked() : null);
        }

        private static Exception? Finalizer(Exception? __exception)
        {
            AltarSummon.End();
            return __exception;
        }
    }
}
