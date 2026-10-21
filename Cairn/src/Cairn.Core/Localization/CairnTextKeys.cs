// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localization keys Cairn uses (tokens meadworks_cairn_<key>, design section 11).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Cairn.Integration;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Visibility;

namespace ExtraPotions.Cairn.Localization
{
    public readonly struct TextKey
    {
        public TextKey(string key, int arguments)
        {
            Key = key;
            Arguments = arguments;
        }

        public string Key { get; }

        /// <summary>Number of <c>{0}</c>-style placeholders, numbered from 0.</summary>
        public int Arguments { get; }

        public override string ToString() => Key;
    }

    /// <summary>Every key of assets/localization/*.json; the tests hold the catalog to this table.</summary>
    public static partial class CairnTextKeys
    {
        /// <summary>Once per session when the Q1 hold applies.</summary>
        public const string HeldNotice = "held_notice";

        public const string SkillName = "skill_exploration";
        public const string SkillDescription = "skill_exploration_description";
        public const string Vantage = "vantage";

        public const string HudDay = "hud_day";

        /// <summary>Comfort square in the minimap column: comfort level {0}, Rested time left {0} (m:ss).</summary>
        public const string SquareComfort = "square_comfort";
        public const string SquareRested = "square_rested";
        public const string SquareNotRested = "square_not_rested";

        /// <summary>Heading under the compass: {0} whole degrees, {1} compass point (CA06b).</summary>
        public const string HudHeading = "hud_heading";

        /// <summary>Next weather on the clock line: {0} name, {1} minutes.</summary>
        public const string HudForecast = "hud_forecast";

        /// <summary>Next weather on the clock line: {0} name, {1} minutes, {2} wind point and strength.</summary>
        public const string HudForecastWind = "hud_forecast_wind";
        public const string RadiusLine = "radius_line";
        public const string RadiusInputs = "radius_inputs";
        public const string ImportDone = "import_done";
        public const string TidyPinsDescription = "tidy_pins_description";

        /// <summary>Tempest warning on the HUD: {0} event name, {1} countdown m:ss (CA09).</summary>
        public const string TempestWarning = "tempest_warning";

        /// <summary>Tempest event clearing: {0} event name (CA09).</summary>
        public const string TempestClearing = "tempest_clearing";

        /// <summary>Center notice when a Tempest warning starts where the player is: {0} event name (CA09).</summary>
        public const string TempestNotice = "tempest_notice";

        /// <summary>A Tempest event's name on the HUD row and center notice, worded as Tempest names it (tempest_blizzard,
        /// tempest_thunderstorm); the contract carries no name, so the catalogs repeat Tempest's kind names.</summary>
        public static string TempestEvent(TempestKind kind) => TempestToken(ExtraPotions.Cairn.Hud.TempestWarning.Token(kind) ?? kind.ToString().ToLowerInvariant());

        /// <summary>The name key of a detail contract token: <c>tempest_&lt;token&gt;</c>.</summary>
        public static string TempestToken(string token) => "tempest_" + token;

        public static string Weather(WeatherCategory category) => "weather_" + category.ToString().ToLowerInvariant();

        public static string CompassPoint(string point) => "compass_" + point.ToLowerInvariant();
        public static string Setting(CairnSetting setting) => "setting_" + setting.Section[0] + "_" + setting.Key.ToLowerInvariant().Replace(' ', '_');
        public static string Effect(VisibilityEffect effect) => "effect_" + effect.ToString().ToLowerInvariant();
        public static string SettingHelp(CairnSetting setting) => "help_" + Setting(setting);

        /// <summary>The game's map pin types (Minimap.PinType names), each with a readable label key.</summary>
        public static readonly IReadOnlyList<string> PinTypes = Array.AsReadOnly(new[]
        {
            "Icon0", "Icon1", "Icon2", "Icon3", "Icon4", "Death", "Bed", "Shout", "None", "Boss", "Player", "RandomEvent", "Ping",
            "EventArea", "Hildir1", "Hildir2", "Hildir3", "Memorial",
        });

        public static string PinType(string type) => "pin_type_" + type.ToLowerInvariant();

        private static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        public static IReadOnlyList<TextKey> All { get; } = Array.AsReadOnly(
            new[]
            {
                new TextKey(HeldNotice, 0),
                new TextKey(SkillName, 0), new TextKey(SkillDescription, 0), new TextKey(Vantage, 0),
                new TextKey(HudDay, 1), new TextKey(SquareComfort, 1), new TextKey(SquareRested, 1), new TextKey(SquareNotRested, 0),
                new TextKey(HudHeading, 2), new TextKey(HudForecast, 2), new TextKey(HudForecastWind, 3),
                new TextKey(RadiusLine, 1), new TextKey(RadiusInputs, 7), new TextKey(ImportDone, 2),
                new TextKey(TidyPinsDescription, 0),
                new TextKey(TempestWarning, 2), new TextKey(TempestClearing, 1), new TextKey(TempestNotice, 1),
                new TextKey(TempestEvent(TempestKind.Blizzard), 0), new TextKey(TempestEvent(TempestKind.Thunderstorm), 0),
                new TextKey(TempestEvent(TempestKind.LightningStorm), 0), new TextKey(TempestEvent(TempestKind.DenseFog), 0),
                new TextKey(TempestEvent(TempestKind.AshStorm), 0), new TextKey(TempestEvent(TempestKind.MeteorShower), 0),
                new TextKey("ui_close", 0), new TextKey("ui_on", 0), new TextKey("ui_off", 0),
                new TextKey("ui_tab_markers", 0), new TextKey("ui_tab_hud", 0), new TextKey("ui_tab_compass", 0), new TextKey("ui_tab_visibility", 0), new TextKey("ui_tab_map", 0),
                new TextKey("ui_markers_hint", 0), new TextKey("ui_shown_by_type", 1),
                new TextKey("ui_map_cursor", 0),
                new TextKey("ui_help_close", 0),
                new TextKey("ui_help_tab_markers", 0), new TextKey("ui_help_tab_hud", 0),
                new TextKey("ui_help_tab_compass", 0), new TextKey("ui_help_tab_visibility", 0), new TextKey("ui_help_tab_map", 0),
                new TextKey("ui_help_map_cursor", 0), new TextKey("ui_help_marker_type", 0), new TextKey("ui_help_marker_pin", 0),
                new TextKey("ui_help_marker_map", 0), new TextKey("ui_help_decrease", 0), new TextKey("ui_help_increase", 0),
                new TextKey("ui_menu_hud_settings", 0), new TextKey("ui_menu_markers", 0),
                new TextKey("ui_menu_hud_hide", 0),
                new TextKey("ui_menu_hud_show", 0),
                new TextKey("sleep_sleeping", 2), new TextKey("sleep_boost", 1), new TextKey("sleep_skip", 1),
                new TextKey("setting_remove", 1), new TextKey("setting_allow_removing", 1),
                new TextKey("help_remove", 1), new TextKey("help_allow_removing", 1),
            }
            .Concat(Enum.GetValues(typeof(CairnModule)).Cast<CairnModule>().Select(m => new TextKey(StepAsideNotice.KeyOf(m), 1)))
            .Concat(Enum.GetValues(typeof(WeatherCategory)).Cast<WeatherCategory>().Select(c => new TextKey(Weather(c), 0)))
            .Concat(Points.Select(p => new TextKey(CompassPoint(p), 0)))
            .Concat(PinTypes.Select(t => new TextKey(PinType(t), 0)))
            .Concat(CairnSettings.All.Where(s => !s.Hidden && s.Section != CairnSettings.Visibility.Section && s.Section != CairnSettings.VisibilityServer.Section).Select(s => new TextKey(Setting(s), 0)))
            .Concat(CairnSettings.All.Where(s => !s.Hidden && s.Section != CairnSettings.Visibility.Section && s.Section != CairnSettings.VisibilityServer.Section && !ReferenceEquals(s, CairnSettings.Map.TidyPins)).Select(s => new TextKey(SettingHelp(s), 0)))
            .Concat(Enum.GetValues(typeof(VisibilityEffect)).Cast<VisibilityEffect>().Select(e => new TextKey(Effect(e), 0)))
            .ToArray());
    }
}
