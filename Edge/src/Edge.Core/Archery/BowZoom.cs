// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Bow zoom idea seen in BetterArchery (ishid4); implemented from its public feature list only.
using System;

namespace ExtraPotions.Edge.Archery
{
    /// <summary>
    /// Bow zoom math: the blend follows the bow's draw (0 at rest, 1 fully drawn) at a bounded speed, and the camera's
    /// field of view is the game's value divided by a factor that grows with the blend.
    /// </summary>
    public static class BowZoom
    {
        public const float MaxFactor = 3f;

        /// <summary>Seconds a full zoom in or out takes.</summary>
        public const float EaseSeconds = 0.2f;

        public static float Fov(float baseFov, float factor, float blend)
        {
            var f = float.IsNaN(factor) ? 1f : Math.Max(1f, Math.Min(MaxFactor, factor));
            var b = Clamp01(blend);
            return baseFov / (1f + (f - 1f) * b);
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/>, a full 0..1 sweep taking <paramref name="easeSeconds"/>.</summary>
        public static float Step(float current, float target, float deltaSeconds, float easeSeconds)
        {
            var c = Clamp01(current);
            var t = Clamp01(target);
            if (!(easeSeconds > 0f)) return t;
            var max = Math.Max(0f, deltaSeconds) / easeSeconds;
            var delta = t - c;
            return Math.Abs(delta) <= max ? t : c + Math.Sign(delta) * max;
        }

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
    }
}
