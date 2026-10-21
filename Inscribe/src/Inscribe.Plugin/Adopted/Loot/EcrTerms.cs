// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// The Elite Creatures Reborn terms of the chance model (ecr-integration.md sections 4-5), pure functions of the
    /// rules and the roll input, shared by the roll, <c>LootPreview</c> and <c>ecraft ecr</c> so they can never disagree.
    /// With <see cref="EcrRoll"/> at its default every term is the vanilla one.
    /// </summary>
    public static class EcrTerms
    {
        /// <summary><c>S</c>: ECR's own star table for ECR stars, else <c>drops.star_multipliers</c>.</summary>
        public static float StarMultiplier(DropRules drops, in LootInput input) =>
            input.Ecr.StarsFromEcr ? drops.Ecr.StarMultiplier(input.Stars) : drops.StarMultiplier(input.Stars);

        /// <summary><c>T_stone</c>: the world tier's stone chance factor; 1 without a recorded tier.</summary>
        public static float StoneFactor(DropRules drops, in LootInput input) =>
            input.Ecr.HasTier ? drops.Ecr.TierStoneMultiplier(input.Ecr.Tier) : 1f;

        /// <summary>The ECR share of the rarity shift in percent: <c>star_rarity_bonus x stars</c> plus the tier's bonus.</summary>
        public static float RarityPercent(DropRules drops, in LootInput input)
        {
            float percent = input.Ecr.StarsFromEcr ? drops.Ecr.StarRarityBonus * input.Stars : 0f;
            return input.Ecr.HasTier ? percent + drops.Ecr.TierRarityPercent(input.Ecr.Tier) : percent;
        }

        /// <summary>
        /// The whole rarity shift: Norns' Favour plus the ECR share, summed before the one multiply so the bonuses add
        /// rather than compound (section 4, drops.md section 10).
        /// </summary>
        public static float RarityBonus(DropRules drops, in LootInput input) =>
            input.Modifiers.RarityBonusPercent + RarityPercent(drops, input);
    }
}
