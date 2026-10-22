// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: meadworks.seasons.clock 1.0 and meadworks.seasons.facts 1.0. view 1.0 is unchanged.
using System;
using System.Globalization;
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Integration
{
    /// <summary>
    /// The saved clock, without the Season Override cheat. Capability <c>meadworks.seasons.clock</c> 1.0 is
    /// <c>Func&lt;(int, int, int, int, int, int, int, int)&gt;</c>: season, day in the season, season length, day of the year,
    /// year length, world day, anchor day, revision. Eight ints do not fit <c>System.Tuple</c> on this runtime (its eighth slot is a nested tuple),
    /// so this one is a value tuple. All zero means the clock has not arrived. Main thread only.
    /// </summary>
    public readonly struct SeasonClockView : IEquatable<SeasonClockView>
    {
        public const string Id = "meadworks.seasons.clock";
        public static readonly Version ContractVersion = new Version(1, 0);

        public SeasonClockView(int season, int dayInSeason, int daysInSeason, int dayOfYear, int yearLength, int worldDay, int anchorDay, int revision)
        {
            Season = season;
            DayInSeason = dayInSeason;
            DaysInSeason = daysInSeason;
            DayOfYear = dayOfYear;
            YearLength = yearLength;
            WorldDay = worldDay;
            AnchorDay = anchorDay;
            Revision = revision;
        }

        public int Season { get; }
        public int DayInSeason { get; }
        public int DaysInSeason { get; }
        public int DayOfYear { get; }
        public int YearLength { get; }
        public int WorldDay { get; }
        public int AnchorDay { get; }
        public int Revision { get; }

        public bool Available => DaysInSeason > 0;

        public static SeasonClockView FromClock(SeasonClock clock) =>
            new SeasonClockView((int)clock.Season, clock.DayInSeason, clock.DaysIn(clock.Season), clock.DayOfYear, clock.YearLength, clock.WorldDay, clock.AnchorDay, clock.Revision);

        public (int, int, int, int, int, int, int, int) ToTuple() =>
            (Season, DayInSeason, DaysInSeason, DayOfYear, YearLength, WorldDay, AnchorDay, Revision);

        public static bool TryFromTuple((int, int, int, int, int, int, int, int) tuple, out SeasonClockView view)
        {
            view = default;
            int season = tuple.Item1, day = tuple.Item2, length = tuple.Item3, dayOfYear = tuple.Item4, year = tuple.Item5;
            int worldDay = tuple.Item6, anchor = tuple.Item7, revision = tuple.Item8;
            if (length == 0 && day == 0 && year == 0 && worldDay == 0 && anchor == 0 && revision == 0 && season == 0 && dayOfYear == 0)
            {
                view = default;
                return true;
            }
            if (season < 0 || season >= Seasons.Count) return false;
            if (length < 1 || year < length || day < 0 || day > length || dayOfYear < 0 || dayOfYear > year) return false;
            if (worldDay < anchor || worldDay - anchor != day) return false;
            view = new SeasonClockView(season, day, length, dayOfYear, year, worldDay, anchor, revision);
            return true;
        }

        public bool Equals(SeasonClockView other) => Season == other.Season && DayInSeason == other.DayInSeason && DaysInSeason == other.DaysInSeason
            && DayOfYear == other.DayOfYear && YearLength == other.YearLength && WorldDay == other.WorldDay && AnchorDay == other.AnchorDay && Revision == other.Revision;

        public override bool Equals(object? obj) => obj is SeasonClockView other && Equals(other);

        public override int GetHashCode() =>
            ((((((((Season * 397 ^ DayInSeason) * 397 ^ DaysInSeason) * 397 ^ DayOfYear) * 397 ^ YearLength) * 397 ^ WorldDay) * 397 ^ AnchorDay) * 397) ^ Revision);
    }

    /// <summary>Numbers other mods may read. Empty means Solstice has no clock yet.</summary>
    public readonly struct SeasonFactValues
    {
        public SeasonFactValues(bool available, int season, int calendarSeason, int day, int length, int dayOfYear, int year, int worldDay, int anchor, int revision,
            bool held, bool effects, int nightPercent, float dayStart, float wood, float meat, float breed, bool torch, float torchDrain, bool overheat,
            float growth, float snow, bool ice)
        {
            Available = available;
            Season = season;
            CalendarSeason = calendarSeason;
            Day = day;
            Length = length;
            DayOfYear = dayOfYear;
            Year = year;
            WorldDay = worldDay;
            Anchor = anchor;
            Revision = revision;
            Held = held;
            Effects = effects;
            NightPercent = nightPercent;
            DayStart = dayStart;
            Wood = wood;
            Meat = meat;
            Breed = breed;
            Torch = torch;
            TorchDrain = torchDrain;
            Overheat = overheat;
            Growth = growth;
            Snow = snow;
            Ice = ice;
        }

        public bool Available { get; }
        public int Season { get; }
        public int CalendarSeason { get; }
        public int Day { get; }
        public int Length { get; }
        public int DayOfYear { get; }
        public int Year { get; }
        public int WorldDay { get; }
        public int Anchor { get; }
        public int Revision { get; }
        public bool Held { get; }
        public bool Effects { get; }
        public int NightPercent { get; }
        public float DayStart { get; }
        public float Wood { get; }
        public float Meat { get; }
        public float Breed { get; }
        public bool Torch { get; }
        public float TorchDrain { get; }
        public bool Overheat { get; }
        public float Growth { get; }
        public float Snow { get; }
        public bool Ice { get; }
    }

    /// <summary>
    /// Capability <c>meadworks.seasons.facts</c> 1.0 is <c>Func&lt;string&gt;</c>. Empty means unavailable.
    /// Keys are lowercase, separated by semicolons, values after one equals sign. Unknown keys are ignored.
    /// <c>season</c> is the season players are in, including the Season Override cheat. <c>calendar</c> is the saved season.
    /// <c>effects=0</c> means the server turned Solstice's own effects off; consumers should not apply seasonal rates either.
    /// </summary>
    public static class SeasonFacts
    {
        public const string Id = "meadworks.seasons.facts";
        public static readonly Version ContractVersion = new Version(1, 0);

        public static string Format(SeasonFactValues facts)
        {
            if (!facts.Available) return "";
            var culture = CultureInfo.InvariantCulture;
            return string.Join(";",
                "season=" + facts.Season.ToString(culture),
                "calendar=" + facts.CalendarSeason.ToString(culture),
                "day=" + facts.Day.ToString(culture),
                "length=" + facts.Length.ToString(culture),
                "doy=" + facts.DayOfYear.ToString(culture),
                "year=" + facts.Year.ToString(culture),
                "world=" + facts.WorldDay.ToString(culture),
                "anchor=" + facts.Anchor.ToString(culture),
                "rev=" + facts.Revision.ToString(culture),
                "held=" + (facts.Held ? "1" : "0"),
                "effects=" + (facts.Effects ? "1" : "0"),
                "night=" + facts.NightPercent.ToString(culture),
                "daystart=" + facts.DayStart.ToString("0.###", culture),
                "wood=" + facts.Wood.ToString("0.###", culture),
                "meat=" + facts.Meat.ToString("0.###", culture),
                "breed=" + facts.Breed.ToString("0.###", culture),
                "torch=" + (facts.Torch ? "1" : "0"),
                "torchdrain=" + facts.TorchDrain.ToString("0.###", culture),
                "overheat=" + (facts.Overheat ? "1" : "0"),
                "growth=" + facts.Growth.ToString("0.###", culture),
                "snow=" + facts.Snow.ToString("0.###", culture),
                "ice=" + (facts.Ice ? "1" : "0"));
        }

        public static bool TryGet(string? text, string key, out string value)
        {
            value = "";
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(key)) return false;
            foreach (var part in text!.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                if (!string.Equals(part.Substring(0, eq), key, StringComparison.Ordinal)) continue;
                value = part.Substring(eq + 1);
                return true;
            }
            return false;
        }
    }
}
