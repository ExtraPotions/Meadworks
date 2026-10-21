// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Aspects;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The one place the game adds a knockback to a character's movement, run every physics step on the character's
    /// owner, on foot and swimming alike, just before the velocity is applied. While a Gravitic boss is pulling this
    /// machine's own player, the pull is added here (<see cref="GraviticPull"/>), so the game carries it out as its own
    /// intent rather than correcting it away the next step. Any other character, or any step with no pull running, is
    /// a single bool check. It runs inside the game's character loop, so a failure is reported and never rethrown.
    /// </summary>
    [HarmonyPatch(typeof(Character), "AddPushbackForce")]
    public static class GraviticPullPatch
    {
        private static void Postfix(Character __instance, ref Vector3 velocity)
        {
            if (!GraviticPull.Active || !(__instance is Player player) || player != Player.m_localPlayer)
            {
                return;
            }
            try
            {
                GraviticPull.Steer(player, ref velocity);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.AddPushbackForce gravitic");
            }
        }
    }

}
