// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Larder
{
    /// <summary>The few float helpers the rules share (Unity's Mathf is not available in Larder.Core).</summary>
    public static class LarderMath
    {
        public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        public static float Lerp(float from, float to, float t) => from + (to - from) * Clamp01(t);

        /// <summary>A multiplier or amount that cannot be negative: below 0 or not a number reads 0.</summary>
        public static float NonNegative(float value) => value > 0f && !float.IsInfinity(value) ? value : 0f;

        /// <summary>Whether two floats are equal within the tolerance Unity's Mathf.Approximately uses.</summary>
        public static bool Approximately(float a, float b)
        {
            return Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8f);
        }
    }
}
