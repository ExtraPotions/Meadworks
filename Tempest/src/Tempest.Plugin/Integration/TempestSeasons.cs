// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Tempest.Config;
using ExtraPotions.Tempest.Weather;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Tempest.Runtime
{
    public static class TempestSeasons
    {
        static readonly System.Version ContractVersion = new System.Version(1, 0);
        static readonly SeasonFactsReader Reader = new SeasonFactsReader(Resolve);

        static Func<string>? Resolve()
        {
            CruciblePlugin.Services.Capabilities.TryResolve("meadworks.seasons.facts", ContractVersion, out Func<string>? provider);
            return provider;
        }

        public static void Tick()
        {
            var before = EffectiveSeason(Reader.Reading);
            if (!Reader.Poll(UnityEngine.Time.unscaledTimeAsDouble)) return;
            if (before == EffectiveSeason(Reader.Reading)) return;
            TempestFilter.Invalidate();
            TempestFilter.Reapply();
        }

        static int EffectiveSeason(SeasonReading reading) => reading.Available && reading.Effects ? reading.Season : -1;

        public static SeasonalWeights? Weights(TempestConfig settings)
        {
            var season = Reader.Reading;
            return season.Available && season.Effects
                ? new SeasonalWeights(settings.Get(TempestSettings.SeasonalWeather.Weights[season.Season])) : null;
        }

        public static string? EventEnvironment(string? name, Func<string, bool> exists)
        {
            var season = Reader.Reading;
            return season.Available && season.Effects ? SeasonalEnvironments.ForSeason(name, season.Season, exists) : name;
        }

        public static void Shutdown() => Reader.Reset();
    }
}
