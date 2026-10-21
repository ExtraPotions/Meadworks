// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// A Cloning decoy's blow emptied at the swing, on the decoy's owner where its attack runs: the game builds every melee,
    /// thrown and area blow's damage in <c>Attack.ModifyDamage</c>, so whatever it lands on - a wall, a tree, a cart -
    /// takes nothing from it. Hits on players and creatures are emptied again where they land (<see cref="CloneHitPatch"/>),
    /// which also catches what never passes through here, such as an area its projectile leaves behind.
    /// </summary>
    [HarmonyPatch(typeof(Attack), "ModifyDamage")]
    public static class CloneAttackPatch
    {
        private static void Postfix(Attack __instance, HitData hitData) =>
            Guard.Run("Attack.ModifyDamage cloning", static (attack, hit) => Disarm(attack, hit), __instance, hitData);

        private static void Disarm(Attack attack, HitData hit)
        {
            if (hit != null && CloneStore.IsDecoy(attack.m_character))
            {
                CloneHits.Harmless(hit);
            }
        }
    }
}
