// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Rolling
{
    /// <summary>
    /// The numeric core of every roll (classes-and-tiers.md section 5, affixes.md "Value types"), free of game types so
    /// a scratch harness can compile it on its own and check the distributions. Pure: every random number comes from
    /// the <see cref="Random"/> passed in, so a seeded source reproduces a roll exactly.
    /// </summary>
    internal static class RollMath
    {
        /// <summary>
        /// A <c>scaled</c> value times the item class's <c>damage_scale</c>, rounded again to the tier's decimals (half
        /// away from zero), so what is stored is what is shown and applied. In decimal: 5 x 0.7 is 3.5 and rounds to 4,
        /// where the float product would be 3.4999 and round down.
        /// </summary>
        public static float Scale(float value, float scale, int decimals)
        {
            decimals = Math.Min(Math.Max(decimals, 0), 4);
            return (float)Math.Round((decimal)value * (decimal)scale, decimals, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// A value uniform in [min, max] at the tier's decimals: every representable step (1, 0.1 or 0.01 apart)
        /// between the bounds is equally likely, both bounds included, and the result never leaves the bounds.
        /// Judgement call: drawing the steps directly rather than rounding a continuous draw, which would give each
        /// bound half the chance of an inner value.
        /// </summary>
        public static float RollValue(float min, float max, int decimals, Random random)
        {
            if (max <= min)
            {
                return min;
            }
            decimals = Math.Min(Math.Max(decimals, 0), 4);
            double scale = Math.Pow(10, decimals);
            long low = (long)Math.Round(min * scale, MidpointRounding.AwayFromZero);
            long high = (long)Math.Round(max * scale, MidpointRounding.AwayFromZero);
            long step = low + (long)Math.Floor(random.NextDouble() * (high - low + 1));
            step = Math.Min(Math.Max(step, low), high);
            float value = (float)Math.Round(step / scale, decimals, MidpointRounding.AwayFromZero);
            return Math.Min(Math.Max(value, min), max);
        }

        /// <summary>Index of a weighted pick; weights at or below 0 never win. -1 when nothing can win.</summary>
        public static int PickWeighted(IReadOnlyList<float> weights, Random random)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                total += Math.Max(weights[i], 0f);
            }
            if (total <= 0)
            {
                return -1;
            }
            double target = random.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                float weight = Math.Max(weights[i], 0f);
                if (weight > 0f && target < weight)
                {
                    return i;
                }
                target -= weight;
            }
            return LastPositive(weights);
        }

        /// <summary>
        /// The affix count of a fresh roll: uniform in [min, max], or by <c>rolling.count_weights</c> when the rarity
        /// has a row (counts outside [min, max] are ignored; a row with no usable weight falls back to uniform).
        /// </summary>
        public static int PickCount(int min, int max, IReadOnlyDictionary<int, float>? weights, Random random)
        {
            if (max <= min)
            {
                return Math.Max(min, 0);
            }
            if (weights != null)
            {
                List<float> row = new List<float>(max - min + 1);
                for (int count = min; count <= max; count++)
                {
                    row.Add(weights.TryGetValue(count, out float w) ? w : 0f);
                }
                int index = PickWeighted(row, random);
                if (index >= 0)
                {
                    return min + index;
                }
            }
            return random.Next(min, max + 1);
        }

        /// <summary>Where a value sits in its tier, 0 (worst) to 1 (best); a single-value tier or a flag is 1.</summary>
        public static float Position(float value, float min, float max) =>
            max <= min ? 1f : Math.Min(Math.Max((value - min) / (max - min), 0f), 1f);

        private static int LastPositive(IReadOnlyList<float> weights)
        {
            for (int i = weights.Count - 1; i >= 0; i--)
            {
                if (weights[i] > 0f)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
