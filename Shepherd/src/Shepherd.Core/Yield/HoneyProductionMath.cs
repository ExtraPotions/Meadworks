// Adapted from ValheimMods OpenKeep/OpenKeep/src/Homestead/HiveRate.cs
// and OpenKeep/OpenKeep/src/Homestead/HiveProgress.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 3bb778330f23645ccddb42fbc0c6ffab72c842b6
// Changed by expDARE, 2026: finite guards, portable rate math and native-unit progress boundary.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Yield
{
    public static class HoneyProductionMath
    {
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static float SecondsPerHoney(float fixedPerDay, bool perPlayer, int online, float daySeconds)
        {
            if (!Finite(daySeconds) || daySeconds <= 0f) return 0f;
            float rate = perPlayer ? Math.Max(1, online) :
                Finite(fixedPerDay) ? Math.Max(0f, Math.Min(1000f, fixedPerDay)) : 0f;
            float seconds = rate > 0f ? daySeconds / rate : 0f;
            return Finite(seconds) && seconds > 0f ? seconds : 0f;
        }

        public static bool TryRescale(float product, float fromSeconds, float toSeconds, out float scaled)
        {
            scaled = product;
            if (!Finite(product) || product < 0f || !Finite(fromSeconds) || fromSeconds <= 0f
                || !Finite(toSeconds) || toSeconds <= 0f) return false;
            float result = (float)((double)product * toSeconds / fromSeconds);
            if (!Finite(result)) return false;
            scaled = result;
            return true;
        }

        public static bool TryNativeRemainder(float product, float elapsed, float seconds, out float total, out float remainder)
        {
            total = product + elapsed;
            remainder = product;
            if (!Finite(product) || product < 0f || !Finite(elapsed) || elapsed < 0f
                || !Finite(seconds) || seconds <= 0f || !Finite(total) || (double)total / seconds >= int.MaxValue)
                return false;
            // The native producer rolls only above the period, then drops the fractional remainder.
            remainder = total > seconds ? total - (int)(total / seconds) * seconds : total;
            return Finite(remainder) && remainder >= 0f;
        }
    }
}
