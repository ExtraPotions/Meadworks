// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Scaling
{
    /// <summary>
    /// Combines a creature's star power and its mutations into the fixed multipliers - health, size, movement, swing
    /// speed - the way the specification demands: additively, never multiplicatively. Every multiplier is read as a
    /// bonus above 1, the bonuses are summed, and the total is applied once, so two mutations pulling opposite ways
    /// cancel in proportion rather than compounding into an absurd number. They read the mutations a creature wears, not
    /// only those that work, so a Cloning decoy's body (<see cref="CreatureTraits.Decoy"/>) matches its creature's exactly.
    /// </summary>
    public static class StatMath
    {
        /// <summary>The one hard floor: a combined health multiplier below this would be a crash, not a fight.</summary>
        public const float HealthFloor = 0.05f;

        /// <summary>A correctness floor so a very well-fed Devouring creature crawls rather than reverses or freezes.</summary>
        public const float MoveFloor = 0.05f;

        public static float HealthMultiplier(BiomeRules rules, CreatureTraits traits)
        {
            float bonus = rules.Star.HpAt(traits.Stars) - 1f;
            if (traits.Wears(Mutation.Mad))
            {
                bonus += rules.PowerOf(Mutation.Mad, Fields.Health) - 1f; // a cost: never enhanced
            }
            if (traits.Wears(Mutation.Bloated))
            {
                bonus += Enhance.Stat(rules, traits, Mutation.Bloated, Fields.Health) - 1f;
            }
            if (traits.Wears(Mutation.Blinking))
            {
                bonus += rules.PowerOf(Mutation.Blinking, Fields.Health) - 1f; // a cost: never enhanced
            }
            return Mathf.Max(HealthFloor, 1f + bonus);
        }

        public static float SizeMultiplier(BiomeRules rules, CreatureTraits traits)
        {
            return 1f + rules.Star.GrowthAt(traits.Stars);
        }

        /// <summary>
        /// Movement, including a live negative bonus for Devouring's accumulated bulk (0 when it carries none) and the
        /// Swift modifier's bonus (0 when the creature is not Swift).
        /// </summary>
        public static float MoveMultiplier(BiomeRules rules, CreatureTraits traits, float devourBonus, float swiftBonus = 0f)
        {
            float bonus = rules.Star.SpeedAt(traits.Stars) - 1f + devourBonus + swiftBonus;
            if (traits.Wears(Mutation.Mad))
            {
                bonus += Enhance.Stat(rules, traits, Mutation.Mad, Fields.Move) - 1f;
            }
            if (traits.Wears(Mutation.Devouring))
            {
                bonus += rules.PowerOf(Mutation.Devouring, Fields.Move) - 1f; // a tuning knob, usually <= 1: never enhanced
            }
            float factor = Mathf.Max(MoveFloor, 1f + bonus) * AspectMath.SpeedFactor(traits); // Colossal, a layer apart
            return traits.Wears(Mutation.Relentless) ? Mathf.Min(1f, factor) : factor; // Relentless: never above its base
        }

        public static float SwingSpeedMultiplier(BiomeRules rules, CreatureTraits traits)
        {
            float bonus = rules.Star.SwingSpeedAt(traits.Stars) - 1f;
            if (traits.Wears(Mutation.Mad))
            {
                bonus += Enhance.Stat(rules, traits, Mutation.Mad, Fields.AttackSpeed) - 1f;
            }
            return Mathf.Max(MoveFloor, 1f + bonus);
        }
    }
}
