// Tempest - ExtraPotions Meadworks weather filter.
// Portions Copyright (c) 2023 Pete Navarra (aka Vapok), NoFogBruh 2.1.2 (NoFogBruh/Features/WeatherEventsComponent.cs at
// 6a1bfe69271a0db58e51459767f9cbce5b3ac580, https://github.com/Vapok/NoFogBruh), MIT.
// The MIT notice is retained in THIRD_PARTY_NOTICES/NoFogBruh-MIT.txt.
// Changed by expDARE, 2026: the four switch sections, environment names, key names and descriptions became a portable table
// in Tempest's "Weather Filter" sections (Cairn Q2 moved them to Tempest); IsEnvironmentEnabled, the GetAvailableEnvironments
// filter with its biome clear fallback, and the GetEnvironmentOverride rule became pure functions; dynamically created
// switches were replaced by one list of other environment names; Tempest's own event environments are never filtered.
// Known Solstice variants inherit base switches and disabled names; optional seasonal weights can remove ordinary weather.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). This file as a whole: SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Tempest.Weather
{
    /// <summary>NoFogBruh's four switch sections.</summary>
    public enum SwitchGroup { World, Boss, Dungeon, Raid }

    /// <summary>One weather switch: the game environment it allows, its key and description (NoFogBruh's), its sections.</summary>
    public sealed class WeatherSwitch
    {
        internal WeatherSwitch(SwitchGroup group, string environment, string key, string description)
        {
            Group = group;
            Environment = environment;
            Key = key;
            Description = description;
        }

        public SwitchGroup Group { get; }

        /// <summary>The game environment name (<c>EnvSetup.m_name</c>).</summary>
        public string Environment { get; }

        /// <summary>NoFogBruh's key, kept so its config imports one to one.</summary>
        public string Key { get; }

        public string Description { get; }

        /// <summary>Tempest's section: <c>Weather Filter - World</c> and so on.</summary>
        public string Section => "Weather Filter - " + Group;

        /// <summary>NoFogBruh's section: <c>Weather Events - World</c> and so on.</summary>
        public string SourceSection => "Weather Events - " + Group;
    }

    public static class WeatherSwitches
    {
        public static IReadOnlyList<WeatherSwitch> All { get; } = Array.AsReadOnly(new[]
        {
            new WeatherSwitch(SwitchGroup.World, "Misty", "Enable Misty Weather", "Enables dense fog weather in Meadows, Black Forest, Plains, and Ocean."),
            new WeatherSwitch(SwitchGroup.World, "DeepForest Mist", "Enable Deep Forest Mist Weather", "Enables mist and fog weather in Black Forest."),
            new WeatherSwitch(SwitchGroup.World, "Rain", "Enable Rain Weather", "Enables standard rain in Meadows, Black Forest, Plains, and Ocean."),
            new WeatherSwitch(SwitchGroup.World, "LightRain", "Enable Light Rain Weather", "Enables light rain and drizzle in Meadows, Plains, and Ocean."),
            new WeatherSwitch(SwitchGroup.World, "ThunderStorm", "Enable Thunderstorm Weather", "Enables heavy thunderstorms and sea storms in Meadows, Plains, and Ocean."),
            new WeatherSwitch(SwitchGroup.World, "SwampRain", "Enable Swamp Rain Weather", "Enables continuous rain and gloom in Swamp."),
            new WeatherSwitch(SwitchGroup.World, "Snow", "Enable Snow Weather", "Enables snowfall in Mountain and Deep North."),
            new WeatherSwitch(SwitchGroup.World, "SnowStorm", "Enable Snowstorm Weather", "Enables snowstorms and blizzards in Mountain and Deep North."),
            new WeatherSwitch(SwitchGroup.World, "Twilight_Snow", "Enable Twilight Snow Weather", "Enables twilight snowfall in the Deep North."),
            new WeatherSwitch(SwitchGroup.World, "Twilight_SnowStorm", "Enable Twilight Snowstorm Weather", "Enables twilight snowstorms in the Deep North."),
            new WeatherSwitch(SwitchGroup.World, "Mistlands_rain", "Enable Mistlands Rain Weather", "Enables rainfall in Mistlands."),
            new WeatherSwitch(SwitchGroup.World, "Mistlands_thunder", "Enable Mistlands Thunder Weather", "Enables thunderstorms in Mistlands."),
            new WeatherSwitch(SwitchGroup.World, "Ashlands_misty", "Enable Ashlands Mist Weather", "Enables foggy mist weather in Ashlands."),
            new WeatherSwitch(SwitchGroup.World, "Ashlands_ashrain", "Enable Ashlands Ash Rain Weather", "Enables ash rain in Ashlands."),
            new WeatherSwitch(SwitchGroup.World, "Ashlands_CinderRain", "Enable Ashlands Cinder Rain Weather", "Enables cinder rain in Ashlands."),
            new WeatherSwitch(SwitchGroup.World, "Ashlands_storm", "Enable Ashlands Storm Weather", "Enables heavy storms in Ashlands."),
            new WeatherSwitch(SwitchGroup.World, "Ashlands_meteorshower", "Enable Ashlands Meteor Shower Weather", "Enables meteor showers in Ashlands."),
            new WeatherSwitch(SwitchGroup.World, "Ashlands_SeaStorm", "Enable Ashlands Sea Storm Weather", "Enables boiling sea storms in Ashlands."),
            new WeatherSwitch(SwitchGroup.World, "Ashrain", "Enable Legacy Ash Rain Weather", "Enables the older ash rain weather in Ashlands. Off: Ashlands never picks it."),
            new WeatherSwitch(SwitchGroup.World, "Darklands_dark", "Enable Darklands Dark Weather", "Enables darklands environmental darkness."),
            new WeatherSwitch(SwitchGroup.World, "nofogts", "Enable Internal NoFog Weather", "Enables the game's fog-free thunderstorm variant. Off: the game never picks it."),
            new WeatherSwitch(SwitchGroup.Boss, "Eikthyr", "Enable Eikthyr Weather", "Enables storm and lightning weather during Eikthyr boss fight."),
            new WeatherSwitch(SwitchGroup.Boss, "Eikthyr_no_lightning", "Enable Eikthyr No Lightning Weather", "Enables storm without lightning during Eikthyr boss fight."),
            new WeatherSwitch(SwitchGroup.Boss, "GDKing", "Enable The Elder Weather", "Enables overcast root weather during The Elder boss fight."),
            new WeatherSwitch(SwitchGroup.Boss, "Bonemass", "Enable Bonemass Weather", "Enables murky swamp weather during Bonemass boss fight."),
            new WeatherSwitch(SwitchGroup.Boss, "Moder", "Enable Moder Weather", "Enables freezing blizzard weather during Moder boss fight."),
            new WeatherSwitch(SwitchGroup.Boss, "GoblinKing", "Enable Yagluth Weather", "Enables fiery atmospheric weather during Yagluth boss fight."),
            new WeatherSwitch(SwitchGroup.Boss, "Queen", "Enable The Queen Weather", "Enables infested chamber weather during The Queen boss fight."),
            new WeatherSwitch(SwitchGroup.Boss, "Fader", "Enable Fader Weather", "Enables ash vortex weather during Fader boss fight."),
            new WeatherSwitch(SwitchGroup.Boss, "DN_Bossroom", "Enable Deep North Boss Room Weather", "Enables Deep North boss arena weather."),
            new WeatherSwitch(SwitchGroup.Dungeon, "Crypt", "Enable Burial Chambers Weather", "Enables interior crypt gloom in Black Forest Burial Chambers."),
            new WeatherSwitch(SwitchGroup.Dungeon, "SunkenCrypt", "Enable Sunken Crypt Weather", "Enables interior wet gloom in Swamp Sunken Crypts."),
            new WeatherSwitch(SwitchGroup.Dungeon, "Caves", "Enable Mountain Caves Weather", "Enables interior mist and cold in Mountain Frost Caves."),
            new WeatherSwitch(SwitchGroup.Dungeon, "CavesHildir", "Enable Hildir Frost Caves Weather", "Enables interior mist in Hildir Mountain Caves."),
            new WeatherSwitch(SwitchGroup.Dungeon, "CryptHildir", "Enable Hildir Sunken Crypt Weather", "Enables interior mist in Hildir Sunken Crypts."),
            new WeatherSwitch(SwitchGroup.Dungeon, "InfectedMine", "Enable Infected Mine Weather", "Enables interior mist and spores in Mistlands Infected Mines."),
            new WeatherSwitch(SwitchGroup.Raid, "Ghosts", "Enable Ghosts Raid Weather", "Enables ghostly mist during ghost raid encounters."),
            new WeatherSwitch(SwitchGroup.Raid, "JotunInvasion_meadows", "Enable Meadows Invasion Raid Weather", "Enables atmospheric distortion during Meadows Jotun invasions."),
            new WeatherSwitch(SwitchGroup.Raid, "JotunInvasion_blackforest", "Enable Black Forest Invasion Raid Weather", "Enables atmospheric distortion during Black Forest Jotun invasions."),
            new WeatherSwitch(SwitchGroup.Raid, "JotunInvasion_swamp", "Enable Swamp Invasion Raid Weather", "Enables atmospheric distortion during Swamp Jotun invasions."),
            new WeatherSwitch(SwitchGroup.Raid, "JotunInvasion_mountain", "Enable Mountain Invasion Raid Weather", "Enables atmospheric distortion during Mountain Jotun invasions."),
            new WeatherSwitch(SwitchGroup.Raid, "JotunInvasion_plains", "Enable Plains Invasion Raid Weather", "Enables atmospheric distortion during Plains Jotun invasions."),
            new WeatherSwitch(SwitchGroup.Raid, "JotunInvasion_mistlands", "Enable Mistlands Invasion Raid Weather", "Enables atmospheric distortion during Mistlands Jotun invasions."),
            new WeatherSwitch(SwitchGroup.Raid, "Morkhalla", "Enable Morkhalla Raid Weather", "Enables Morkhalla event weather."),
            new WeatherSwitch(SwitchGroup.Raid, "TheHollow", "Enable The Hollow Raid Weather", "Enables The Hollow event weather."),
        });

        static readonly Dictionary<string, WeatherSwitch> ByEnvironment = All.ToDictionary(s => s.Environment, StringComparer.OrdinalIgnoreCase);

        public static WeatherSwitch? Find(string? environment) =>
            environment != null && ByEnvironment.TryGetValue(environment, out var s) ? s : null;
    }

    /// <summary>
    /// The weather filter at one moment: which switches are on (read from C01 at use time) and the list of other
    /// environment names switched off. As in NoFogBruh, any environment whose name contains "clear" is always allowed;
    /// unknown names are allowed unless listed; Tempest's own event environments are always allowed.
    /// </summary>
    public sealed class WeatherFilter
    {
        readonly Func<WeatherSwitch, bool> isOn;
        readonly HashSet<string> others;
        readonly SeasonalWeights? seasonal;

        public WeatherFilter(Func<WeatherSwitch, bool> isOn, string? otherDisabled) : this(isOn, otherDisabled, null) { }

        public WeatherFilter(Func<WeatherSwitch, bool> isOn, string? otherDisabled, SeasonalWeights? seasonal)
        {
            this.isOn = isOn ?? throw new ArgumentNullException(nameof(isOn));
            others = new HashSet<string>(SplitList(otherDisabled), StringComparer.OrdinalIgnoreCase);
            this.seasonal = seasonal;
        }

        public bool Allows(string? environment)
        {
            if (string.IsNullOrEmpty(environment) || IsClear(environment!) || IsTempest(environment!)) return true;
            var source = SeasonalEnvironments.BaseName(environment!);
            if (others.Contains(environment!) || others.Contains(source)) return false;
            var s = WeatherSwitches.Find(source);
            return (s == null || isOn(s)) && Weight(environment) > 0;
        }

        public float Weight(string? environment) => seasonal?.Multiplier(environment) ?? 1;

        /// <summary>
        /// NoFogBruh's GetAvailableEnvironments filter: drops switched-off entries; when nothing is left, the fallback
        /// (if known) is the only entry. An empty result means no fallback was known and the caller keeps the game's list.
        /// </summary>
        public IReadOnlyList<string> Available(IEnumerable<string> environments, string? fallback)
        {
            var kept = (environments ?? Enumerable.Empty<string>()).Where(Allows).ToList();
            if (kept.Count == 0 && !string.IsNullOrEmpty(fallback)) kept.Add(fallback!);
            return kept;
        }

        /// <summary>NoFogBruh's GetEnvironmentOverride rule: a switched-off forced environment is dropped (null).</summary>
        public string? ForcedOrNull(string? environment) => string.IsNullOrEmpty(environment) || !Allows(environment) ? null : environment;

        /// <summary>
        /// NoFogBruh's fallback choice: the biome's first environment whose name contains "clear", else the game's
        /// <c>Clear</c>, else the game's default environment.
        /// </summary>
        public static string? Fallback(IEnumerable<string>? biomeEnvironments, Func<string, bool> exists, string? defaultEnvironment)
        {
            foreach (var name in biomeEnvironments ?? Enumerable.Empty<string>())
                if (!string.IsNullOrEmpty(name) && IsClear(name) && exists(name)) return name;
            return exists("Clear") ? "Clear" : defaultEnvironment;
        }

        /// <summary>The canonical text of the other-environments list: trimmed, de-duplicated, without clear or Tempest names.</summary>
        public static string NormalizeList(string? text) => string.Join(", ", SplitList(text));

        static IEnumerable<string> SplitList(string? text)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (text ?? "").Split(','))
            {
                var name = part.Trim();
                if (name.Length == 0 || IsClear(name) || IsTempest(name) || !seen.Add(name)) continue;
                yield return name;
            }
        }

        static bool IsClear(string name) => name.IndexOf("clear", StringComparison.OrdinalIgnoreCase) >= 0;

        static bool IsTempest(string name) => EnvironmentPlan.IsEventEnvironment(name);
    }
}
