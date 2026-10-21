// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the engine's "now" from the game's world clock (design B1, B2). The plugin passes
// ZNet.GetTimeSeconds, EnvMan.m_dayLengthSec and DateTime.UtcNow; dawn is where the game puts the morning
// (EnvMan.GetMorningStartSec: 15 percent into a day).
using System;

namespace ExtraPotions.Delve.Engine
{
    public static class WorldClock
    {
        /// <summary>Fraction of a day at which the game's morning starts.</summary>
        public const double DawnFraction = 0.15;

        /// <summary>Both clocks now: world seconds over the day length, and the wall clock.</summary>
        public static Stamp Now(double worldSeconds, double dayLengthSeconds, DateTime utcNow)
        {
            Check(dayLengthSeconds);
            return Stamp.At(Math.Max(0, worldSeconds) / dayLengthSeconds, utcNow);
        }

        /// <summary>The game's whole day number (EnvMan.GetDay).</summary>
        public static int DayIndex(double worldSeconds, double dayLengthSeconds)
        {
            Check(dayLengthSeconds);
            return (int)Math.Floor(Math.Max(0, worldSeconds) / dayLengthSeconds);
        }

        /// <summary>A dawn lies in (<paramref name="previousSeconds"/>, <paramref name="nowSeconds"/>]; time going back is none.</summary>
        public static bool DawnBetween(double previousSeconds, double nowSeconds, double dayLengthSeconds)
        {
            Check(dayLengthSeconds);
            if (!(nowSeconds > previousSeconds)) return false;
            double offset = DawnFraction * dayLengthSeconds;
            double firstAfter = Math.Floor((previousSeconds - offset) / dayLengthSeconds + 1) * dayLengthSeconds + offset;
            return firstAfter <= nowSeconds;
        }

        /// <summary>Day-clock kinds sweep at dawn, real-hour kinds at every check, any kind at once on regenerate now.</summary>
        public static bool ShouldSweep(ClockKind clock, bool dawn, bool regenerateNow) =>
            regenerateNow || clock == ClockKind.RealHours || dawn;

        /// <summary>
        /// A time the game stores as <c>ZNet.GetTime().Ticks</c> (world seconds times 10^7, such as a spawner's alive
        /// time) as a day-only stamp; 0 or less is unknown and starts at the first evaluation.
        /// </summary>
        public static Stamp FromGameTicks(long ticks, double dayLengthSeconds)
        {
            Check(dayLengthSeconds);
            if (ticks <= 0) return default;
            return Stamp.DayOnly(ticks / 1e7 / dayLengthSeconds);
        }

        private static void Check(double dayLengthSeconds)
        {
            if (!(dayLengthSeconds > 0) || double.IsInfinity(dayLengthSeconds)) throw new ArgumentException("Expected a positive day length.", nameof(dayLengthSeconds));
        }
    }
}
