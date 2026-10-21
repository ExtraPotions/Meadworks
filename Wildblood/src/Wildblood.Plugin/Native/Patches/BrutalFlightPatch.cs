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
    /// owner just before the velocity is applied. While this machine's own player is in the air from a Brutal throw, the
    /// velocity is set to the body's own here (<see cref="BrutalFlight.Steer"/>), so the game's walking code applies no
    /// change and the throw alone carries them. Last of all patches here, so the flight has the final word over any other
    /// push. Any other step is a single bool check; a failure is reported and never rethrown.
    /// </summary>
    [HarmonyPatch(typeof(Character), "AddPushbackForce")]
    public static class BrutalFlightPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Character __instance, ref Vector3 velocity)
        {
            if (!BrutalFlight.Active || !(__instance is Player player))
            {
                return;
            }
            try
            {
                BrutalFlight.Steer(player, ref velocity);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.AddPushbackForce brutal");
            }
        }
    }

    /// <summary>
    /// The game's fall hit, made by the player's own client as they land (<c>Character.UpdateGroundContact</c>) and sent
    /// through <c>Character.Damage</c>: dropped before it is sent while a Brutal throw forgives it
    /// (<see cref="BrutalFlight.Forgives"/>), so the landing deals nothing and shows nothing. Every other hit is one byte
    /// check; a failure is reported and the fall hurts as the game decides.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    public static class BrutalFallPatch
    {
        private static bool Prefix(Character __instance, HitData hit)
        {
            if (hit == null || hit.m_hitType != HitData.HitType.Fall)
            {
                return true;
            }
            try
            {
                return !BrutalFlight.Forgives(__instance, hit);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.Damage brutal fall");
                return true;
            }
        }
    }
}
