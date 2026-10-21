// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// The Swift modifier's movement bonus. It joins the creature's other movement bonuses additively and is re-derived from
// the captured base speeds, so applying it again never compounds.
using System;

namespace ExtraPotions.Wildblood.Traits
{
    public static class SwiftMovement
    {
        /// <summary>The additive movement bonus for a creature's stored Swift power; 0 when it is not Swift.</summary>
        public static float Bonus(bool swift, float power)
        {
            return swift && !float.IsNaN(power) ? Math.Clamp(power, 0f, 1f) : 0f;
        }

        /// <summary>True when the current bonus differs from the one already applied, so movement is re-derived once.</summary>
        public static bool NeedsReapply(float applied, float current)
        {
            return Math.Abs(applied - current) > 0.0001f;
        }
    }
}
