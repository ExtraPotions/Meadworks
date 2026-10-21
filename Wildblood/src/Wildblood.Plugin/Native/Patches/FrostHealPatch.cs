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
    /// Frostbound's frost heal (<see cref="FrostHeal"/>), on the creature's owner, where the game resolves every hit. The
    /// prefix runs first of every prefix on the hit, so the frost it takes out is the frost the attacker sent, before this
    /// mod or the game scales or resists any of it; it never throws, so it can never stop a hit landing. The postfix heals
    /// once the hit has landed, last of every postfix, so the rest of the hit hurts first, a killing blow heals nothing,
    /// and what other reactions measure (Warding's reflect, Screecher's shriek) is what the hit took, not the heal. A
    /// failure there is reported and swallowed: the hit has landed by then and must stay landed.
    /// </summary>
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    public static class FrostHealPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Character __instance, HitData hit, out float __state)
        {
            __state = 0f;
            try
            {
                ZNetView nview = __instance.m_nview;
                if (hit != null && nview != null && nview.IsValid() && nview.IsOwner())
                {
                    __state = FrostHeal.Strip(__instance, hit);
                }
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.RPC_Damage frost heal capture");
            }
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Character __instance, float __state, bool __runOriginal)
        {
            if (__state > 0f && __runOriginal) // another mod's prefix skipped the hit: it never landed, so nothing heals
            {
                SafeCall.Run("Character.RPC_Damage frost heal", () => FrostHeal.Feed(__instance, __state));
            }
        }
    }
}
