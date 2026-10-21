// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Generates an inscription's tier rows from a ladder <c>{ count, from, min, max }</c> exactly as the planning page
    /// does (classes-and-tiers.md section 4): unlock levels spread from <c>from</c> to 8, values on a curve
    /// <c>min + span * (j/k)^1.25</c>, weights equal to the tier number. Pure double arithmetic in the page's order of
    /// operations, rounding half up like JavaScript's <c>Math.round</c>, so both produce the same rows.
    /// </summary>
    internal static class TierLadder
    {
        public const int MaxCount = 16;
        public const int MaxLevel = 8;

        /// <summary>The inputs of one ladder; <see cref="Valued"/> false for flags.</summary>
        public sealed class Spec
        {
            public int Count;
            public int From;
            public bool Valued;
            public double Min;
            public double Max;

            /// <summary>Decimals written in the YAML's min and max texts.</summary>
            public int MinDecimals;
            public int MaxDecimals;

            /// <summary>A whole-number flat ladder: <c>value: flat</c>, no unit, integer min and max.</summary>
            public bool WholeFlat;
        }

        /// <summary>The rows, strongest last (index 0 is T<c>k</c>, grade 1).</summary>
        public static List<AffixTierDef> Rows(Spec spec)
        {
            int[] levels = UnlockLevels(spec.Count, spec.From);
            List<AffixTierDef> rows = new List<AffixTierDef>(spec.Count);
            for (int j = 0; j < spec.Count; j++)
            {
                int shown = spec.Count - j;
                rows.Add(new AffixTierDef { Grade = j + 1, Shown = shown, Level = levels[j], Weight = shown });
            }
            if (spec.Valued)
            {
                FillValues(spec, rows);
            }
            return rows;
        }

        /// <summary>Unlock levels, weakest tier first (index j = 0 is T<c>k</c>).</summary>
        public static int[] UnlockLevels(int k, int from)
        {
            int n = 9 - from;
            int[] levels = new int[k];
            if (k <= n)
            {
                for (int j = 0; j < k; j++)
                {
                    levels[j] = k == 1 ? from : (int)JsRound(from + j * (double)(8 - from) / (k - 1));
                }
                return levels;
            }
            if (n <= 1)
            {
                for (int j = 0; j < k; j++)
                {
                    levels[j] = MaxLevel;
                }
                return levels;
            }
            return Spread(k, from, n, levels);
        }

        // T k at `from`; the other k - 1 tiers over levels from+1 .. 8, the lower levels taking the remainder first.
        private static int[] Spread(int k, int from, int n, int[] levels)
        {
            levels[0] = from;
            int lv = n - 1, perLevel = (k - 1) / lv, rem = (k - 1) % lv, next = 1;
            for (int i = 0; i < lv; i++)
            {
                for (int x = 0; x < perLevel + (i < rem ? 1 : 0); x++)
                {
                    levels[next++] = from + 1 + i;
                }
            }
            return levels;
        }

        private static void FillValues(Spec spec, List<AffixTierDef> rows)
        {
            int k = spec.Count;
            double span = spec.Max - spec.Min, step = span / k;
            if (spec.WholeFlat && span < 2 * k)
            {
                for (int j = 0; j < k; j++)
                {
                    double v = k == 1 ? spec.Max : JsRound(spec.Min + span * j / (k - 1));
                    SetRow(rows[j], v, v, 0);
                }
                return;
            }
            int dec = spec.WholeFlat ? 0 : Math.Min(2, Math.Max(StepDecimals(step), Math.Max(spec.MinDecimals, spec.MaxDecimals)));
            FillCurve(spec, rows, span, dec);
        }

        private static void FillCurve(Spec spec, List<AffixTierDef> rows, double span, int dec)
        {
            int k = spec.Count;
            double q = Math.Pow(10, -dec);
            double[] b = new double[k + 1];
            for (int j = 0; j <= k; j++)
            {
                b[j] = JsRound((spec.Min + span * Math.Pow((double)j / k, 1.25)) / q) * q;
            }
            for (int j = 0; j < k; j++)
            {
                double low = j == 0 ? b[0] : b[j] + q, high = b[j + 1];
                SetRow(rows[j], low > high ? high : low, high, dec);
            }
        }

        private static int StepDecimals(double step) => step >= 1 ? 0 : step >= 0.1 ? 1 : 2;

        private static void SetRow(AffixTierDef row, double min, double max, int dec)
        {
            row.Min = (float)Math.Round(min, dec, MidpointRounding.AwayFromZero);
            row.Max = (float)Math.Round(max, dec, MidpointRounding.AwayFromZero);
            row.Decimals = dec;
        }

        /// <summary>JavaScript's <c>Math.round</c>: the nearest integer, halves up.</summary>
        public static double JsRound(double x) => Math.Floor(x + 0.5);
    }
}
