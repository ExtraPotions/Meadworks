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
    /// Corrodent's armour wear (<see cref="CorrodentWear"/>), where the game wears a player's armour under a hit: inside
    /// the hit, on the hit player's own machine, the one place their gear lives. The prefix notes the worn pieces'
    /// durability only for a hit by a Corrodent creature and never throws, so it can never stop the hit; the postfix
    /// multiplies whatever the game took. A failure there is reported and swallowed: the game's own wear has happened
    /// and the hit goes on.
    /// </summary>
    [HarmonyPatch(typeof(Player), "DamageArmorDurability")]
    public static class CorrodentPatch
    {
        private static void Prefix(Player __instance, HitData hit, out CorrodentWear.Before __state)
        {
            __state = default;
            try
            {
                if (hit != null)
                {
                    __state = CorrodentWear.Capture(__instance, hit);
                }
            }
            catch (Exception e)
            {
                Guard.Report(e, "Player.DamageArmorDurability corrodent capture");
            }
        }

        private static void Postfix(Player __instance, CorrodentWear.Before __state)
        {
            if (__state.Active)
            {
                SafeCall.Run("Player.DamageArmorDurability corrodent", () => CorrodentWear.Apply(__instance, __state));
            }
        }
    }
}
