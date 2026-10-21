// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>How a kill's drop list is decided, one choice for the whole world (`loot.md` section 1).</summary>
    public enum LootMode
    {
        /// <summary>The mod never touches drops. This is the loot feature's off switch.</summary>
        Vanilla,

        /// <summary>The creature's own table, quantities multiplied by the star `drops` line.</summary>
        Scaled,

        /// <summary>The creature's own table, rolled once more per star, each roll independent. The default.</summary>
        Rolled,

        /// <summary>The `creatures:` rules decide entirely; a creature with no entry drops nothing but its trophy.</summary>
        Curated,
        /// <summary>Keep base chance and scale successful quantities using the adopted CreatureManager engine.</summary>
        Amount,
    }

    /// <summary>
    /// The world-wide loot settings from the rule file's `loot:` block. Trophies step outside all of it unless
    /// <see cref="MultiplyTrophies"/> says otherwise, because twelve identical trophies from one kill is clutter,
    /// not a reward (`loot.md` section 2).
    /// </summary>
    public sealed class LootRules
    {
        public LootMode Mode = LootMode.Rolled;

        /// <summary>Percent chance each star's extra roll happens, indexed like every other line (0 = unstarred,
        /// never rolled). Past the end the last entry repeats, the file's usual convention.</summary>
        public float[] ExtraRollChance = { 0f, 100f };

        /// <summary>Most extra rolls one kill can make however many stars it has; 0 means uncapped.</summary>
        public int MaxExtraRolls = 5;

        /// <summary>Multiplies every dropped quantity, after the mode. 1 leaves quantities alone.</summary>
        public float GlobalMultiplier = 1f;

        /// <summary>Applied on top of <see cref="GlobalMultiplier"/>, bosses only.</summary>
        public float BossMultiplier = 1f;

        /// <summary>On, trophies follow the mode like everything else. Off by default.</summary>
        public bool MultiplyTrophies;
        public int AmountPerStar = 50;
        public int AmountPerStarBosses = 50;

        public float ExtraRollChanceAt(int star)
        {
            if (ExtraRollChance == null || ExtraRollChance.Length == 0)
            {
                return 100f;
            }
            return ExtraRollChance[Mathf.Clamp(star, 0, ExtraRollChance.Length - 1)];
        }

        public LootRules Clone()
        {
            return new LootRules
            {
                Mode = Mode, ExtraRollChance = (float[])ExtraRollChance.Clone(), MaxExtraRolls = MaxExtraRolls,
                GlobalMultiplier = GlobalMultiplier, BossMultiplier = BossMultiplier,
                MultiplyTrophies = MultiplyTrophies,
                AmountPerStar = AmountPerStar, AmountPerStarBosses = AmountPerStarBosses,
            };
        }
    }
}
