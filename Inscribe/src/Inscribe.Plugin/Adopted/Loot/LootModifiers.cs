// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Killer-side modifiers on a death (drops.md section 10): Fateweaver on the stone chance, Norns' Favour on the rarity
    /// weights above the lowest magic rarity, Trophy Taker and Hoardfinder on the creature's own vanilla trophy and
    /// coin/valuable drop chances. Chests, bosses' guaranteed counts and <c>ecraft</c> test rolls use <see cref="None"/>
    /// (Norns' Favour still shifts a boss's rarity row).
    /// </summary>
    public readonly struct LootModifiers
    {
        public static readonly LootModifiers None = new LootModifiers(0f, 0f, 0f, 0f);

        public LootModifiers(float rarityPercent, float stonesPercent, float trophyPercent, float coinsPercent)
        {
            RarityBonusPercent = rarityPercent;
            StonesPercent = stonesPercent;
            TrophyPercent = trophyPercent;
            CoinsPercent = coinsPercent;
        }

        /// <summary>Norns' Favour +L%: rarity weights above the lowest magic rarity times <c>1 + L/100</c>.</summary>
        public float RarityBonusPercent { get; }

        /// <summary>Fateweaver +F%.</summary>
        public float StonesPercent { get; }

        /// <summary>Trophy Taker +T%.</summary>
        public float TrophyPercent { get; }

        /// <summary>Hoardfinder +C%.</summary>
        public float CoinsPercent { get; }

        /// <summary>Multiplies the stone chance <c>p</c> (Fateweaver: <c>1 + F/100</c>). Boss guarantees are untouched.</summary>
        public float StoneChanceFactor => Factor(StonesPercent);

        public float TrophyFactor => Factor(TrophyPercent);

        public float CoinsFactor => Factor(CoinsPercent);

        /// <summary>Whether any vanilla drop chance changes (<see cref="VanillaDropBoost"/>).</summary>
        public bool BoostsVanillaDrops => TrophyPercent > 0f || CoinsPercent > 0f;

        /// <summary>The killer's published totals for a death; <see cref="None"/> when there is no player killer.</summary>
        public static LootModifiers ForDeath(Character creature) => KillerStats.Read(creature);

        private static float Factor(float percent) => 1f + percent / 100f;
    }
}
