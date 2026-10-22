// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the saved world-day clock. SeasonCalendar remains the pure function used to seed a world that has no clock.
using System;
using System.Globalization;

namespace ExtraPotions.Solstice.Calendar
{
    /// <summary>Why a clock text was accepted or refused.</summary>
    public enum ClockParse { Ok, Missing, Corrupt, Newer }

    /// <summary>
    /// The season the world is in, saved with the world and advanced by world days only.
    /// <see cref="AnchorDay"/> is the world day on which the current season's day-in-season is 0
    /// (the day before its first morning, which is world day 0 for a new world's spring).
    /// Lengths are the ones in force. The Season Override cheat is not stored here.
    /// </summary>
    public readonly struct SeasonClock : IEquatable<SeasonClock>
    {
        public const int FormatVersion = 1;

        public SeasonClock(int anchorDay, Season season, int spring, int summer, int fall, int winter, int worldDay, int revision)
        {
            AnchorDay = anchorDay;
            Season = season;
            Spring = spring;
            Summer = summer;
            Fall = fall;
            Winter = winter;
            WorldDay = worldDay;
            Revision = revision;
        }

        public int AnchorDay { get; }
        public Season Season { get; }
        public int Spring { get; }
        public int Summer { get; }
        public int Fall { get; }
        public int Winter { get; }
        public int WorldDay { get; }
        public int Revision { get; }

        public int YearLength => Spring + Summer + Fall + Winter;

        public int DayInSeason => WorldDay - AnchorDay;

        public int DaysIn(Season season)
        {
            switch (season)
            {
                case Season.Spring: return Spring;
                case Season.Summer: return Summer;
                case Season.Fall: return Fall;
                case Season.Winter: return Winter;
                default: return Spring;
            }
        }

        /// <summary>1 to the year length, or 0 only when the clock is still on world day 0.</summary>
        public int DayOfYear
        {
            get
            {
                int before = 0;
                foreach (var season in Seasons.All)
                {
                    if (season == Season) break;
                    before += DaysIn(season);
                }
                return before + DayInSeason;
            }
        }

        public SeasonCalendar Calendar => new SeasonCalendar(Spring, Summer, Fall, Winter);

        public bool Equals(SeasonClock other) =>
            AnchorDay == other.AnchorDay && Season == other.Season && Spring == other.Spring && Summer == other.Summer
            && Fall == other.Fall && Winter == other.Winter && WorldDay == other.WorldDay && Revision == other.Revision;

        public override bool Equals(object? obj) => obj is SeasonClock other && Equals(other);

        public override int GetHashCode() =>
            ((((((((AnchorDay * 397 ^ (int)Season) * 397 ^ Spring) * 397 ^ Summer) * 397 ^ Fall) * 397 ^ Winter) * 397 ^ WorldDay) * 397) ^ Revision);
    }

    /// <summary>Seeds and advances a <see cref="SeasonClock"/> from world days and the live season lengths.</summary>
    public static class SeasonClocks
    {
        const int MaximumSteps = 100000;

        /// <summary>A new world, or a world with no clock: the same season <see cref="SeasonCalendar.At"/> would report.</summary>
        public static SeasonClock Seed(int worldDay, SeasonCalendar lengths)
        {
            if (lengths == null) throw new ArgumentNullException(nameof(lengths));
            if (worldDay < 0) worldDay = 0;
            var position = lengths.At(worldDay);
            return new SeasonClock(worldDay - position.DayInSeason, position.Season,
                lengths.DaysIn(Season.Spring), lengths.DaysIn(Season.Summer), lengths.DaysIn(Season.Fall), lengths.DaysIn(Season.Winter),
                worldDay, 1);
        }

        /// <summary>
        /// Moves <paramref name="saved"/> to <paramref name="worldDay"/> using <paramref name="lengths"/>.
        /// The current season keeps its start day. Live lengths replace the stored ones, so a shorter current season can end
        /// on this morning and later seasons use the new lengths. A world day before the start walks backward. A clock that
        /// cannot be walked is seeded again and its revision still increases.
        /// </summary>
        public static SeasonClock Reconcile(SeasonClock saved, int worldDay, SeasonCalendar lengths)
        {
            if (lengths == null) throw new ArgumentNullException(nameof(lengths));
            if (worldDay < 0) worldDay = 0;
            int revision = saved.Revision < 1 ? 1 : saved.Revision;
            if (!Enum.IsDefined(typeof(Season), saved.Season)) return Bump(Seed(worldDay, lengths), revision);

            int anchor = saved.AnchorDay;
            var season = saved.Season;
            int steps = 0;
            // Day 0 of a season is only the world's first day. Landing on a later season's start day is the previous season's last day.
            while ((worldDay < anchor || (worldDay == anchor && worldDay > 0)) && steps++ < MaximumSteps)
            {
                season = Seasons.Previous(season);
                int previous = lengths.DaysIn(season);
                if (anchor < previous) return Bump(Seed(worldDay, lengths), revision);
                anchor -= previous;
            }
            if (anchor < 0 || worldDay < anchor) return Bump(Seed(worldDay, lengths), revision);

            while (worldDay - anchor > lengths.DaysIn(season) && steps++ < MaximumSteps)
            {
                anchor += lengths.DaysIn(season);
                season = Seasons.Next(season);
            }
            if (steps > MaximumSteps || worldDay - anchor > lengths.DaysIn(season)) return Bump(Seed(worldDay, lengths), revision);

            var next = new SeasonClock(anchor, season,
                lengths.DaysIn(Season.Spring), lengths.DaysIn(Season.Summer), lengths.DaysIn(Season.Fall), lengths.DaysIn(Season.Winter),
                worldDay, revision);
            if (next.AnchorDay == saved.AnchorDay && next.Season == saved.Season && next.Spring == saved.Spring && next.Summer == saved.Summer
                && next.Fall == saved.Fall && next.Winter == saved.Winter && next.WorldDay == saved.WorldDay)
                return next;
            return new SeasonClock(next.AnchorDay, next.Season, next.Spring, next.Summer, next.Fall, next.Winter, next.WorldDay, NextRevision(revision));
        }

        static SeasonClock Bump(SeasonClock seeded, int previous) =>
            new SeasonClock(seeded.AnchorDay, seeded.Season, seeded.Spring, seeded.Summer, seeded.Fall, seeded.Winter, seeded.WorldDay, NextRevision(previous));

        static int NextRevision(int revision) => revision >= int.MaxValue || revision < 1 ? 1 : revision + 1;
    }

    /// <summary>
    /// Clock text for <see cref="SeasonClockKey"/>. Lowercase digits, dots and the version token, so a global key that is
    /// lowercased and split on the first space still round-trips. <c>s1.&lt;anchor&gt;.&lt;season&gt;.&lt;spring&gt;.&lt;summer&gt;.&lt;fall&gt;.&lt;winter&gt;.&lt;world day&gt;.&lt;revision&gt;</c>
    /// </summary>
    public static class SeasonClockText
    {
        public const string Version = "s1";
        public const int MaximumLength = 128;

        public static string Format(SeasonClock clock) =>
            string.Join(".", Version,
                clock.AnchorDay.ToString(CultureInfo.InvariantCulture),
                ((int)clock.Season).ToString(CultureInfo.InvariantCulture),
                clock.Spring.ToString(CultureInfo.InvariantCulture),
                clock.Summer.ToString(CultureInfo.InvariantCulture),
                clock.Fall.ToString(CultureInfo.InvariantCulture),
                clock.Winter.ToString(CultureInfo.InvariantCulture),
                clock.WorldDay.ToString(CultureInfo.InvariantCulture),
                clock.Revision.ToString(CultureInfo.InvariantCulture));

        /// <summary>The canonical text, or null when <paramref name="text"/> is not a version-1 clock.</summary>
        public static string? Canonical(string? text) =>
            TryParse(text, out var clock, out var parse) && parse == ClockParse.Ok ? Format(clock) : null;

        public static bool TryParse(string? text, out SeasonClock clock, out ClockParse parse)
        {
            clock = default;
            if (string.IsNullOrEmpty(text))
            {
                parse = ClockParse.Missing;
                return false;
            }
            if (text!.Length > MaximumLength || !Allowed(text))
            {
                parse = ClockParse.Corrupt;
                return false;
            }
            var parts = text.Split('.');
            if (parts.Length < 1 || !parts[0].StartsWith("s", StringComparison.Ordinal) || parts[0].Length < 2
                || !int.TryParse(parts[0].Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            {
                parse = ClockParse.Corrupt;
                return false;
            }
            if (version > SeasonClock.FormatVersion)
            {
                parse = ClockParse.Newer;
                return false;
            }
            if (version != SeasonClock.FormatVersion || parts.Length != 9
                || !Int(parts[1], out var anchor) || !Int(parts[2], out var season) || !Int(parts[3], out var spring)
                || !Int(parts[4], out var summer) || !Int(parts[5], out var fall) || !Int(parts[6], out var winter)
                || !Int(parts[7], out var worldDay) || !Int(parts[8], out var revision)
                || season > 3 || spring < 1 || summer < 1 || fall < 1 || winter < 1 || revision < 1 || worldDay < anchor)
            {
                parse = ClockParse.Corrupt;
                return false;
            }
            clock = new SeasonClock(anchor, (Season)season, spring, summer, fall, winter, worldDay, revision);
            if (clock.DayInSeason > clock.DaysIn(clock.Season))
            {
                clock = default;
                parse = ClockParse.Corrupt;
                return false;
            }
            parse = ClockParse.Ok;
            return true;
        }

        static bool Int(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

        static bool Allowed(string text)
        {
            foreach (var c in text)
                if (!(c >= '0' && c <= '9') && c != '.' && c != 's') return false;
            return true;
        }
    }

    /// <summary>The guarded world-key name. Not an <c>mw.</c> setting players edit; Crucible stores it with the world.</summary>
    public static class SeasonClockKey
    {
        public const string Value = "mw.solstice.clock";
    }
}
