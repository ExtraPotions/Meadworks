// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// The fixed parent-to-child star-reduction distribution from the specification. Each copy rolls its own star count
    /// independently: most come back several stars weaker, so a chain normally dies within a generation or two and the
    /// long cascade is a rare story rather than the norm. This table is a designed probability shape, not a per-biome
    /// tunable, so it lives in code rather than the rule file; only the two cascade caps are configurable.
    /// </summary>
    public static class SplinterTable
    {
        // Row = parent star count 1..5. Columns = {1 less, 2 less, 3 less, 4 less, straight to 0} as percentages.
        private static readonly float[][] Weights =
        {
            new[] { 0f,  0f,  0f,  0f,  100f },  // 1 star
            new[] { 15f, 0f,  0f,  0f,  85f },   // 2 stars
            new[] { 15f, 35f, 0f,  0f,  50f },   // 3 stars
            new[] { 15f, 35f, 30f, 0f,  20f },   // 4 stars
            new[] { 15f, 35f, 30f, 15f, 5f },    // 5 stars
        };

        /// <summary>The child's star count. A 0-star parent breaks straight into two plain 0-star copies.</summary>
        public static int RollChildStars(int parentStars)
        {
            if (parentStars <= 0)
            {
                return 0;
            }
            float[] row = Weights[Mathf.Clamp(parentStars, 1, 5) - 1];
            float draw = Dice.Draw();
            float running = 0f;
            for (int index = 0; index < 4; index++)
            {
                running += row[index];
                if (draw <= running)
                {
                    return Mathf.Max(0, parentStars - (index + 1));
                }
            }
            return 0;
        }
    }
}
