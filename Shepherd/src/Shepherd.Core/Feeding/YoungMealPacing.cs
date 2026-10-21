// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Feeding
{
    public static class YoungMealPacing
    {
        public const double FallbackSeconds = 600;

        public static double Interval(double adultSeconds) => adultSeconds > 0 && !double.IsInfinity(adultSeconds) && !double.IsNaN(adultSeconds)
            ? adultSeconds : FallbackSeconds;

        public static bool Due(long nowTicks, long lastMealTicks, double adultSeconds)
        {
            if (nowTicks <= 0) return false;
            if (lastMealTicks <= 0) return true;
            if (lastMealTicks > nowTicks) return false;
            return (nowTicks - lastMealTicks) / (double)TimeSpan.TicksPerSecond >= Interval(adultSeconds);
        }
    }
}
