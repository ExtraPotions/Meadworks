// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Setting names, defaults and ranges follow design section 4 with the 2026-10-08 toggle decision; the
// Pathfinder, NoFogBruh, BetterFog and TimeWeatherHud options they replace are recorded in the design (names only).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Cairn.Visibility;
using static ExtraPotions.Cairn.Config.SettingScope;

namespace ExtraPotions.Cairn.Config
{

    /// <summary>Text rules for the text settings.</summary>
    public static class TextRules
    {
        /// <summary>Stored text as is; null reads empty.</summary>
        public static string Verbatim(string value) => value ?? "";

        /// <summary>Trimmed text; null reads empty (an empty pad chord means off).</summary>
        public static string Trimmed(string value) => (value ?? "").Trim();

        /// <summary>A keyboard shortcut text: trimmed; empty reads <paramref name="fallback"/>.</summary>
        public static Func<string, string> Key(string fallback) => value => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    /// <summary>
    /// Every Cairn setting in file order. The plugin binds exactly this list; the portable tests compare it with the design.
    /// Cairn has no cheat: map reveal moved to Hex (roster, 2026-10-07).
    /// </summary>
    public static class CairnSettings
    {
        /// <summary>Design 4.1 (Pathfinder names and defaults).</summary>
        public static class Radius
        {
            public const string Section = "1 - Exploration Radius";

            public static readonly CairnSetting<bool> Enabled = CairnSetting.Toggle(Section, "Enabled", true, ServerAuthoritative, "Cairn computes the map exploration radius. Off: the game's 100 m radius with Keel's and the Exploration skill's factors only.");
            public static readonly CairnSetting<float> MinimumRadius = CairnSetting.Number(Section, "Minimum Radius", 20f, 0f, 10000f, ServerAuthoritative, "Smallest exploration radius in meters; a lower computed radius is raised to this. Used as at most Maximum Radius.");
            public static readonly CairnSetting<float> MaximumRadius = CairnSetting.Number(Section, "Maximum Radius", 2000f, 0f, 10000f, ServerAuthoritative, "Largest exploration radius in meters; a higher computed radius is lowered to this. Large values may cost performance.");
            public static readonly CairnSetting<float> LandExploreRadius = CairnSetting.Number(Section, "Land Explore Radius", 200f, 0f, 2000f, ServerAuthoritative, "Radius uncovered while traveling on land near sea level. The game uses 100.");
            public static readonly CairnSetting<float> SeaExploreRadius = CairnSetting.Number(Section, "Sea Explore Radius", 300f, 0f, 2000f, ServerAuthoritative, "Radius uncovered while you or a player within 21 m is steering a ship. The game uses 100.");
            public static readonly CairnSetting<float> AltitudeRadiusBonus = CairnSetting.Number(Section, "Altitude Radius Bonus", 0.5f, 0f, 2f, ServerAuthoritative, "Each 100 m above sea level adds this share of the land radius. 0 turns it off.");
            public static readonly CairnSetting<float> ForestRadiusPenalty = CairnSetting.Number(Section, "Forest Radius Penalty", 0.3f, 0f, 1f, ServerAuthoritative, "In forest (Black Forest, forested Meadows and Plains) this share of the land radius is subtracted. 0 turns it off.");
            public static readonly CairnSetting<float> DaylightRadiusScale = CairnSetting.Number(Section, "Daylight Radius Scale", 0.2f, 0f, 1f, ServerAuthoritative, "How much daylight widens and darkness narrows the radius. 0 turns it off.");
            public static readonly CairnSetting<float> WeatherRadiusScale = CairnSetting.Number(Section, "Weather Radius Scale", 0.5f, 0f, 1f, ServerAuthoritative, "How much fog, mist and snowstorms narrow the radius. 0 turns it off.");
            public static readonly CairnSetting<bool> ShowRadius = CairnSetting.Toggle(Section, "Show Radius", false, Local, "Show the current exploration radius on the HUD.");
            public static readonly CairnSetting<bool> ShowRadiusInputs = CairnSetting.Toggle(Section, "Show Radius Inputs", false, Local, "Diagnostic record of every input of the exploration radius (light, weather, altitude, location, factors). Kept in this file only; the HUD no longer draws it.");

            internal static IEnumerable<CairnSetting> Items => new CairnSetting[]
            {
                Enabled, MinimumRadius, MaximumRadius, LandExploreRadius, SeaExploreRadius, AltitudeRadiusBonus, ForestRadiusPenalty,
                DaylightRadiusScale, WeatherRadiusScale, ShowRadius, ShowRadiusInputs,
            };
        }

        /// <summary>Design 4.2 as plain toggles (user decision 2026-10-08): off keeps the game's effect.</summary>
        public static class Visibility
        {
            public const string Section = "2 - Visibility";

            private static readonly CairnSetting<bool>[] Toggles = VisibilityEffects.All
                .Select(e => CairnSetting.Toggle(Section, "Remove " + e.Name, false, Local, "Remove " + e.Description + ". Off keeps the game's effect."))
                .ToArray();

            public static CairnSetting<bool> Remove(VisibilityEffect effect) => Toggles[(int)effect];

            internal static IEnumerable<CairnSetting> Items => Toggles;
        }

        /// <summary>Design 4.3 as toggles: a server running Cairn may keep any effect on for everyone.</summary>
        public static class VisibilityServer
        {
            public const string Section = "3 - Visibility Server";

            private static readonly CairnSetting<bool>[] Toggles = VisibilityEffects.All
                .Select(e => CairnSetting.Toggle(Section, "Allow Removing " + e.Name, true, ServerAuthoritative, "Players may remove " + e.Description + ". Off keeps it on for everyone."))
                .ToArray();

            public static CairnSetting<bool> Allow(VisibilityEffect effect) => Toggles[(int)effect];

            internal static IEnumerable<CairnSetting> Items => Toggles;
        }

        /// <summary>Design 4.4 (TimeWeatherHud names and defaults).</summary>
        public static class Hud
        {
            public const string Section = "4 - HUD";

            public static readonly CairnSetting<bool> Enabled = CairnSetting.Toggle(Section, "Enabled", true, Local, "Show the time, weather and compass HUD, and the sleep row under the minimap (moon: night, beds usable; count: players in bed out of players online; +N%: faster night; N%: skip to morning).");
            public static readonly CairnSetting<string> ToggleKey = CairnSetting.Text(Section, "Toggle Key", "", Local, "Key that shows or hides the HUD. Empty (default): none; the Meadworks menu (F8, Esc > Meadworks) has the toggle.", TextRules.Key(""));
            public static readonly CairnSetting<string> SettingsKey = CairnSetting.Text(Section, "Settings Key", "", Local, "Key that opens Cairn's compass markers window. Empty (default): none; the Meadworks menu (F8, Esc > Meadworks) lists the window. Cairn's settings are in Almanac.", TextRules.Key(""));
            public static readonly CairnSetting<bool> ShowDay = CairnSetting.Toggle(Section, "Show Day", true, Local, "Shows the world's day number (Day 12) at the start of the HUD's time line. Your own display setting. Default: on.");
            public static readonly CairnSetting<bool> ShowTime = CairnSetting.Toggle(Section, "Show Time", true, Local, "Show the time of day with a sun or moon mark.");
            public static readonly CairnSetting<bool> ShowWeather = CairnSetting.Toggle(Section, "Show Weather", true, Local, "Shows the current weather's name and icon on the HUD's time line, including Tempest events such as Blizzard or Lightning Storm. Your own display setting. Default: on.");
            public static readonly CairnSetting<bool> ShowForecast = CairnSetting.Toggle(Section, "Show Forecast", true, Local, "Shows the next weather on the HUD clock line for the biome you are in: its name, the minutes until the shared world clock changes it, and the wind direction and strength at that change. The prediction uses the game's environment schedule, so a client matches the host. When Tempest is installed, environments it has switched off are left out. Hidden while a raid, a forced environment or a Tempest event is controlling the weather. The current weather stays on the line when both fit; the clock is never cut. Seasonal names from Solstice, such as Clear Winter, use the same name and icon as the weather they copy. The prediction reads the current season's list, so a change after the season turns can differ. Your own display setting. Default: on.");
            public static readonly CairnSetting<bool> Use24Hour = CairnSetting.Toggle(Section, "Use 24 Hour", true, Local, "24-hour clock; off shows a 12-hour clock.");
            public static readonly CairnSetting<bool> ShowCompass = CairnSetting.Toggle(Section, "Show Compass", true, Local, "Shows the compass strip under the time line: the direction you face in the middle, with N/E/S/W marks and the chosen map pins. Your own display setting. Default: on.");
            public static readonly CairnSetting<int> StripDegrees = CairnSetting.Integer(Section, "Strip Degrees", 120, 60, 360, Local, "Degrees the compass shows at once.");
            public static readonly CairnSetting<int> CompassWidth = CairnSetting.Integer(Section, "Compass Strip Width", global::ExtraPotions.Cairn.Hud.HudLayout.MinWidth,
                global::ExtraPotions.Cairn.Hud.HudLayout.MinWidth, global::ExtraPotions.Cairn.Hud.HudLayout.MaxWidth, Local,
                "Width of the compass plank in HUD units; 240 is the standard width (320 pixels at 1080p). The time line, compass marks and pins are placed again at once. Your own display setting.");
            public static readonly CairnSetting<bool> ShowHeading = CairnSetting.Toggle(Section, "Show Heading", true, Local, "Show the exact heading under the compass.");
            public static readonly CairnSetting<bool> ShowMarkers = CairnSetting.Toggle(Section, "Show Markers", true, Local, "Show the chosen map pins on the compass.");
            public static readonly CairnSetting<int> MaxMarkers = CairnSetting.Integer(Section, "Max Markers", 10, 1, 30, Local, "Markers shown at once, nearest first.");
            public static readonly CairnSetting<float> MaxDistance = CairnSetting.Number(Section, "Max Distance", 0f, 0f, 100000f, Local, "Hide markers farther than this many meters; 0 shows all.");
            public static readonly CairnSetting<bool> TempestWarnings = CairnSetting.Toggle(Section, "Tempest Warnings", true, Local, "Show Tempest's storm warnings and countdown on the HUD.");
            public static readonly CairnSetting<string> GamepadToggle = CairnSetting.Text(Section, "Gamepad Toggle", "", Local, "Gamepad chord that shows or hides the HUD; empty is off.", TextRules.Trimmed);
            public static readonly CairnSetting<string> GamepadSettings = CairnSetting.Text(Section, "Gamepad Settings", "", Local, "Gamepad chord that opens Cairn's compass markers window; empty is off.", TextRules.Trimmed);

            internal static IEnumerable<CairnSetting> Items => new CairnSetting[]
            {
                Enabled, ToggleKey, SettingsKey, ShowDay, ShowTime, ShowWeather, ShowForecast, Use24Hour,
                ShowCompass, StripDegrees, CompassWidth, ShowHeading, ShowMarkers, MaxMarkers, MaxDistance, TempestWarnings,
                GamepadToggle, GamepadSettings,
            };
        }

        /// <summary>Design 4.5 (Meadworks Exploration skill).</summary>
        public static class Skill
        {
            public const string Section = "5 - Exploration Skill";

            public static readonly CairnSetting<bool> Enabled = CairnSetting.Toggle(Section, "Exploration Enabled", true, ServerAuthoritative, "The Exploration skill gains experience and applies its perks. Levels are kept either way.");
            public static readonly CairnSetting<float> ExperiencePerSquareKilometre = CairnSetting.Number(Section, "Experience Per Square Kilometer", 10f, 0f, 1000f, ServerAuthoritative, "Experience for each square kilometer of newly uncovered map.");
            public static readonly CairnSetting<float> LocationDiscoveryExperience = CairnSetting.Number(Section, "Location Discovery Experience", 5f, 0f, 1000f, ServerAuthoritative, "Experience for each newly discovered location on the map.");
            public static readonly CairnSetting<float> NewBiomeExperience = CairnSetting.Number(Section, "New Biome Experience", 20f, 0f, 1000f, ServerAuthoritative, "Experience for entering a biome for the first time.");
            public static readonly CairnSetting<float> LandRadiusAt100 = CairnSetting.Number(Section, "Land Radius At 100", 50f, 0f, 500f, ServerAuthoritative, "Percent larger land exploration radius at level 100, growing linearly from level 0.");
            public static readonly CairnSetting<float> WeatherPenaltyReductionAt100 = CairnSetting.Number(Section, "Weather Penalty Reduction At 100", 50f, 0f, 100f, ServerAuthoritative, "Percent less radius lost to fog, mist and snow at level 100.");
            public static readonly CairnSetting<float> NightPenaltyReductionAt100 = CairnSetting.Number(Section, "Night Penalty Reduction At 100", 50f, 0f, 100f, ServerAuthoritative, "Percent less radius lost to darkness at level 100.");
            public static readonly CairnSetting<int> VantageLevel = CairnSetting.Integer(Section, "Vantage Level", 50, 0, 101, ServerAuthoritative, "Level that unlocks Vantage (stand still on high ground to uncover twice the radius once). Above 100 turns it off.");
            public static readonly CairnSetting<float> VantageCooldown = CairnSetting.Number(Section, "Vantage Cooldown", 120f, 0f, 3600f, ServerAuthoritative, "Seconds between two Vantage uses.");

            internal static IEnumerable<CairnSetting> Items => new CairnSetting[]
            {
                Enabled, ExperiencePerSquareKilometre, LocationDiscoveryExperience, NewBiomeExperience, LandRadiusAt100,
                WeatherPenaltyReductionAt100, NightPenaltyReductionAt100, VantageLevel, VantageCooldown,
            };
        }

        /// <summary>Hidden converter marker (C09): the sources this machine already imported.</summary>
        public static class Import
        {
            public const string Section = "6 - Import";

            public static readonly CairnSetting<string> ImportedSources = CairnSetting.Text(Section, "Imported Sources", "", Local, "Managed by Cairn.", TextRules.Verbatim, hidden: true);
            public static readonly CairnSetting<string> PendingMarkers = CairnSetting.Text(Section, "Pending Markers", "", Local, "Imported marker choices waiting for their world and character.", TextRules.Verbatim, hidden: true);
        }

        /// <summary>Every setting in file order.</summary>
        /// <summary>Local native-map presentation; safe default keeps the current map unchanged.</summary>
        public static class Map
        {
            public const string Section = "7 - Map";

            public static readonly CairnSetting<bool> TidyPins = CairnSetting.Toggle(Section, "Tidy Pins", false, Local,
                "Show pin names only under the mouse or gamepad map cursor (or every name while Show All Names Key is held), use smaller icons, grow the hovered pin and fade crossed-out pins. Player, ping and shout pins keep the game's size and opacity. Off restores the game presentation.");
            public static readonly CairnSetting<string> ShowAllNamesKey = CairnSetting.Text(Section, "Show All Names Key", "LeftAlt", Local,
                "With Tidy Pins on, hold this key on the large map to show every pin name. A BepInEx key shortcut such as LeftAlt; None turns it off. Your own key, checked against other MeadWorks keys.",
                TextRules.Trimmed);
            public static readonly CairnSetting<float> HoverScale = CairnSetting.Number(Section, "Hovered Pin Scale", 1.25f, 1f, 2f, Local,
                "With Tidy Pins on, the pin under the mouse or gamepad map cursor is drawn this many times larger. 1 turns it off. Your own display setting.");

            internal static IEnumerable<CairnSetting> Items => new CairnSetting[] { TidyPins, ShowAllNamesKey, HoverScale };
        }

        /// <summary>Graded fog, mist and smoke (BetterFog's fractions of vanilla). A Remove toggle wins; the server's Allow Removing gates each.</summary>
        public static class VisibilityStrength
        {
            public const string Section = "9 - Visibility Strength";

            public static readonly CairnSetting<float> DistanceFog = CairnSetting.Number(Section, "Distance Fog Strength", 1f, 0f, 1f, Local,
                "Distance fog density as a fraction of the game's: 1 keeps the game's fog, 0 clears it. Your own display setting, applied at once. Ignored while Remove Distance Fog is on or the server does not allow removing distance fog.");
            public static readonly CairnSetting<float> Mist = CairnSetting.Number(Section, "Mist Strength", 1f, 0f, 1f, Local,
                "Opacity of mist and fog particles (Mistlands mist, distant fog banks, weather fog) as a fraction of the game's: 1 keeps them, 0 hides them. Your own display setting, applied at once. Ignored while the server does not allow removing Mistlands or particle mist, and held at 1 on a server without Cairn.");
            public static readonly CairnSetting<float> Smoke = CairnSetting.Number(Section, "Smoke Strength", 1f, 0f, 1f, Local,
                "Opacity of fire smoke as a fraction of the game's: 1 keeps it, 0 hides it. Your own display setting, applied at once. Ignored while Remove Smoke is on or the server does not allow removing smoke.");

            internal static IEnumerable<CairnSetting> Items => new CairnSetting[] { DistanceFog, Mist, Smoke };
        }

        /// <summary>Key settings the plugin binds as BepInEx <c>KeyboardShortcut</c>, so MeadWorks key-clash checks see them.</summary>
        public static IReadOnlyList<CairnSetting<string>> KeyboardShortcuts { get; } = Array.AsReadOnly(new[] { Map.ShowAllNamesKey });

        public static class WorldClock
        {
            public const string Section = "8 - World Clock";
            public static readonly CairnSetting<float> Rate = CairnSetting.Number(Section, "World Time Speed", 1f, 0.1f, 10f, ServerAuthoritative,
                "Day/night cycle speed (shared world seconds per normal second): 1 is normal, a 30-minute day and night; 0.5 makes days and nights twice as long and 2 half as long. Changes the shared clock live on the server, including crops, events and other world-time timers, and multiplies Ember sleep acceleration. Movement and real-time timers are unchanged. Vanilla clients use native time synchronization; changing rate preserves the current time.");
        }

        public static IReadOnlyList<CairnSetting> All { get; } = Array.AsReadOnly(
            Radius.Items.Concat(Visibility.Items).Concat(VisibilityServer.Items).Concat(Hud.Items).Concat(Skill.Items)
                .Concat(new CairnSetting[] { Import.ImportedSources, Import.PendingMarkers }).Concat(Map.Items).Concat(new CairnSetting[] { WorldClock.Rate }).Concat(VisibilityStrength.Items).ToArray());
    }
}
