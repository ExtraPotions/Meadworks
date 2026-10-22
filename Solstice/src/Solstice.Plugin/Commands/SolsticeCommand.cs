// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the status console verb.
using System.Collections.Generic;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Integration;
using ExtraPotions.Solstice.Localization;
using Jotunn.Entities;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary><c>solstice [status]</c>: the current season and day. Changing the season is the Season Override cheat, not a verb.</summary>
    public sealed class SolsticeCommand : ConsoleCommand
    {
        public const string CommandName = "solstice";

        public override string Name => CommandName;

        public override string Help => "Solstice seasons: status.";

        public override void Run(string[] args)
        {
            foreach (var line in Execute(args)) global::Console.instance?.Print(line);
        }

        static IEnumerable<string> Execute(string[] args)
        {
            if (args.Length > 0 && !string.Equals(args[0], "status", System.StringComparison.OrdinalIgnoreCase))
            {
                yield return SolsticeText.Text(SolsticeTextKeys.CommandHelp);
                yield break;
            }
            var view = SolsticeSeasons.View;
            if (Plugin.Settings == null || !SolsticeModules.Active(SolsticeModule.Seasons))
            {
                yield return SolsticeText.Text(SolsticeTextKeys.CommandInactive);
                yield break;
            }
            if (!view.Available)
            {
                yield return SolsticeText.Text(SolsticeTextKeys.CommandWaiting);
                yield break;
            }
            var name = SolsticeText.Text(SolsticeTextKeys.SeasonName((Season)view.Season));
            yield return SolsticeText.Format(SolsticeSeasons.Held.HasValue ? SolsticeTextKeys.CommandStatusOverride : SolsticeTextKeys.CommandStatus,
                name, view.DayInSeason, view.DaysInSeason);
        }
    }
}
