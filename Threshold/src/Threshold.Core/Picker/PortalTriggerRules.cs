// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the picker closes when the player steps out of the portal trigger (parity #114; PotalMap's
// documented behavior, behaviour reference only).
using System;

namespace ExtraPotions.Threshold.Picker
{
    public static class PortalTriggerRules
    {
        /// <summary>Meters beyond the trigger the player may stand before the picker closes (feet against a capsule).</summary>
        public const float ExitMargin = 1f;

        /// <summary>
        /// Whether the player left the trigger. <paramref name="gap"/> is the distance from the player to the trigger's
        /// collider now, <paramref name="openGap"/> that distance when the picker opened (0 inside it). Only moving out
        /// beyond the margin from where the picker opened counts. No reading (NaN) keeps the picker open; a portal that is
        /// gone (infinite distance) closes it.
        /// </summary>
        public static bool Left(float openGap, float gap)
        {
            if (float.IsNaN(gap)) return false;
            float start = float.IsNaN(openGap) || openGap < 0f || float.IsInfinity(openGap) ? 0f : openGap;
            return gap > start + ExitMargin;
        }
    }
}
