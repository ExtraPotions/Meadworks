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
    /// Where every attack begins, for the player and every creature alike: while a shriek's ringing lasts, this
    /// machine's own player cannot start an attack with an Elemental or Blood Magic weapon (staffs, and anything else
    /// that trains those skills), and is told why (<see cref="ShriekDeafness.RefusesCast"/>). Melee, bows and the rest
    /// are untouched. Any other attack, or any attack while no one here is deafened, costs one bool check. A failure is
    /// reported and lets the attack go ahead, so a fault here never locks a player out of fighting.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    public static class ShriekCastPatch
    {
        private static bool Prefix(Humanoid __instance, ref bool __result)
        {
            if (!ShriekDeafness.Active || __instance != Player.m_localPlayer)
            {
                return true;
            }
            try
            {
                if (!ShriekDeafness.RefusesCast(__instance))
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                Guard.Report(e, "Humanoid.StartAttack screecher");
                return true;
            }
            __result = false;
            return false;
        }
    }
}
