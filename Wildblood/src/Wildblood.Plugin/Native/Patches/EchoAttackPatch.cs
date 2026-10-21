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
    /// Where every attack begins: an Echoing boss's start is noted before (the weapon and the random state it starts
    /// with) and kept after when the game started it (<see cref="EchoTapes.AttackStarting"/>), so its echo starts the
    /// very same attack `delay` seconds later. The cues the start sends belong to the attack, not to the tape on their own.
    /// With no boss recorded here it is one count check a side. A failure is reported, never rethrown, so the attack runs.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    public static class EchoAttackPatch
    {
        private static void Prefix(Humanoid __instance, bool secondaryAttack)
        {
            if (!EchoTapes.Any)
            {
                return;
            }
            try
            {
                EchoTapes.AttackStarting(__instance, secondaryAttack);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Humanoid.StartAttack echoing");
            }
        }

        private static void Postfix(Humanoid __instance, bool __result)
        {
            if (!EchoTapes.Any)
            {
                return;
            }
            try
            {
                EchoTapes.AttackStarted(__instance, __result);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Humanoid.StartAttack echoing");
            }
        }
    }
}
