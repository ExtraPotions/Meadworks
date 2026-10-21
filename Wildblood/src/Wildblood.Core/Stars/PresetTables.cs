// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Rules/PresetTables.cs and Rules/PresetCell.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; Unity-free (System.Math, Wildblood Biome enum instead of Heightmap.Biome);
// tables and cell logic otherwise unchanged.
// Changed by expDARE, 2026: tier 8 for the Deep North boss; a tier's mutation start never passes the ceiling.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Wildblood.Stars
{
    /// <summary>The biomes Wildblood ranks; <see cref="Other"/> is any biome outside the list (modded or none).</summary>
    public enum Biome { Meadows, BlackForest, Swamp, Mountain, Plains, Mistlands, AshLands, DeepNorth, Ocean, Other }

    /// <summary>
    /// The fixed numbers of the five difficulty presets (EliteCreaturesReborn features/difficulty.md): the shared star
    /// odds by cap, Extreme's caps by tier, and the per-boss terms. Biomes are ranked in the order a group reaches them,
    /// Meadows 0 to Deep North 7; the Ocean has no rank of its own.
    /// </summary>
    public static class PresetTables
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
        internal static readonly float[][] Odds =
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
        internal static readonly int[] ExtremeCaps = { 5, 5, 6, 6, 7, 7, 8, 8, 8 };

        /// <summary>Extreme's lean at tiers 1-3 in the biomes reached so far (smooth climb from 60% to 90% starred). Index = tier.</summary>
        internal static readonly float[] ExtremeRise = { 0f, 1.53833f, 1.44709f, 1.63712f };

        /// <summary>The most stars any preset gives (Extreme at tiers 6-8).</summary>
        public const int MostStars = 8;

        /// <summary>A biome's rank, Meadows 0 to Deep North 7; <see cref="Ocean"/>; or -2 for a biome outside the list.</summary>
        public static int Rank(Biome biome)
        {
            switch (biome)
            {
                case Biome.Meadows: return 0;
                case Biome.BlackForest: return 1;
                case Biome.Swamp: return 2;
                case Biome.Mountain: return 3;
                case Biome.Plains: return 4;
                case Biome.Mistlands: return 5;
                case Biome.AshLands: return 6;
                case Biome.DeepNorth: return 7;
                case Biome.Ocean: return Ocean;
                default: return -2;
            }
        }

        public static int ClampTier(int tier) => Math.Min(TopTier, Math.Max(0, tier));
    }

    /// <summary>
    /// What one difficulty preset gives one biome at one world tier: the star cap, the weight of each star count from 0
    /// to the cap, and the mutation rate of a creature with no stars. Pure: the same preset, tier and biome always give
    /// the same cell, on every peer.
    /// </summary>
    public readonly struct PresetCell
    {
        private PresetCell(int cap, float[] weights, float mutation)
        {
            Cap = cap;
            Weights = weights;
            Mutation = mutation;
        }

        public int Cap { get; }

        /// <summary>Unnormalised weights of 0 ... <see cref="Cap"/> stars.</summary>
        public float[] Weights { get; }

        /// <summary>Percent chance a creature with no stars carries a mutation; each star adds a quarter of it.</summary>
        public float Mutation { get; }

        /// <summary>The cell for a preset; a biome outside the ranked list counts as the biome being entered at this tier.</summary>
        public static PresetCell For(Difficulty preset, int tier, int rank)
        {
            if (preset == Difficulty.Custom) throw new ArgumentException("Custom has no preset cell.", nameof(preset));
            int t = PresetTables.ClampTier(tier);
            int b = rank < PresetTables.Ocean ? t : rank;
            int past = b == PresetTables.Ocean ? 0 : Math.Max(0, t - b);
            int cap = PresetCaps.Cap(preset, t, b);
            float lean = PresetCaps.Lean(preset, t, b) * (1f + PresetTables.PastLean * past);
            return new PresetCell(cap, Weigh(cap, lean), PresetMutations.Rate(preset, t, b, past));
        }

        /// <summary>Percent of creatures with at least one star.</summary>
        public float StarredPercent
        {
            get
            {
                float total = 0f;
                foreach (float w in Weights) total += w;
                return total <= 0f ? 0f : 100f - Weights[0] * 100f / total;
            }
        }

        // The shared row for the cap, each star count's weight times lean^stars.
        private static float[] Weigh(int cap, float lean)
        {
            float[] row = PresetTables.Odds[Math.Min(PresetTables.Odds.Length - 1, Math.Max(0, cap))];
            float[] weights = new float[row.Length];
            float factor = 1f;
            for (int stars = 0; stars < row.Length; stars++)
            {
                weights[stars] = row[stars] * factor;
                factor *= lean;
            }
            return weights;
        }
    }

    /// <summary>The agreed star caps and odds leans of each preset (features/difficulty.md sections 2-3).</summary>
    internal static class PresetCaps
    {
        public static int Cap(Difficulty preset, int t, int b)
        {
            bool ocean = b == PresetTables.Ocean;
            int behind = ocean ? 0 : Math.Max(0, t - b);
            switch (preset)
            {
                case Difficulty.Easy: return ocean ? 2 : Math.Min(4, 1 + behind);
                case Difficulty.Medium: return ocean ? (t < 4 ? 2 : 3) : Math.Min(5, 2 + behind);
                case Difficulty.Hard: return ocean ? (t < 4 ? 3 : 4) : Math.Min(5, 3 + behind);
                case Difficulty.VeryHard: return ocean ? (t < 4 ? 4 : 5) : Math.Min(5, 4 + behind);
                default: return Reached(t, b) ? PresetTables.ExtremeCaps[t] : Math.Max(5, PresetTables.ExtremeCaps[t] - 1);
            }
        }

        public static float Lean(Difficulty preset, int t, int b)
        {
            switch (preset)
            {
                case Difficulty.Easy: return 0.9f;
                case Difficulty.Medium: return 1f;
                case Difficulty.Hard: return 1.25f;
                case Difficulty.VeryHard: return 1.1f * (1f + 0.15f * t);
                default: return Reached(t, b) && t >= 1 && t <= 3 ? PresetTables.ExtremeRise[t] : 1.3f * (1f + 0.05f * t);
            }
        }

        // A biome reached so far: the one being entered and every one before it (never the Ocean).
        private static bool Reached(int t, int b) => b != PresetTables.Ocean && b <= t;
    }

    /// <summary>
    /// The agreed mutation rates (features/difficulty.md section 4): the biome being entered starts at the preset's rate,
    /// every boss killed after a biome closes a fifth of the gap to the ceiling; Very Hard is halfway between Hard and
    /// Extreme in every cell; the Ocean has rates of its own.
    /// </summary>
    internal static class PresetMutations
    {
        public static float Rate(Difficulty preset, int t, int b, int past)
        {
            if (preset == Difficulty.VeryHard)
                return (Rate(Difficulty.Hard, t, b, past) + Rate(Difficulty.Extreme, t, b, past)) / 2f;
            Shape(preset, out float start, out float perTier, out float ceiling);
            float begin = Math.Min(ceiling, start + perTier * t); // Extreme passes its ceiling at tier 8
            if (b == PresetTables.Ocean) return Ocean(preset, t, begin);
            return ceiling - (ceiling - begin) * (float)Math.Pow(1f - PresetTables.PastClose, past);
        }

        private static void Shape(Difficulty preset, out float start, out float perTier, out float ceiling)
        {
            switch (preset)
            {
                case Difficulty.Easy: start = 10f; perTier = 0f; ceiling = 35f; return;
                case Difficulty.Medium: start = 25f; perTier = 0f; ceiling = 55f; return;
                case Difficulty.Hard: start = 30f; perTier = 0f; ceiling = 65f; return;
                default: start = 45f; perTier = 7f; ceiling = 98f; return;
            }
        }

        private static float Ocean(Difficulty preset, int t, float begin)
        {
            switch (preset)
            {
                case Difficulty.Easy: return 10f;
                case Difficulty.Medium: return t < 4 ? 25f : 30f;
                case Difficulty.Hard: return t < 4 ? 30f : 35f;
                default: return begin;
            }
        }
    }
}
