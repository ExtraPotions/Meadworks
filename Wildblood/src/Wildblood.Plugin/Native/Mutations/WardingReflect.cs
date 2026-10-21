// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Warding's reflected damage, worked out on the creature's owner where the game resolves the hit. It reflects
    /// the base hit: what the hit actually took from the creature's health after its resistances and armour, with the
    /// game's sneak-attack and staggered-target bonuses divided back out and nothing counted past the health it had
    /// left, times the reflect percentage. That is then capped at <c>max reflect</c> percent of the ATTACKER's maximum
    /// health per second, across every hit and every Warding creature together (<see cref="ReflectBudget"/>), never
    /// enhanced, so no sneak attack, big weapon, large star or many-projectile volley can turn reflects into a one-shot.
    /// <para>
    /// Both bonuses are applied inside <c>Character.RPC_Damage</c> through <c>HitData.ApplyModifier</c>, on the same
    /// hit object the postfix sees: the backstab bonus (<c>hit.m_backstabBonus</c>) in the one branch that also
    /// stamps the creature's <c>m_backstabTime</c>, and a flat 2x on a creature that is staggering. So the prefix
    /// records the health, that stamp and the stagger state before the hit, and the postfix reads from them which
    /// bonuses fired. The division is exact on any world without the hidden world level; with one, the game's flat
    /// enemy armour is not proportional, so the base can come out somewhat higher than the bare hit would have done -
    /// still under the cap.
    /// </para>
    /// </summary>
    public static class WardingReflect
    {
        /// <summary>The game's fixed multiplier on a hit against a staggering creature.</summary>
        private const float StaggerBonus = 2f;

        /// <summary>The creature as the hit found it; <c>Active</c> only for a Warding creature, on its owner.</summary>
        public readonly struct Before
        {
            public readonly bool Active;
            public readonly float Health;
            public readonly float BackstabTime;
            public readonly bool Staggering;

            public Before(float health, float backstabTime, bool staggering)
            {
                Active = true;
                Health = health;
                BackstabTime = backstabTime;
                Staggering = staggering;
            }
        }

        /// <summary>Prefix side: what the postfix needs to tell the base hit from its bonuses. <paramref name="controller"/>
        /// is the victim's own, looked up once for the whole hit.</summary>
        public static Before Capture(Character victim, EliteController? controller)
        {
            ZNetView nview = victim.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner() || !IsWarding(controller))
            {
                return default;
            }
            // IsStaggering reads the animator's state, which a stagger triggered by this very hit does not change
            // before the animator next updates, so it answers here exactly as it will when the game checks it mid-hit.
            return new Before(victim.GetHealth(), victim.m_backstabTime, victim.IsStaggering());
        }

        /// <summary>Postfix side: the capped share of the base hit, dealt back to a living attacker.</summary>
        public static void Return(EliteController controller, Character victim, Character attacker, HitData hit,
            Before before)
        {
            if (!before.Active || attacker == victim || attacker.IsDead())
            {
                return;
            }
            float amount = Capped(controller, attacker, hit.m_attacker, BaseHit(victim, hit, before));
            if (amount <= 0f)
            {
                return;
            }
            attacker.Damage(Reflected(victim, attacker, amount));
            // Route the tell through THIS creature's own ZNetView, not the world-wide bus: every player still receives
            // it, but only the clients holding the creature (the attacker included) handle it; the rest drop it unread.
            CreatureRpc.FireReflect(controller.View, attacker.GetCenterPoint());
        }

        private static bool IsWarding(EliteController? controller) =>
            controller != null && controller.Ready && controller.Traits.Has(Mutation.Warding);

        /// <summary>What the hit did without its two bonuses, never more than the health the creature lost.</summary>
        private static float BaseHit(Character victim, HitData hit, Before before)
        {
            float lost = before.Health - Mathf.Max(0f, victim.GetHealth());
            if (lost <= 0f)
            {
                return 0f; // the hit never reached its health: dodged, ignored, or the creature was already down
            }
            return Mathf.Min(WithoutBonuses(hit, Bonus(victim, hit, before)), lost);
        }

        /// <summary>The product of the bonuses the game applied, read from what changed as the hit resolved.</summary>
        private static float Bonus(Character victim, HitData hit, Before before)
        {
            // The game stamps m_backstabTime in the branch that applies m_backstabBonus, and nowhere else.
            float bonus = victim.m_backstabTime != before.BackstabTime ? hit.m_backstabBonus : 1f;
            return before.Staggering && !victim.IsPlayer() ? bonus * StaggerBonus : bonus;
        }

        /// <summary>
        /// The resolved hit's damage to health with the bonus divided out of the parts it multiplied. ApplyModifier
        /// scales every typed damage except true damage and the non-player share, and the world-level flat damage is
        /// added on top, so those count whole. Fire, poison and spirit were multiplied too, but the game zeroes them
        /// out of the hit before it touches health and deals them later as burning and poison, so they are not in the
        /// total.
        /// </summary>
        private static float WithoutBonuses(HitData hit, float bonus)
        {
            HitData.DamageTypes d = hit.m_damage;
            float boosted = d.m_blunt + d.m_slash + d.m_pierce + d.m_chop + d.m_pickaxe + d.m_frost + d.m_lightning;
            return hit.GetTotalDamage() - boosted + boosted / Mathf.Max(bonus, 1f);
        }

        /// <summary>
        /// The reflect share of the base hit, cut to what is left of the attacker's budget: max reflect percent of its
        /// health in any one second, however many hits landed in it.
        /// </summary>
        private static float Capped(EliteController controller, Character attacker, ZDOID attackerId, float baseHit)
        {
            float reflect = Enhance.Magnitude(controller.Rules, controller.Traits, Mutation.Warding, Fields.Reflect);
            float ceiling = controller.Rules.PowerOf(Mutation.Warding, Fields.MaxReflect); // never enhanced: a ceiling
            float amount = baseHit * reflect / 100f;
            return ceiling > 0f ? ReflectBudget.Take(attackerId, amount, attacker.GetMaxHealth() * ceiling / 100f) : amount;
        }

        private static HitData Reflected(Character victim, Character attacker, float amount)
        {
            HitData reflected = new HitData();
            reflected.m_damage.m_blunt = amount;
            reflected.m_point = attacker.transform.position;
            reflected.m_dir = (attacker.transform.position - victim.transform.position).normalized;
            reflected.m_hitType = HitData.HitType.EnemyHit;
            return reflected;
        }
    }
}
