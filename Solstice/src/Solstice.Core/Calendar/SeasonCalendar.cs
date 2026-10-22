// Solstice - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from Seasons 1.10.6 by shudnal (https://github.com/shudnal/Seasons), commit 4054af6e8823d787921d673607e4942554872b66,
// Unlicense (public domain), SeasonState/SeasonState.cs and Seasons.cs. See THIRD_PARTY_NOTICES/Seasons-Unlicense.txt.
// Changed by expDARE, 2026: calendar arithmetic (GetDayOfYear, GetSeason, GetDayInSeason, GetYearLengthInDays, GetNightLength,
// DayStartFraction) lifted out of the game-bound SeasonState into pure functions of a world day and four season lengths;
// no Unity, no config entries, no per-world state. The reference quirks (day 0, the year wrap) are kept on purpose.
using System;

namespace ExtraPotions.Solstice.Calendar
{
    /// <summary>The four seasons, in calendar order (Seasons.Season).</summary>
    public enum Season { Spring = 0, Summer = 1, Fall = 2, Winter = 3 }

    public static class Seasons
    {
        public const int Count = 4;

        public static readonly Season[] All = { Season.Spring, Season.Summer, Season.Fall, Season.Winter };

        public static Season Next(Season season) => (Season)(((int)season + 1) % Count);

        public static Season Previous(Season season) => (Season)((Count + (int)season - 1) % Count);
    }

    /// <summary>Where a world day falls in the year.</summary>
    public readonly struct SeasonPosition : IEquatable<SeasonPosition>
    {
        public SeasonPosition(Season season, int dayInSeason, int dayOfYear, int daysInSeason)
        {
            Season = season;
            DayInSeason = dayInSeason;
            DayOfYear = dayOfYear;
            DaysInSeason = daysInSeason;
        }

        public Season Season { get; }

        /// <summary>1 to <see cref="DaysInSeason"/>; 0 only on world day 0 (before the first morning).</summary>
        public int DayInSeason { get; }

        /// <summary>1 to the year length; 0 only on world day 0.</summary>
        public int DayOfYear { get; }

        public int DaysInSeason { get; }

        public int DaysLeft => Math.Max(0, DaysInSeason - DayInSeason);

        public Season Next => Seasons.Next(Season);

        public bool Equals(SeasonPosition other) => Season == other.Season && DayInSeason == other.DayInSeason && DayOfYear == other.DayOfYear && DaysInSeason == other.DaysInSeason;

        public override bool Equals(object? obj) => obj is SeasonPosition other && Equals(other);

        public override int GetHashCode() => (((int)Season * 397 ^ DayInSeason) * 397 ^ DayOfYear) * 397 ^ DaysInSeason;
    }

    /// <summary>
    /// Four seasons of configurable length over a repeating year. A season is a pure function of the world day and the four lengths.
    /// A world with no saved clock is seeded from this function. After that the saved clock is the authority, so a length change does not
    /// move the season already underway. The day is the game's world day (<c>EnvMan.GetDay</c>: 1 on the first morning of a new world).
    /// </summary>
    public sealed class SeasonCalendar
    {
        public static readonly SeasonCalendar Default = new SeasonCalendar(10, 10, 10, 10);

        readonly int[] days;

        /// <summary>Lengths below one day count as one day (as in the reference).</summary>
        public SeasonCalendar(int spring, int summer, int fall, int winter)
        {
            days = new[] { Math.Max(1, spring), Math.Max(1, summer), Math.Max(1, fall), Math.Max(1, winter) };
            YearLength = days[0] + days[1] + days[2] + days[3];
        }

        public int YearLength { get; }

        public int DaysIn(Season season) => days[(int)season];

        /// <summary>
        /// Day of the year for a world day: the world day itself in the first year, then 1 to the year length, where the last day of a year is
        /// the year length and never 0 (GetDayOfYear). World day 0, and any earlier day, is day 0.
        /// </summary>
        public int DayOfYear(int day)
        {
            if (day < 0) day = 0;
            int dayOfYear = day % YearLength;
            return dayOfYear == day ? dayOfYear : (dayOfYear == 0 ? YearLength : dayOfYear);
        }

        public Season SeasonOf(int day)
        {
            int dayOfYear = DayOfYear(day);
            int total = 0;
            foreach (var season in Seasons.All)
            {
                total += DaysIn(season);
                if (dayOfYear <= total) return season;
            }
            return Season.Winter;
        }

        public int DayInSeason(int day)
        {
            int dayOfYear = DayOfYear(day);
            int total = 0;
            int length = 0;
            foreach (var season in Seasons.All)
            {
                length = DaysIn(season);
                if (dayOfYear <= total + length) return dayOfYear - total;
                total += length;
            }
            return dayOfYear >= total ? length : dayOfYear - total;
        }

        public SeasonPosition At(int day)
        {
            var season = SeasonOf(day);
            return new SeasonPosition(season, DayInSeason(day), DayOfYear(day), DaysIn(season));
        }
    }

    /// <summary>The night length of a season, gradual or fixed, and the day start it implies (SeasonState.GetNightLength, DayStartFraction).</summary>
    public static class NightLength
    {
        /// <summary>
        /// Night length in percent of a day for a day of a season. Fixed: the season's own length. Gradual: the season's length holds in the
        /// middle of the season and moves linearly to the neighbouring season's length from there, rounding halves to even.
        /// </summary>
        public static int For(SeasonCalendar calendar, Func<Season, int> nightOf, Season season, int dayInSeason, bool gradual)
        {
            if (calendar == null) throw new ArgumentNullException(nameof(calendar));
            if (nightOf == null) throw new ArgumentNullException(nameof(nightOf));
            int current = nightOf(season);
            if (!gradual) return current;

            int daysInSeason = calendar.DaysIn(season);
            float currentPeakDay = daysInSeason / 2f;
            int lastPeakDay = (int)Math.Ceiling(currentPeakDay);
            int firstPeakDay = (int)Math.Floor(currentPeakDay);

            if (dayInSeason == firstPeakDay || dayInSeason == lastPeakDay) return current;
            if (dayInSeason < firstPeakDay)
            {
                var previous = Seasons.Previous(season);
                int daysInPreviousSeason = calendar.DaysIn(previous);
                lastPeakDay = (int)Math.Ceiling(daysInPreviousSeason / 2f);
                int daysInPrevious = daysInPreviousSeason - lastPeakDay;
                int previousNight = nightOf(previous);
                return Round(Lerp(previousNight, current, (float)(dayInSeason + daysInPrevious) / (firstPeakDay + daysInPrevious)));
            }
            if (dayInSeason > lastPeakDay)
            {
                var next = Seasons.Next(season);
                int daysInNextSeason = calendar.DaysIn(next);
                firstPeakDay = (int)Math.Floor(daysInNextSeason / 2f);
                int daysLeft = daysInSeason - lastPeakDay;
                int nextNight = nightOf(next);
                return Round(Lerp(current, nextNight, (float)(dayInSeason - lastPeakDay) / (firstPeakDay + daysLeft)));
            }
            return current;
        }

        /// <summary>Where the day starts, as a fraction of the day: half the night before the morning (night length is in percent).</summary>
        public static float DayStartFraction(int nightLength) => nightLength / 2f / 100f;

        static float Lerp(float a, float b, float t) => a + (b - a) * Math.Min(1f, Math.Max(0f, t));

        static int Round(float value) => (int)Math.Round(value);
    }
}
