// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Scaling
{
    /// <summary>
    /// The numbers an aspect changes about the boss itself, read live from the rules. They multiply the boss as its
    /// stars left it rather than joining the additive star sum, so "25% less health" means exactly that - an aspect is
    /// a separate layer, not another line on the star table. Hit-time damage changes live in <see cref="AspectDamage"/>.
    /// </summary>
    public static class AspectMath
    {
        /// <summary>The floor under a reduced multiplier, so a misconfigured 100% cut leaves a fight rather than a crash.</summary>
        private const float Floor = 0.05f;

        public static float Power(Aspect aspect, string field) => RuleState.Active.Boss.Aspects.PowerOf(aspect, field);

        /// <summary>Twin's and Tethered's cut and Colossal's boost to the starred maximum health, multiplied when a boss
        /// carries more than one; 1 for everything else (a Phantom copy's health is flat).</summary>
        public static float HealthFactor(CreatureTraits traits)
        {
            float factor = 1f;
            if (traits.HasAspect(Aspect.Twin))
            {
                factor *= Cut(Power(Aspect.Twin, Fields.LessHealth));
            }
            if (traits.HasAspect(Aspect.Tethered))
            {
                factor *= Cut(Power(Aspect.Tethered, Fields.LessHealth));
            }
            if (traits.HasAspect(Aspect.Colossal))
            {
                factor *= Boost(Power(Aspect.Colossal, Fields.MoreHealth));
            }
            return factor;
        }

        /// <summary>Colossal's growth on top of the boss's star growth; 1 for everything else.</summary>
        public static float SizeFactor(CreatureTraits traits) =>
            traits.HasAspect(Aspect.Colossal) ? Boost(Power(Aspect.Colossal, Fields.Bigger)) : 1f;

        /// <summary>Colossal's slowness on top of the boss's star speed; 1 for everything else.</summary>
        public static float SpeedFactor(CreatureTraits traits) =>
            traits.HasAspect(Aspect.Colossal) ? Cut(Power(Aspect.Colossal, Fields.Slower)) : 1f;

        /// <summary>
        /// A Phantom copy's whole maximum health, whatever its stars: `health per tier` for each world tier, with tier 0
        /// counting as 1, so a copy stays a few blows' work as the world hardens.
        /// </summary>
        public static float PhantomHealth(AspectRules rules) =>
            Mathf.Max(1f, rules.PowerOf(Aspect.Phantom, Fields.HealthPerTier) * Mathf.Max(1, WorldTier.Current()));

        public static float PhantomHealth() => PhantomHealth(RuleState.Active.Boss.Aspects);

        /// <summary>The health marks, in percent of its maximum left, at which a Phantom boss splits off its copies.</summary>
        public static float[] PhantomSplits() => RuleState.Active.Boss.Aspects.ListOf(Aspect.Phantom, Fields.SplitAt);

        /// <summary>The share of its starred damage a boss deals: Twin's, Tethered's and a Phantom copy's cut, 1 otherwise.</summary>
        public static float DamageFactor(CreatureTraits traits)
        {
            if (traits.PhantomCopy)
            {
                return Cut(Power(Aspect.Phantom, Fields.LessDamage));
            }
            float factor = traits.HasAspect(Aspect.Twin) ? Cut(Power(Aspect.Twin, Fields.LessDamage)) : 1f;
            return traits.HasAspect(Aspect.Tethered) ? factor * Cut(Power(Aspect.Tethered, Fields.LessDamage)) : factor;
        }

        /// <summary>A "percent less" number as the multiplier it leaves: 25 becomes 0.75.</summary>
        public static float Cut(float percentLess) => Mathf.Max(Floor, 1f - percentLess / 100f);

        /// <summary>A "percent more" number as its multiplier: 20 becomes 1.2.</summary>
        public static float Boost(float percentMore) => Mathf.Max(0f, 1f + percentMore / 100f);
    }
}
