// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;
using Biome = Heightmap.Biome;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// The game's biome flags mapped to the economy's biome ids and tiers (<c>biomes:</c>, drops.md section 3). Tiers
    /// are clamped to 1..8 (8 = Deep North, classes-and-tiers.md section 2).
    /// </summary>
    public static class BiomeTiers
    {
        private static readonly Biome[] Flags =
        {
            Biome.Meadows, Biome.BlackForest, Biome.Swamp, Biome.Mountain, Biome.Plains,
            Biome.Mistlands, Biome.AshLands, Biome.Ocean, Biome.DeepNorth,
        };

        private static readonly string[] Ids =
        {
            "meadows", "black_forest", "swamp", "mountain", "plains", "mistlands", "ashlands", "ocean", "deep_north",
        };

        /// <summary>The economy id of one biome flag, or null for none / a modded flag.</summary>
        public static string? IdOf(Biome biome)
        {
            int index = Array.IndexOf(Flags, biome);
            return index < 0 ? null : Ids[index];
        }

        /// <summary>The lowest mapped tier among the set flags; 0 when none is mapped. Out: the biome that gave it.</summary>
        public static int LowestTier(Biome flags, EconomyRules economy, out string biomeId)
        {
            int best = 0;
            biomeId = "";
            for (int i = 0; i < Flags.Length; i++)
            {
                if ((flags & Flags[i]) == 0)
                {
                    continue;
                }
                int tier = economy.BiomeTier(Ids[i]);
                if (tier > 0 && (best == 0 || tier < best))
                {
                    best = tier;
                    biomeId = Ids[i];
                }
            }
            return best == 0 ? 0 : Clamp(best);
        }

        /// <summary>
        /// The tier of the biome at a world position, from the world generator (works on a dedicated server with no
        /// terrain loaded; dungeons report their surface biome). Unmapped biomes fall back to tier 1.
        /// </summary>
        public static int AtPosition(Vector3 position, EconomyRules economy, out string biomeId)
        {
            Biome biome = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(position) : Biome.None;
            biomeId = IdOf(biome) ?? "unknown";
            int tier = economy.BiomeTier(biomeId);
            return tier > 0 ? Clamp(tier) : 1;
        }

        public static int Clamp(int tier) => Math.Max(1, Math.Min(DropParser.Tiers, tier));
    }
}
