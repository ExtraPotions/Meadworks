// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Magpie.Config
{
    /// <summary>
    /// Design section 4.2: effective radius = min(Pickup Range, Max Pickup Range) x (current player auto pickup range /
    /// the player prefab's range). The factor is 1 unless another mod or product scales the player's range (A12, for
    /// example the Inscribe <c>pickup_radius</c> affix); a missing or broken factor counts as 1.
    /// </summary>
    public static class PickupRadius
    {
        public static float Effective(float pickupRange, float maxPickupRange, float currentPlayerRange, float basePlayerRange)
        {
            var range = MagpieSettings.PickupRange.Clamp(pickupRange);
            var cap = MagpieSettings.MaxPickupRange.Clamp(maxPickupRange);
            return Math.Min(range, cap) * Factor(currentPlayerRange, basePlayerRange);
        }

        public static float Factor(float current, float baseRange)
        {
            if (!Usable(current) || !Usable(baseRange)) return 1f;
            return current / baseRange;
        }

        private static bool Usable(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
