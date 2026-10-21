// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// <c>drops.ecr</c> (ecr-integration.md section 10): the Elite Creatures Reborn terms of the drop roll. Read only on a
    /// peer where ECR is installed; the defaults are DECISIONS ECR-4 to ECR-7. The tier lists are indexed by ECR's world
    /// tier (0-7), which ECR does not record yet (ECR-6), so they are inert until it does.
    /// </summary>
    public sealed class EcrDrops
    {
        /// <summary>Index = ECR stars (0 = unstarred). Replaces <c>drops.star_multipliers</c> for ECR-resolved creatures.</summary>
        public IReadOnlyList<float> StarMultipliers { get; internal set; } = new[] { 1f, 1f, 1.5f, 2f, 2.5f, 3f };

        /// <summary>Added per star beyond <see cref="StarMultipliers"/>.</summary>
        public float StarStep { get; internal set; } = 0.5f;

        /// <summary>Percent per ECR star on the gear rarity weights above the first magic rarity (default 0, ECR-5).</summary>
        public float StarRarityBonus { get; internal set; }

        /// <summary>Index = ECR world tier; multiplies the stone chance only.</summary>
        public IReadOnlyList<float> TierStoneMultipliers { get; internal set; } = new[] { 1f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f };

        /// <summary>Index = ECR world tier; percent on the gear rarity weights, summed with Norns' Favour.</summary>
        public IReadOnlyList<float> TierRarityBonus { get; internal set; } = new[] { 0f, 5f, 10f, 15f, 20f, 25f, 30f, 35f };

        /// <summary>ECR's hollow copies (Phantom copies, Cloning decoys) drop nothing from us; applies with the synergy off too.</summary>
        public bool SkipWorthless { get; internal set; } = true;

        /// <summary>The star multiplier for this many ECR stars: the list, then the last entry plus the step per extra star.</summary>
        public float StarMultiplier(int stars)
        {
            if (StarMultipliers.Count == 0)
            {
                return 1f;
            }
            int last = StarMultipliers.Count - 1;
            stars = Math.Max(0, stars);
            return stars <= last ? StarMultipliers[stars] : StarMultipliers[last] + (stars - last) * StarStep;
        }

        /// <summary>The stone chance factor at an ECR world tier; an index past the end reuses the last entry.</summary>
        public float TierStoneMultiplier(int tier) => AtOrLast(TierStoneMultipliers, tier, 1f);

        /// <summary>The rarity bonus percent at an ECR world tier; an index past the end reuses the last entry.</summary>
        public float TierRarityPercent(int tier) => AtOrLast(TierRarityBonus, tier, 0f);

        private static float AtOrLast(IReadOnlyList<float> list, int index, float empty)
        {
            if (list.Count == 0 || index < 0)
            {
                return empty;
            }
            return list[Math.Min(index, list.Count - 1)];
        }
    }
}
