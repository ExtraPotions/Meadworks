// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: carries untouched saved defaults forward when a default changes (as Wildblood's DefaultUpgrades).
using System;
using System.Collections.Generic;
using ExtraPotions.Tempest.Events;

namespace ExtraPotions.Tempest.Config
{
    /// <summary>
    /// A saved value that is exactly an earlier default was never chosen by anyone, so it moves to the current default.
    /// Any other saved value is the owner's choice and stays as it is.
    /// </summary>
    public static class DefaultUpgrades
    {
        /// <summary>The Blizzard Biomes default before the Deep North whiteout (2026-10-19).</summary>
        public const string BlizzardBiomesBeforeDeepNorth = "Meadows, BlackForest";

        static readonly Dictionary<TempestSetting<string>, string> earlier = new Dictionary<TempestSetting<string>, string>
        {
            [TempestSettings.Schedule.BlizzardBiomes] = BlizzardBiomesBeforeDeepNorth,
        };

        /// <summary>The current default when <paramref name="saved"/> is exactly the earlier default; otherwise null.</summary>
        public static string? Upgrade(TempestSetting setting, string? saved) =>
            setting is TempestSetting<string> text && saved != null && earlier.TryGetValue(text, out var old)
                && string.Equals(saved, old, StringComparison.Ordinal) ? text.Default : null;

        /// <summary>A customized, non-empty Blizzard Biomes list that leaves the Deep North out (worth one log line).</summary>
        public static bool MissesDeepNorth(TempestSetting setting, string? saved)
        {
            if (!ReferenceEquals(setting, TempestSettings.Schedule.BlizzardBiomes) || saved == null) return false;
            var biomes = BiomeSets.Parse(EventKind.Blizzard, saved);
            return biomes != WeatherBiome.None && (biomes & WeatherBiome.DeepNorth) == 0;
        }
    }
}
