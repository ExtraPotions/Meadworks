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
    /// Every animation cue a creature's owner sends (a swing, a stagger, a taunt), before the game sends it to every
    /// machine: an Echoing boss's go on its tape (<see cref="EchoTapes.Cue"/>) for its echo to play `delay` seconds later.
    /// With no boss recorded here it is one count check. A failure is reported, never rethrown, so the cue is always sent.
    /// </summary>
    [HarmonyPatch(typeof(ZSyncAnimation), nameof(ZSyncAnimation.SetTrigger))]
    public static class EchoCuePatch
    {
        private static void Prefix(ZSyncAnimation __instance, string name)
        {
            if (!EchoTapes.Any)
            {
                return;
            }
            try
            {
                EchoTapes.Cue(__instance, name);
            }
            catch (Exception e)
            {
                Guard.Report(e, "ZSyncAnimation.SetTrigger echoing");
            }
        }
    }
}
