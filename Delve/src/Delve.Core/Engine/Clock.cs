// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: one schedule for every kind (design B1, B2): a trigger is stamped with both the world day and the
// wall-clock time, so a kind can switch clocks mid-wait without losing its place.
using System;

namespace ExtraPotions.Delve.Engine
{
    /// <summary>
    /// A moment on both clocks: the world day (fractional, from the game's world time divided by the day length) and the
    /// wall-clock time in UTC. A component can be unknown (imported data, a world loaded before Delve): it starts at the
    /// first evaluation that completes it.
    /// </summary>
    public readonly struct Stamp
    {
        private Stamp(double day, DateTime utc, bool hasDay, bool hasUtc)
        {
            Day = day;
            Utc = utc;
            HasDay = hasDay;
            HasUtc = hasUtc;
        }

        public double Day { get; }
        public DateTime Utc { get; }
        public bool HasDay { get; }
        public bool HasUtc { get; }

        /// <summary>A complete stamp. Local times are converted to UTC; an unspecified kind is refused.</summary>
        public static Stamp At(double day, DateTime time)
        {
            if (double.IsNaN(day) || double.IsInfinity(day)) throw new ArgumentException("Expected a finite world day.", nameof(day));
            if (time.Kind == DateTimeKind.Unspecified) throw new ArgumentException("Expected a UTC or local time, not an unspecified one.", nameof(time));
            return new Stamp(day, time.ToUniversalTime(), true, true);
        }

        public static Stamp DayOnly(double day)
        {
            if (double.IsNaN(day) || double.IsInfinity(day)) throw new ArgumentException("Expected a finite world day.", nameof(day));
            return new Stamp(day, default, true, false);
        }

        public static Stamp UtcOnly(DateTime time)
        {
            if (time.Kind == DateTimeKind.Unspecified) throw new ArgumentException("Expected a UTC or local time, not an unspecified one.", nameof(time));
            return new Stamp(0, time.ToUniversalTime(), false, true);
        }

        /// <summary>Fills each unknown component from <paramref name="now"/> (the clock that was unknown starts now).</summary>
        public Stamp CompletedWith(Stamp now) => new Stamp(HasDay ? Day : now.Day, HasUtc ? Utc : now.Utc, HasDay || now.HasDay, HasUtc || now.HasUtc);

        public override string ToString() => (HasDay ? "day " + Day.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "day ?")
            + " / " + (HasUtc ? Utc.ToString("u", System.Globalization.CultureInfo.InvariantCulture) : "utc ?");
    }

    /// <summary>Due math for both clocks (design B1, section 4 clock ranges).</summary>
    public static class ClockRules
    {
        public const double MinDelay = 0.25;
        public const double MaxDays = 3650;
        public const double MaxHours = 87600;

        /// <summary>The delay within its clock's range; NaN or infinity gives <paramref name="fallback"/>.</summary>
        public static double Clamp(ClockKind clock, double delay, double fallback = 30)
        {
            if (double.IsNaN(delay) || double.IsInfinity(delay)) delay = fallback;
            var max = clock == ClockKind.RealHours ? MaxHours : MaxDays;
            return Math.Min(max, Math.Max(MinDelay, delay));
        }

        /// <summary>Time since <paramref name="since"/> in the clock's unit; 0 when either side lacks that clock's component.</summary>
        public static double Elapsed(ClockKind clock, Stamp since, Stamp now)
        {
            if (clock == ClockKind.RealHours)
                return since.HasUtc && now.HasUtc ? Math.Max(0, (now.Utc - since.Utc).TotalHours) : 0;
            return since.HasDay && now.HasDay ? Math.Max(0, now.Day - since.Day) : 0;
        }

        public static bool Due(ClockKind clock, double delay, Stamp since, Stamp now) =>
            Elapsed(clock, since, now) >= Clamp(clock, delay) - 1e-9;

        /// <summary>What is left of the delay, never negative.</summary>
        public static double Remaining(ClockKind clock, double delay, Stamp since, Stamp now) =>
            Math.Max(0, Clamp(clock, delay) - Elapsed(clock, since, now));

        /// <summary>A read-only clock snapshot; a missing selected-clock component stays unknown instead of starting a visit.</summary>
        public static (double? Remaining, bool Due) Inspect(ClockKind clock, double delay, Stamp? since, Stamp now, bool requested)
        {
            bool known = since.HasValue && (clock == ClockKind.RealHours
                ? since.Value.HasUtc && now.HasUtc : since.Value.HasDay && now.HasDay);
            return (known ? Remaining(clock, delay, since!.Value, now) : (double?)null,
                requested || known && Due(clock, delay, since!.Value, now));
        }

        /// <summary>Whole units for a notice ("in 6 days"): rounded up, 0 only when nothing is left.</summary>
        public static int WholeUnitsLeft(double remaining) => remaining <= 1e-9 ? 0 : (int)Math.Ceiling(remaining - 1e-9);
    }
}
