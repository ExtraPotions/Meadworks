// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Wildblood.Adopted.Aspects;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The instant a boss's blow lands, handed to a Colossal boss's behaviour, which weighs it and sends the shockwave.
    /// Hooked on the two methods that deal the blow - the melee sweep and the area burst - which the game calls from
    /// <c>Attack.OnAttackTrigger</c> at the impact frame of the attack's animation, only on the attacker's owner and only
    /// while the attack is still live. So a swing the game cancels (the boss staggered, the owner changed mid-swing)
    /// shakes nothing, exactly as it hits nothing. Projectile, taunt and breath attacks never reach either method. It runs
    /// inside the game's attack, so a failure is reported and swallowed rather than cutting the attack short.
    /// </summary>
    [HarmonyPatch]
    public static class ColossalAttackPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Attack), "DoMeleeAttack");
            yield return AccessTools.Method(typeof(Attack), "DoAreaAttack");
        }

        private static void Postfix(Attack __instance, Humanoid ___m_character)
        {
            if (___m_character == null || !___m_character.IsBoss())
            {
                return; // every creature's and player's blows come through here; only a boss can be Colossal
            }
            SafeCall.Run("Attack landed (Colossal)", () => Landed(__instance, ___m_character));
        }

        private static void Landed(Attack attack, Humanoid boss)
        {
            ColossalBehaviour colossal = boss.GetComponent<ColossalBehaviour>();
            if (colossal != null)
            {
                colossal.Landed(attack);
            }
        }
    }
}
