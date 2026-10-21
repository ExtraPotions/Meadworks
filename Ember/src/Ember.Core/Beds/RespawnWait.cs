// Adapted from ValheimModLibs/AreaLoading/QuickWait.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 684671f98de54db6ea6652092ec041f44016c37d
// Changed by expDARE, 2026: Unity-free fixed respawn policy and reversible native load threshold.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Ember.Beds
{
    public static class RespawnWait
    {
        public const float DeathSeconds = 10f;
        public const double Range = 1000d;
        public const float NearSeconds = 1f;

        public static float Seconds(double metres, float nativeLoadDuration)
        {
            double full = DeathSeconds + (double)nativeLoadDuration;
            if (!Valid(metres, nativeLoadDuration)) return (float)full;
            double share = Math.Min(metres / Range, 1d);
            return (float)(NearSeconds + (full - NearSeconds) * share);
        }

        public static float DeathDelay(double metres, float nativeLoadDuration, double elapsedSinceDeath)
        {
            double elapsed = Finite(elapsedSinceDeath) ? Math.Max(0d, elapsedSinceDeath) : 0d;
            double delay = DeathSeconds;
            if (Valid(metres, nativeLoadDuration) && Finite(elapsedSinceDeath) && elapsedSinceDeath >= 0d)
                delay *= Seconds(metres, nativeLoadDuration) / (DeathSeconds + (double)nativeLoadDuration);
            return (float)Math.Max(0d, delay - elapsed);
        }

        // The original timer keeps real elapsed time; callers restore this temporary threshold after the call.
        public static float LoadDuration(double metres, float nativeLoadDuration)
        {
            if (!Valid(metres, nativeLoadDuration)) return nativeLoadDuration;
            return (float)(nativeLoadDuration * (double)Seconds(metres, nativeLoadDuration) /
                           (DeathSeconds + (double)nativeLoadDuration));
        }

        private static bool Valid(double metres, float duration) => Finite(metres) && metres >= 0d &&
            !float.IsNaN(duration) && !float.IsInfinity(duration) && duration >= 0f;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
