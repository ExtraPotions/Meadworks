// Solstice - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from Seasons 1.10.6 by shudnal (https://github.com/shudnal/Seasons), commit 4054af6e8823d787921d673607e4942554872b66,
// Unlicense (public domain), SeasonState/SeasonStatePatches.cs (Plant_TimeSincePlanted_PlantsGrowthMultiplier) and
// SeasonState/SeasonState.cs (GetStartOfCurrentSeason). See THIRD_PARTY_NOTICES/Seasons-Unlicense.txt.
// Changed by expDARE, 2026: the walk back through the seasons is a pure function of the elapsed time, the current time, the day length,
// the calendar and a multiplier per season; the history uses today's season lengths; a long walk is bounded.
// ScaleAt takes the saved clock's season and day so a length change does not remap growth already in progress.
using System;

namespace ExtraPotions.Solstice.Calendar
{
    /// <summary>Time weighted by the seasons it passed through.</summary>
    public static class SeasonTime
    {
        const int MaximumSteps = 100000;

        /// <summary>The time (seconds) the current season started at: the start of the world day, back to the season's first day.</summary>
        public static double StartOfSeason(double now, double dayLengthSeconds, SeasonCalendar calendar)
        {
            if (calendar == null) throw new ArgumentNullException(nameof(calendar));
            int worldDay = (int)(now / dayLengthSeconds);
            var position = calendar.At(worldDay);
            return worldDay * dayLengthSeconds - Math.Max(0, position.DayInSeason - 1) * dayLengthSeconds;
        }

        /// <summary>
        /// The effective time of <paramref name="elapsed"/> seconds that ended at <paramref name="now"/>: each stretch spent in a season counts
        /// with that season's multiplier. Walks back one season at a time. Bad input (no day length, elapsed time that is negative or not a number)
        /// gives the elapsed time unchanged, or 0 for no or negative elapsed time.
        /// </summary>
        public static double Scale(double elapsed, double now, double dayLengthSeconds, SeasonCalendar calendar, Func<Season, double> multiplierOf)
        {
            if (calendar == null) throw new ArgumentNullException(nameof(calendar));
            if (dayLengthSeconds <= 0 || double.IsNaN(now) || double.IsInfinity(now))
                return ScaleAt(elapsed, now, dayLengthSeconds, calendar, multiplierOf, Season.Spring, 0);
            int worldDay = (int)(now / dayLengthSeconds);
            var position = calendar.At(worldDay);
            return ScaleAt(elapsed, now, dayLengthSeconds, calendar, multiplierOf, position.Season, position.DayInSeason);
        }

        /// <summary>
        /// <see cref="Scale"/> for a saved clock: <paramref name="season"/> and <paramref name="dayInSeason"/> are the clock's,
        /// not the season a pure remap of the world day would give after a length change.
        /// </summary>
        public static double ScaleAt(double elapsed, double now, double dayLengthSeconds, SeasonCalendar calendar, Func<Season, double> multiplierOf, Season season, int dayInSeason)
        {
            if (calendar == null) throw new ArgumentNullException(nameof(calendar));
            if (multiplierOf == null) throw new ArgumentNullException(nameof(multiplierOf));
            if (double.IsNaN(elapsed) || elapsed <= 0) return 0;
            if (dayLengthSeconds <= 0 || double.IsNaN(now) || double.IsInfinity(now) || double.IsInfinity(elapsed)) return elapsed;
            if (dayInSeason < 0) dayInSeason = 0;

            int worldDay = (int)(now / dayLengthSeconds);
            double start = worldDay * dayLengthSeconds - Math.Max(0, dayInSeason - 1) * dayLengthSeconds;
            double remaining = elapsed;
            double scaled = 0;
            for (int step = 0; step < MaximumSteps; step++)
            {
                double span = now - start;
                scaled += (span >= remaining ? remaining : span) * multiplierOf(season);
                remaining -= span;
                if (remaining <= 0) return scaled;
                now = start;
                season = Seasons.Previous(season);
                start -= calendar.DaysIn(season) * dayLengthSeconds;
            }
            return scaled + remaining * YearAverage(calendar, multiplierOf);
        }

        static double YearAverage(SeasonCalendar calendar, Func<Season, double> multiplierOf)
        {
            double total = 0;
            foreach (var season in Seasons.All) total += calendar.DaysIn(season) * multiplierOf(season);
            return total / calendar.YearLength;
        }
    }
}
