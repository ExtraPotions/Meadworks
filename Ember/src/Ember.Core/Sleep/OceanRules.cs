// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT. Adapted from BetterSleepBruh 2.1.0 at
// fe2bb81866e58f3c4afeb832876cba6ebf1e12cd: BetterSleepBruh/Patches/WaterVolumePatches.cs and ShipPatches.cs. The MIT
// notice is retained in THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: the decisions are pure and testable; the wave clock advances once per frame (the game
// updates the water time three times a frame); a non-finite or negative rate counts as no boost; the
// multiplier is clamped to 0..1; namespace Ember.
using System;

namespace ExtraPotions.Ember.Sleep
{
    /// <summary>Ocean and boat safeguards while the night is accelerated (wave speed, wave height, boat water force).</summary>
    public static class OceanRules
    {
        /// <summary>Waves calm back toward the target at this much per real second.</summary>
        public const float CalmSpeed = 0.5f;

        static bool Boosting(double extraRate) => extraRate > 0.0 && !double.IsNaN(extraRate) && !double.IsInfinity(extraRate);

        /// <summary>True when the wave clock should advance at real time instead of boosted time.</summary>
        public static bool DecoupleWaves(bool enabled, double extraRate) => enabled && Boosting(extraRate);

        /// <summary>
        /// How far one UpdateWaterTime call moves the decoupled wave clock. The game calls it from FixedUpdate, Update and
        /// LateUpdate, so only the first non-fixed call of each frame advances, by that frame's delta (zero while paused).
        /// </summary>
        public static float WaveClockStep(bool fixedStep, int frame, ref int lastFrame, float dt)
        {
            if (fixedStep || frame == lastFrame) return 0f;
            lastFrame = frame;
            return Math.Max(0f, dt);
        }

        /// <summary>The wave height factor to move toward: the multiplier while calming applies, else normal.</summary>
        public static float CalmTarget(bool enabled, double extraRate, float multiplier) =>
            enabled && Boosting(extraRate) ? Math.Max(0f, Math.Min(1f, float.IsNaN(multiplier) ? 1f : multiplier)) : 1f;

        /// <summary>One smoothing step of the calm factor.</summary>
        public static float Approach(float current, float target, float seconds)
        {
            var step = CalmSpeed * Math.Max(0f, seconds);
            if (target > current) return Math.Min(target, current + step);
            return Math.Max(target, current - step);
        }

        /// <summary>True when a boat's water force update should be skipped this frame.</summary>
        public static bool SkipShipWaterForce(bool preventDamage, bool suppressShake, double extraRate) =>
            (preventDamage || suppressShake) && Boosting(extraRate);
    }
}
