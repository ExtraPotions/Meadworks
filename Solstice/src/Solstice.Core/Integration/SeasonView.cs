// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Solstice's reading for the Crucible capability meadworks.seasons.view 1.0, shaped like Tempest's WeatherEventsView.
using System;
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Integration
{
    /// <summary>
    /// One reading of the season, for Cairn, Tempest and any other consumer. The capability <c>meadworks.seasons.view</c> 1.0 has the BCL
    /// shape <c>Func&lt;Tuple&lt;int,int,int,int,int,int&gt;&gt;</c> with items: season (0 Spring, 1 Summer, 2 Fall, 3 Winter), day in the
    /// season (1 to the season length), season length in days, day of the year, year length in days, and a revision that the provider
    /// bumps whenever any of the other five change. All zero means unavailable (Solstice off for the session). Consumers call the delegate
    /// on the Unity main thread only. The shape needs no Crucible.Contracts type, so no consumer needs a newer Crucible than 0.3.12; a
    /// typed contract in Crucible.Contracts is an open question (DEVELOPMENT.md).
    /// </summary>
    public readonly struct SeasonView : IEquatable<SeasonView>
    {
        public const string Id = "meadworks.seasons.view";
        public static readonly Version ContractVersion = new Version(1, 0);

        public SeasonView(int season, int dayInSeason, int daysInSeason, int dayOfYear, int yearLength, int revision)
        {
            Season = season;
            DayInSeason = dayInSeason;
            DaysInSeason = daysInSeason;
            DayOfYear = dayOfYear;
            YearLength = yearLength;
            Revision = revision;
        }

        public int Season { get; }
        public int DayInSeason { get; }
        public int DaysInSeason { get; }
        public int DayOfYear { get; }
        public int YearLength { get; }
        public int Revision { get; }

        public bool Available => DaysInSeason > 0;

        /// <summary>
        /// The reading for a world day. A <paramref name="forced"/> season (the Season Override cheat) replaces the season and keeps the day
        /// of the natural position, limited to the length of the forced season.
        /// </summary>
        public static SeasonView Read(SeasonCalendar calendar, int day, Season? forced = null)
        {
            if (calendar == null) throw new ArgumentNullException(nameof(calendar));
            var position = calendar.At(day);
            var season = forced ?? position.Season;
            int length = calendar.DaysIn(season);
            return new SeasonView((int)season, Math.Min(position.DayInSeason, length), length, position.DayOfYear, calendar.YearLength, 0);
        }

        /// <summary>The reading for a saved clock. A forced season replaces the season and keeps the day, limited to that season's length.</summary>
        public static SeasonView FromClock(SeasonClock clock, Season? forced = null)
        {
            var season = forced ?? clock.Season;
            int length = clock.DaysIn(season);
            int day = Math.Min(Math.Max(0, clock.DayInSeason), length);
            return new SeasonView((int)season, day, length, clock.DayOfYear, clock.YearLength, 0);
        }

        public SeasonView WithRevision(int revision) => new SeasonView(Season, DayInSeason, DaysInSeason, DayOfYear, YearLength, revision);

        public Tuple<int, int, int, int, int, int> ToTuple() => Tuple.Create(Season, DayInSeason, DaysInSeason, DayOfYear, YearLength, Revision);

        public static SeasonView FromTuple(Tuple<int, int, int, int, int, int> tuple)
        {
            if (!TryFromTuple(tuple, out var view)) throw new ArgumentException("Invalid season reading.", nameof(tuple));
            return view;
        }

        /// <summary>Reads a provider reply; a null or out-of-range reply gives false and the unavailable reading.</summary>
        public static bool TryFromTuple(Tuple<int, int, int, int, int, int>? tuple, out SeasonView view)
        {
            view = default;
            if (tuple == null) return false;
            int season = tuple.Item1, day = tuple.Item2, length = tuple.Item3, dayOfYear = tuple.Item4, year = tuple.Item5;
            if (season < 0 || season >= Seasons.Count) return false;
            if (length < 0 || year < length || day < 0 || day > length || dayOfYear < 0 || dayOfYear > year) return false;
            view = new SeasonView(season, day, length, dayOfYear, year, tuple.Item6);
            return true;
        }

        public bool Equals(SeasonView other) => Season == other.Season && DayInSeason == other.DayInSeason && DaysInSeason == other.DaysInSeason
            && DayOfYear == other.DayOfYear && YearLength == other.YearLength && Revision == other.Revision;

        public override bool Equals(object? obj) => obj is SeasonView other && Equals(other);

        public override int GetHashCode() => ((((Season * 397 ^ DayInSeason) * 397 ^ DaysInSeason) * 397 ^ DayOfYear) * 397 ^ YearLength) * 397 ^ Revision;
    }

    /// <summary>The capability's change counter: bumps when the season, the day or the calendar differ from the last reading.</summary>
    public sealed class SeasonViewRevision
    {
        int revision;
        bool seen;
        SeasonView last;

        public int Observe(SeasonView view)
        {
            var plain = view.WithRevision(0);
            if (!seen || !plain.Equals(last))
            {
                seen = true;
                last = plain;
                revision = unchecked(revision + 1);
            }
            return revision;
        }

        public SeasonView Stamp(SeasonView view) => view.WithRevision(Observe(view));
    }
}
