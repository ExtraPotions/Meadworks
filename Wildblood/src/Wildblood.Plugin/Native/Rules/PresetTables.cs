// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Changed by expDARE, 2026: tier 8 for the Deep North boss.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The fixed numbers of the five difficulty presets, agreed with the user on 2026-10-01 (features/difficulty.md):
    /// the shared star odds by cap, Extreme's caps by tier, and the per-boss terms. Biomes are ranked in the order a
    /// group reaches them, Meadows 0 to Deep North 7; the Ocean has no rank of its own. Data, no logic beyond the rank.
    /// </summary>
    internal static class PresetTables
    {
        /// <summary>The Ocean's rank: it has no boss, so it follows the world tier alone.</summary>
        public const int Ocean = -1;

        /// <summary>The tables cover world tiers 0 to 8 (eight vanilla bosses); a world with more counted bosses uses tier 8's row.</summary>
        public const int TopTier = 8;

        /// <summary>Odds lean this much heavier per star for every boss killed after a biome.</summary>
        public const float PastLean = 0.05f;

        /// <summary>Every boss killed after a biome closes this share of the gap to the preset's mutation ceiling.</summary>
        public const float PastClose = 0.2f;

        /// <summary>Each star adds this share of the cell's mutation rate.</summary>
        public const float StarMutation = 0.25f;

        /// <summary>The weight of 0, 1, 2 ... stars under each cap, index = cap. Every preset shares it.</summary>
        public static readonly float[][] Odds =
        {
            new[] { 100f },
            new[] { 90f, 10f },
            new[] { 85f, 12f, 3f },
            new[] { 75f, 15f, 7f, 3f },
            new[] { 65f, 17f, 10f, 5f, 3f },
            new[] { 55f, 18f, 12f, 8f, 5f, 2f },
            new[] { 45f, 18f, 13f, 10f, 7f, 5f, 2f },
            new[] { 35f, 18f, 14f, 11f, 9f, 7f, 4f, 2f },
            new[] { 28f, 17f, 14f, 12f, 10f, 8f, 6f, 3f, 2f },
        };

        /// <summary>Extreme's cap by world tier in the biomes reached so far; the rest and the Ocean one lower, never below 5.</summary>
        public static readonly int[] ExtremeCaps = { 5, 5, 6, 6, 7, 7, 8, 8, 8 };

        /// <summary>
        /// Extreme's lean at tiers 1-3 in the biomes reached so far, solved so the biome being entered climbs smoothly
        /// from 60% starred at tier 0 to 90% at tier 4 (each rise four-fifths of the one before) instead of stepping
        /// with the 5,5,6,6 caps. Index = tier; 0 = not used.
        /// </summary>
        public static readonly float[] ExtremeRise = { 0f, 1.53833f, 1.44709f, 1.63712f };

        /// <summary>The most stars any preset gives (Extreme at tiers 6-8).</summary>
        public const int MostStars = 8;

        /// <summary>A biome's rank, Meadows 0 to Deep North 7; <see cref="Ocean"/>; or -2 for a biome outside the list.</summary>
        public static int Rank(Heightmap.Biome biome)
        {
            switch (biome)
            {
                case Heightmap.Biome.Meadows: return 0;
                case Heightmap.Biome.BlackForest: return 1;
                case Heightmap.Biome.Swamp: return 2;
                case Heightmap.Biome.Mountain: return 3;
                case Heightmap.Biome.Plains: return 4;
                case Heightmap.Biome.Mistlands: return 5;
                case Heightmap.Biome.AshLands: return 6;
                case Heightmap.Biome.DeepNorth: return 7;
                case Heightmap.Biome.Ocean: return Ocean;
                default: return -2;
            }
        }

        public static int ClampTier(int tier) => Mathf.Clamp(tier, 0, TopTier);
    }
}
