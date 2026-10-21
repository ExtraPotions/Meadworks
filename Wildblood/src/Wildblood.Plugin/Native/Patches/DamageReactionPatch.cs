// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The reactions to a hit that has landed: Warding reflects a capped share of the base hit back at the attacker
    /// (see <see cref="WardingReflect"/>) and knocks it back on a melee hit, Leeching heals the attacker for a share of
    /// what it dealt, and a Reflective boss returns a share of what it took as true damage. All key off what the hit
    /// actually did, so they run once the hit is resolved, on the victim's owner, reading their percentages from the
    /// biome rules each creature carries; two steps of <see cref="HitPatch"/>. Before the hit it only records what
    /// Warding needs (its health, backstab stamp and stagger state), and never throws, so it can never stop a hit landing.
    /// </summary>
    public static class DamageReactionPatch
    {
        internal static WardingReflect.Before Capture(Struck struck)
        {
            try
            {
                return WardingReflect.Capture(struck.Victim, struck.Elite);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.RPC_Damage warding capture");
                return default;
            }
        }

        internal static void React(Struck struck, WardingReflect.Before before)
        {
            HitData? hit = struck.Hit;
            if (hit == null || !struck.Owned)
            {
                return;
            }
            float dealt = hit.GetTotalDamage();
            Character? attacker = struck.Attacker;
            if (dealt <= 0f || attacker == null)
            {
                return;
            }
            Warding(struck, attacker, hit, before);
            Leech(struck.AttackerElite, attacker, dealt);
            ReflectiveHit.Return(struck.Victim, struck.Elite, attacker, dealt);
        }

        private static void Warding(Struck struck, Character attacker, HitData hit, WardingReflect.Before before)
        {
            EliteController? controller = With(struck.Elite, Mutation.Warding);
            if (controller == null)
            {
                return;
            }
            Character victim = struck.Victim;
            WardingReflect.Return(controller, victim, attacker, hit, before);
            if (!hit.m_ranged)
            {
                Vector3 dir = (attacker.transform.position - victim.transform.position).normalized;
                attacker.ApplyPushback(dir,
                    Enhance.Magnitude(controller.Rules, controller.Traits, Mutation.Warding, Fields.Knockback));
            }
        }

        private static void Leech(EliteController? attackerElite, Character attacker, float dealt)
        {
            EliteController? controller = With(attackerElite, Mutation.Leeching);
            if (controller != null)
            {
                float steal = Enhance.Magnitude(controller.Rules, controller.Traits, Mutation.Leeching, Fields.Lifesteal);
                attacker.Heal(dealt * steal / 100f, showText: false);
            }
        }

        private static EliteController? With(EliteController? controller, Mutation mutation) =>
            controller != null && controller.Ready && controller.Traits.Has(mutation) ? controller : null;
    }
}
