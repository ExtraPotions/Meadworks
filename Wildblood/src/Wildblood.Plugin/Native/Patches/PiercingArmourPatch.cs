// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the armor read Piercing cuts (EliteCreaturesReborn 3.22, behavior only).
using System;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The player's armor as the game reads it while resolving a hit: cut by <see cref="PiercingArmour"/> while a
    /// Piercing creature's hit on this player resolves, untouched otherwise (one null check). A failure is reported and the
    /// armor left as the game read it.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetBodyArmor))]
    public static class PiercingArmourPatch
    {
        private static void Postfix(Player __instance, ref float __result)
        {
            if (PiercingArmour.Ignoring <= 0f)
            {
                return;
            }
            try
            {
                PiercingArmour.Apply(__instance, ref __result);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Player.GetBodyArmor piercing");
            }
        }
    }
}
