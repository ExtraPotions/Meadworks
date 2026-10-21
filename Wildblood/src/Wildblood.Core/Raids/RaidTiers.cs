// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Wildblood.Raids
{
    /// <summary>
    /// The raids' gear tiers and the fair stake of each: 0 leather, 1 bronze, 2 iron, 3 silver, 4 black metal,
    /// 5 Mistlands, 6 Flametal, 7 the Deep North (Bloodgold or one of its armor sets). The step to each tier grows by 100
    /// coins from tier 3 on (300, 400, 500), so the Deep North's step is 600.
    /// </summary>
    public static class RaidTiers
    {
        /// <summary>The highest tier: the Deep North.</summary>
        public const int Top = 7;

        /// <summary>The highest tier before the Deep North. Raid records up to it keep their original encoding.</summary>
        public const int BeforeDeepNorth = 6;

        private static readonly int[] FairStakes = { 100, 200, 400, 600, 900, 1300, 1800, 2400 };

        /// <summary>The fair stake for a gear tier, clamped to 0-<see cref="Top"/>.</summary>
        public static int FairStake(int tier) => FairStakes[Clamp(tier)];

        /// <summary>A tier clamped to 0-<see cref="Top"/>.</summary>
        public static int Clamp(int tier) => Math.Min(Top, Math.Max(0, tier));
    }
}
