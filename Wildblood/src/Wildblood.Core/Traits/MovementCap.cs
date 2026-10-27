// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// The outrunnable cap on Wildblood's movement multiplier.
using System;

namespace ExtraPotions.Wildblood.Traits
{
    public static class MovementCap
    {
        /// <summary>
        /// The movement factor after the player-speed cap. Only speed Wildblood added is capped: a factor of 1 or less is
        /// returned unchanged, and a capped factor never drops below 1, so no creature runs slower than its vanilla speed.
        /// </summary>
        public static float Factor(float baseRun, float factor, float playerRun)
        {
            if (baseRun <= 0f || factor <= 1f || float.IsNaN(playerRun) || baseRun * factor <= playerRun) return factor;
            return Math.Min(factor, Math.Max(1f, playerRun / baseRun));
        }
    }
}
