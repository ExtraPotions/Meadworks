// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Reads <c>drops.ecr</c> (ecr-integration.md section 10). Merging is the family's ordinary one (ECO-1): scalars take
    /// the last value read, the lists are replaced whole. Validation: every number at least 0; each list non-empty and
    /// at most 64 entries (error); a <c>tier_*</c> list longer than 8 is a warning, since ECR's world tiers stop at 7.
    /// </summary>
    internal static class EcrDropParser
    {
        private const int MaxEntries = 64;
        private const int EcrTiers = 8;

        public static EcrDrops Parse(MapReader drops)
        {
            MapReader? sub = drops.Sub("ecr");
            if (sub == null)
            {
                return new EcrDrops();
            }
            MapReader r = sub.Value;
            r.Unknown("star_multipliers", "star_step", "star_rarity_bonus", "tier_rune_multipliers", "tier_rarity_bonus", "skip_worthless");
            EcrDrops ecr = new EcrDrops
            {
                StarStep = r.Float("star_step", 0.5f, 0f),
                StarRarityBonus = r.Float("star_rarity_bonus", 0f, 0f),
                SkipWorthless = r.Bool("skip_worthless", true),
            };
            ecr.StarMultipliers = List(r, "star_multipliers", ecr.StarMultipliers, 0);
            ecr.TierStoneMultipliers = List(r, "tier_rune_multipliers", ecr.TierStoneMultipliers, EcrTiers);
            ecr.TierRarityBonus = List(r, "tier_rarity_bonus", ecr.TierRarityBonus, EcrTiers);
            return ecr;
        }

        // Absent: the default. A bad list is an error and keeps the default, so one pass reports everything.
        private static IReadOnlyList<float> List(MapReader r, string key, IReadOnlyList<float> fallback, int warnAbove)
        {
            float[]? values = r.Floats(key);
            if (values == null)
            {
                return fallback;
            }
            if (values.Length == 0 || values.Length > MaxEntries)
            {
                r.Error(key, $"should have 1 to {MaxEntries} entries, has {values.Length}");
                return fallback;
            }
            if (warnAbove > 0 && values.Length > warnAbove)
            {
                r.Warn(key, $"has {values.Length} entries; Elite Creatures Reborn's world tiers stop at {warnAbove - 1}, the rest are never used");
            }
            return values;
        }
    }
}
