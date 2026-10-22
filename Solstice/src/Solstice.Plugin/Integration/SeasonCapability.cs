// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: provider of meadworks.seasons.view 1.0 and the diagnostics lines.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Integration;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Provides <c>meadworks.seasons.view</c> 1.0 to Cairn, Tempest and any other mod: season, day in the season, season length, day of the
    /// year, year length and a revision, as the BCL tuple described on <see cref="SeasonView"/>. All zero while Solstice is off for the
    /// session. Read on the Unity main thread only.
    /// </summary>
    public static class SeasonCapability
    {
        public static void Register(ProductRegistration registration)
        {
            registration.AddCapability<Func<Tuple<int, int, int, int, int, int>>>(SeasonView.Id, SeasonView.ContractVersion, Provide);
            registration.AddCapability<Func<(int, int, int, int, int, int, int, int)>>(SeasonClockView.Id, SeasonClockView.ContractVersion, Clock);
            registration.AddCapability<Func<string>>(SeasonFacts.Id, SeasonFacts.ContractVersion, Facts);
            registration.AddCapability<Func<Tuple<bool, float, float, float, float>>>(HudOccupiedContract.Id, HudOccupiedContract.ContractVersion, SeasonIndicator.Occupied);
            registration.AddCapability<Func<IReadOnlyList<string>>>("meadworks.diagnostics." + Plugin.Guid, new System.Version(1, 0), Describe);
        }

        static Tuple<int, int, int, int, int, int> Provide() => SolsticeSeasons.View.ToTuple();

        static (int, int, int, int, int, int, int, int) Clock() =>
            SolsticeSeasons.Clock is SeasonClock clock ? SeasonClockView.FromClock(clock).ToTuple() : (0, 0, 0, 0, 0, 0, 0, 0);

        static string Facts() => SolsticeSeasons.Facts();

        static IReadOnlyList<string> Describe()
        {
            var lines = new List<string>();
            var aside = SolsticeModules.StepAside;
            lines.Add("module: seasons " + (SolsticeModules.Active(SolsticeModule.Seasons) ? "on" : "off")
                + (aside.Any ? " (steps aside: " + string.Join(", ", aside.Notices.Select(n => n.SourceName)) + ")" : ""));
            var view = SolsticeSeasons.View;
            lines.Add(view.Available
                ? "season: " + (ExtraPotions.Solstice.Calendar.Season)view.Season + ", day " + view.DayInSeason + " of " + view.DaysInSeason + ", day " + view.DayOfYear + " of " + view.YearLength
                    + (SolsticeSeasons.Held.HasValue ? " (held by the Season Override cheat)" : "") + ", effects " + (SolsticeSeasons.Effects != null ? "on" : "off")
                    + (SolsticeSeasons.Clock.HasValue ? ", clock revision " + SolsticeSeasons.Clock.Value.Revision : "")
                : "season: unavailable");
            lines.Add(SeasonVisuals.Describe() + (SeasonIndicator.Showing ? ", plate" : "") + SeasonMusicRuntime.Describe());
            lines.Add("weather reset " + (SolsticeWeather.RefreshPending ? "waiting" : "idle")
                + (SolsticeWeather.LastDecision.Yield ? ", yielding to " + SolsticeWeather.LastDecision.Reason : ""));
            return lines;
        }
    }
}
