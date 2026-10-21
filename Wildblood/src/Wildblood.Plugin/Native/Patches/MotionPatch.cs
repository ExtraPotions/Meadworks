// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Aspects;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The one patch on <c>Character.UpdateMotion</c>, the start of a character's movement step on its owner, run every
    /// physics step for every character. An echo's step is skipped: it walks, turns and flies only as its boss did, put
    /// in place by <see cref="EchoDriver"/>, so the game's walking, falling, gravity and friction never move it. For
    /// everything else, before the game blends its stored swim velocity toward the swimmer's intent, a Gravitic pull's
    /// last push is taken back out of it (<see cref="GraviticPull.Repay"/>), so in water the pull never outlasts itself.
    /// With no echo loaded and nothing owed it is two checks; a failure is reported, never rethrown, and lets the step run.
    /// </summary>
    [HarmonyPatch(typeof(Character), "UpdateMotion")]
    public static class MotionPatch
    {
        private static bool Prefix(Character __instance)
        {
            if (EchoLink.Any && EchoLink.IsEcho(__instance))
            {
                return false;
            }
            if (!GraviticPull.Owes)
            {
                return true;
            }
            try
            {
                GraviticPull.Repay(__instance);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.UpdateMotion gravitic");
            }
            return true;
        }
    }
}
