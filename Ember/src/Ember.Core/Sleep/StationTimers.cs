// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT. Adapted from BetterSleepBruh 2.1.0 at
// fe2bb81866e58f3c4afeb832876cba6ebf1e12cd: BetterSleepBruh/Patches/SmelterPatches.cs and CookingStationPatches.cs
// (GetDeltaTime postfix), Patches/FermenterPatches.cs, PlantPatches.cs and PickablePatches.cs (boost total shift),
// Components/SleepTracker.cs (SaveData, LoadData). The MIT notice is retained in THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: the math is pure and testable; non-finite and negative values are ignored; the total is
// clamped; the saved file is per world id instead of per world name; namespace Ember.
using System;
using System.Globalization;

namespace ExtraPotions.Ember.Sleep
{
    /// <summary>What to do with one timer: write the marker, and how many ticks to move its start time forward.</summary>
    public readonly struct TimerShift
    {
        public TimerShift(bool mark, long marker, long shift)
        {
            Mark = mark;
            Marker = marker;
            Shift = shift;
        }

        public bool Mark { get; }
        public long Marker { get; }
        public long Shift { get; }
    }

    /// <summary>
    /// Station timers during partial sleep. Delta-time stations (cooking stations, ovens, smelters, kilns) divide their
    /// step by the speed-up; start-time stations (fermenters, crops, pickables) move their start time forward by the
    /// boost added since they last looked, so they progress at normal speed while the night is accelerated.
    /// </summary>
    public static class StationTimers
    {
        public const double TicksPerSecond = 10000000.0;

        /// <summary>Upper bound of the boost total in game seconds (about 31,700 years).</summary>
        public const double MaxBoostTotal = 1e12;

        /// <summary>Delta-time stations: the step at normal speed while the extra rate is positive.</summary>
        public static double ScaleDelta(double dt, double extraRate) =>
            extraRate > 0.0 && !double.IsInfinity(extraRate) ? dt / (1.0 + extraRate) : dt;

        /// <summary>Game seconds to DateTime ticks, clamped to 0 .. MaxBoostTotal.</summary>
        public static long ToTicks(double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0.0) return 0L;
            return (long)(Math.Min(seconds, MaxBoostTotal) * TicksPerSecond);
        }

        /// <summary>Start-time stations: the decision for the current total and the timer's marker (-1 when unset).</summary>
        public static TimerShift Shift(long totalTicks, long lastChecked)
        {
            if (lastChecked < 0L) return new TimerShift(true, totalTicks, 0L);
            if (totalTicks > lastChecked) return new TimerShift(true, totalTicks, totalTicks - lastChecked);
            if (lastChecked > totalTicks) return new TimerShift(true, totalTicks, 0L);
            return new TimerShift(false, lastChecked, 0L);
        }

        /// <summary>Adds one applied boost step to the total; non-finite or negative steps are ignored.</summary>
        public static double AddBoost(double total, double applied)
        {
            if (double.IsNaN(total) || double.IsInfinity(total) || total < 0.0) total = 0.0;
            if (double.IsNaN(applied) || double.IsInfinity(applied) || applied <= 0.0) return Math.Min(total, MaxBoostTotal);
            return Math.Min(total + applied, MaxBoostTotal);
        }

        public static string FormatTotal(double total) => AddBoost(total, 0.0).ToString("R", CultureInfo.InvariantCulture);

        public static bool TryParseTotal(string? text, out double total)
        {
            total = 0.0;
            if (text == null) return false;
            if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
                return false;
            total = Math.Min(value, MaxBoostTotal);
            return true;
        }

        /// <summary>The authority's saved total for one world.</summary>
        public static string FileName(long worldId) => "sleep-boost-" + worldId.ToString(CultureInfo.InvariantCulture) + ".txt";
    }
}
