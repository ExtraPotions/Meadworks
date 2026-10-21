// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Rolling
{
    /// <summary>
    /// Which tiers of an affix a roll may pick, and the pick itself (classes-and-tiers.md section 5). Ordinary rolls:
    /// rows unlocked at the item's level or below, weight above 0, and on a class where the affix is only
    /// <c>allowed</c>, below the closed top (<c>tier >= 1 + floor(k * allowed_closed_fraction)</c>); then a rune's
    /// <c>tier_floor</c> keeps only the best that-many of them. Chaotic rolls (the Serpent): every tier the affix
    /// defines, drawn uniformly; level, closed tiers and floor ignored.
    /// </summary>
    internal static class TierEligibility
    {
        /// <summary>
        /// Fills <paramref name="into"/> with the eligible rows, weakest first; empty = the affix cannot roll here. An
        /// affix whose every tier weighs 0 never rolls at all, not even chaotically (configuration.md section 4).
        /// </summary>
        public static void Eligible(AffixDef def, ClassFit fit, RollContext context, List<AffixTierDef> into)
        {
            into.Clear();
            if (context.Chaotic)
            {
                AddChaotic(def, into);
                return;
            }
            int closed = ClosedTiers(def, fit, context.Rules.Economy.Rolling.AllowedClosedFraction);
            for (int i = 0; i < def.Tiers.Count; i++)
            {
                AffixTierDef row = def.Tiers[i];
                if (row.Weight > 0f && row.Level <= context.Level && row.Shown > closed)
                {
                    into.Add(row);
                }
            }
            if (context.TierFloor > 0 && into.Count > context.TierFloor)
            {
                into.RemoveRange(0, into.Count - context.TierFloor);
            }
        }

        /// <summary>Whether an ordinary roll at this level has any tier of the affix to pick (gear capacity, listings).</summary>
        public static bool HasEligible(AffixDef def, ClassFit fit, int level, RuleSet rules)
        {
            int closed = ClosedTiers(def, fit, rules.Economy.Rolling.AllowedClosedFraction);
            for (int i = 0; i < def.Tiers.Count; i++)
            {
                AffixTierDef row = def.Tiers[i];
                if (row.Weight > 0f && row.Level <= level && row.Shown > closed)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How many of the strongest tiers are closed: none on a best fit, <c>floor(k * fraction)</c> when allowed.</summary>
        public static int ClosedTiers(AffixDef def, ClassFit fit, float fraction) =>
            fit == ClassFit.Best ? 0 : (int)Math.Floor(def.TierCount * (double)fraction);

        // Every tier the affix defines, weights ignored, unless no tier may roll at all.
        private static void AddChaotic(AffixDef def, List<AffixTierDef> into)
        {
            for (int i = 0; i < def.Tiers.Count; i++)
            {
                if (def.Tiers[i].Weight > 0f)
                {
                    into.AddRange(def.Tiers);
                    return;
                }
            }
        }

        /// <summary>Picks a row: by tier weight, or uniformly on a chaotic roll or when every weight is 0.</summary>
        public static AffixTierDef Pick(List<AffixTierDef> rows, RollContext context, List<float> scratch)
        {
            if (!context.Chaotic)
            {
                scratch.Clear();
                for (int i = 0; i < rows.Count; i++)
                {
                    scratch.Add(rows[i].Weight);
                }
                int index = RollMath.PickWeighted(scratch, context.Random);
                if (index >= 0)
                {
                    return rows[index];
                }
            }
            return rows[context.Random.Next(rows.Count)];
        }
    }
}
