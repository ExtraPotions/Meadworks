// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Reflective: a share of the damage a hit actually dealt to the boss is dealt back to whoever landed it. Called from
    /// the damage-reaction postfix on the boss's owner, which only ever sees direct hits - the game applies burn and
    /// poison ticks straight to health, never through that path - so a tick is never returned. The returned hit is true
    /// damage (the game's untyped damage, which armour and resistances do not touch) and cannot be blocked or dodged, so
    /// no gear turns the aspect into a non-event. It carries no attacker, so nothing reflects it back again.
    /// </summary>
    internal static class ReflectiveHit
    {
        /// <summary>On the boss's owner, once a hit has landed; <paramref name="controller"/> is the boss's own.</summary>
        public static void Return(Character boss, EliteController? controller, Character attacker, float dealt)
        {
            if (controller == null || !controller.Ready || !controller.Traits.HasAspect(Aspect.Reflective) || attacker == boss)
            {
                return;
            }
            float amount = dealt * AspectMath.Power(Aspect.Reflective, Fields.Reflect) / 100f;
            if (amount <= 0f)
            {
                return;
            }
            attacker.Damage(TrueHit(boss, attacker, amount));
            CreatureRpc.FireReflect(controller.View, attacker.GetCenterPoint());
        }

        private static HitData TrueHit(Character boss, Character attacker, float amount)
        {
            HitData hit = new HitData();
            hit.m_damage.m_damage = amount;
            hit.m_point = attacker.GetCenterPoint();
            hit.m_dir = (attacker.transform.position - boss.transform.position).normalized;
            hit.m_hitType = HitData.HitType.EnemyHit;
            hit.m_blockable = false;
            hit.m_dodgeable = false;
            hit.m_pushForce = 0f;
            return hit;
        }
    }
}
