// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Changed by expDARE, 2026: a tier's mutation start never passes the ceiling (tier 8).
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// What one difficulty preset gives one biome at one world tier (features/difficulty.md): the star cap, the weight
    /// of each star count from 0 to the cap, and the mutation rate of a creature with no stars. Pure: the same preset,
    /// tier and biome always give the same cell, on every peer.
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

        /// <summary>
        /// The cell for a preset. <paramref name="rank"/> is <see cref="PresetTables.Rank"/>'s: a biome outside the
        /// ranked list counts as the biome being entered at this tier.
        /// </summary>
        public static PresetCell For(Difficulty preset, int tier, int rank)
        {
            int t = PresetTables.ClampTier(tier);
            int b = rank < PresetTables.Ocean ? t : rank;
            int past = b == PresetTables.Ocean ? 0 : Mathf.Max(0, t - b);
            int cap = PresetCaps.Cap(preset, t, b);
            float lean = PresetCaps.Lean(preset, t, b) * (1f + PresetTables.PastLean * past);
            return new PresetCell(cap, Weigh(cap, lean), PresetMutations.Rate(preset, t, b, past));
        }

        /// <summary>Percent of creatures with at least one star (for the console).</summary>
        public float StarredPercent
        {
            get
            {
                float total = 0f;
                foreach (float w in Weights)
                {
                    total += w;
                }
                return total <= 0f ? 0f : 100f - Weights[0] * 100f / total;
            }
        }

        // The shared row for the cap, each star count's weight times lean^stars.
        private static float[] Weigh(int cap, float lean)
        {
            float[] row = PresetTables.Odds[Mathf.Clamp(cap, 0, PresetTables.Odds.Length - 1)];
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
        /// <summary>The cap at tier <paramref name="t"/> for ranked biome <paramref name="b"/> (or the Ocean).</summary>
        public static int Cap(Difficulty preset, int t, int b)
        {
            bool ocean = b == PresetTables.Ocean;
            int behind = ocean ? 0 : Mathf.Max(0, t - b);
            switch (preset)
            {
                case Difficulty.Easy: return ocean ? 2 : Mathf.Min(4, 1 + behind);
                case Difficulty.Medium: return ocean ? (t < 4 ? 2 : 3) : Mathf.Min(5, 2 + behind);
                case Difficulty.Hard: return ocean ? (t < 4 ? 3 : 4) : Mathf.Min(5, 3 + behind);
                case Difficulty.VeryHard: return ocean ? (t < 4 ? 4 : 5) : Mathf.Min(5, 4 + behind);
                default: return Reached(t, b) ? PresetTables.ExtremeCaps[t] : Mathf.Max(5, PresetTables.ExtremeCaps[t] - 1);
            }
        }

        /// <summary>The preset's lean at the tier, before the per-boss-past term.</summary>
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
            {
                return (Rate(Difficulty.Hard, t, b, past) + Rate(Difficulty.Extreme, t, b, past)) / 2f;
            }
            Shape(preset, out float start, out float perTier, out float ceiling);
            float begin = Mathf.Min(ceiling, start + perTier * t); // Extreme passes its ceiling at tier 8
            if (b == PresetTables.Ocean)
            {
                return Ocean(preset, t, begin);
            }
            return ceiling - (ceiling - begin) * Mathf.Pow(1f - PresetTables.PastClose, past);
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
