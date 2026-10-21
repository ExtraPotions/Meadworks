// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Character.UpdateBodyFriction sets how hard a body's feet hold the ground, each physics step (and as it jumps).
    /// While this machine's player stands on a Frostbound patch, alive and on the ground, their feet hold only the
    /// grip's share of it (<see cref="IceFooting.Loosen"/>), so friction does not stop the slide the movement patch
    /// lets run (<see cref="IceSlipPatch"/>). Anyone else, or anyone while nobody here is on ice, costs one float
    /// check. A failure is reported and leaves the game's friction as it was.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.UpdateBodyFriction))]
    public static class IceFrictionPatch
    {
        private static void Postfix(Character __instance)
        {
            if (!IceFooting.Slipping || !ReferenceEquals(__instance, Player.m_localPlayer))
            {
                return;
            }
            try
            {
                if (!__instance.IsDead() && __instance.IsOnGround() && __instance.m_collider != null)
                {
                    IceFooting.Loosen(__instance.m_collider.material);
                }
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.UpdateBodyFriction ice");
            }
        }
    }
}
