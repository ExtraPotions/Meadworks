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
    /// The damage modifiers computed at the moment of a hit, where health-linked mutations need the current state.
    /// Outgoing damage sums star attack, Splintering's penalty and Plated's rising bite additively; Devouring adds a
    /// flat amount from what it has eaten; incoming damage is cut by a flat percentage from Plated, which fades as it
    /// is hurt. The percentage is applied directly rather than through the game's own armour curve, whose quadratic
    /// low end let a flat armour value erase almost all of a weak hit's damage regardless of the victim's health.
    /// </summary>
    public static class DamageMath
    {
        public static float OutgoingMultiplier(BiomeRules rules, CreatureTraits traits, float healthFraction)
        {
            float bonus = rules.Star.AttackAt(traits.Stars) - 1f;
            if (traits.Has(Mutation.Splintering))
            {
                bonus += rules.PowerOf(Mutation.Splintering, Fields.Damage) - 1f; // not enhanced
            }
            if (traits.Has(Mutation.Plated))
            {
                bonus += Enhance.Magnitude(rules, traits, Mutation.Plated, Fields.Damage) / 100f * (1f - healthFraction);
            }
            return Mathf.Max(0f, 1f + bonus);
        }

        /// <summary>Flat physical damage a Devouring creature adds from the damage of what it has eaten.</summary>
        public static float DevouredFlatDamage(CreatureTraits traits, ZDO zdo)
        {
            if (!traits.Has(Mutation.Devouring) || zdo == null)
            {
                return 0f;
            }
            return TraitStore.GetDevouredDamage(zdo);
        }

        /// <summary>Plated's damage-cut percent at full health: the configured value, large-star enhanced, hard-capped
        /// by `max reduction` so the enhancement cannot approach invulnerability. 0 when not Plated.</summary>
        public static float PlatedPercent(BiomeRules rules, CreatureTraits traits)
        {
            if (!traits.Has(Mutation.Plated))
            {
                return 0f;
            }
            float percent = Enhance.Magnitude(rules, traits, Mutation.Plated, Fields.Armour);
            return Mathf.Min(percent, rules.PowerOf(Mutation.Plated, Fields.MaxReduction));
        }

        /// <summary>The fraction (0-1) a Plated victim's incoming damage is cut by right now: full at full health,
        /// fading to nothing at empty. 0 otherwise.</summary>
        public static float PlatedReduction(BiomeRules rules, CreatureTraits traits, float healthFraction) =>
            PlatedPercent(rules, traits) / 100f * Mathf.Clamp01(healthFraction);
    }
}
