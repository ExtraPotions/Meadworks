// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localization keys Keel uses (tokens meadworks_keel_<key>, design section 11).
using System;
using System.Collections.Generic;
using ExtraPotions.Keel.Config;

namespace ExtraPotions.Keel.Localization
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
    }

    /// <summary>Every key of assets/localization/*.json; the tests hold the catalog to this table.</summary>
    public static class KeelTextKeys
    {
        public const string OffNoCrucible = "off_no_crucible";
        public const string OffNoKeel = "off_no_keel";
        public const string ImportDone = "import_done";
        public const string RecipeInvalid = "recipe_invalid";
        public const string RudderUse = "rudder_use";
        public const string StorageName = "storage_name";
        public const string GateOpen = "gate_open";
        public const string GateClose = "gate_close";
        public const string GateRefusedMoving = "gate_refused_moving";
        public const string AbilityWindCall = "ability_wind_call";
        public const string AbilityLookout = "ability_lookout";
        public const string AbilityNeedsLevel = "ability_needs_level";
        public const string AbilityCooldown = "ability_cooldown";
        public const string AbilityReady = "ability_ready";
        public const string WindCalled = "wind_called";
        public const string WindCalledSelf = "wind_called_self";
        public const string WindCalledBy = "wind_called_by";
        public const string LookoutSent = "lookout_sent";
        public const string PanelSpeed = "panel_speed";
        public const string PanelSpeedFactor = "panel_speed_factor";
        public const string PanelRadius = "panel_radius";
        public const string UnitMetres = "unit_meters";

        /// <summary>Wind square tooltip: its title, then {0} the strength as a percentage.</summary>
        public const string SquareWind = "square_wind";
        public const string SquareWindText = "square_wind_text";

        /// <summary>The key of a compass point name from <c>WindCallState.Points</c>: <c>compass_north_east</c>.</summary>
        public static string Compass(string point) => "compass_" + (point ?? throw new ArgumentNullException(nameof(point))).Replace('-', '_');

        /// <summary>The key of a speed unit's short name.</summary>
        public static string Unit(SpeedUnit unit)
        {
            switch (unit)
            {
                case SpeedUnit.MetersPerSecond: return "unit_meters_per_second";
                case SpeedUnit.Knots: return "unit_knots";
                case SpeedUnit.KilometersPerHour: return "unit_kilometers_per_hour";
                default: throw new ArgumentException("Unknown unit.", nameof(unit));
            }
        }

        public static IReadOnlyList<TextKey> All { get; } = Array.AsReadOnly(new[]
        {
            new TextKey("step_aside_tuning", 1),
            new TextKey("step_aside_sailing", 1),
            new TextKey("step_aside_catalog", 1),
            new TextKey("off_no_crucible", 0),
            new TextKey("off_no_keel", 0),
            new TextKey("import_done", 2),
            new TextKey("recipe_invalid", 2),
            new TextKey("cheat_ashlands_immune_ships", 0),
            new TextKey("cheat_invulnerable_ships", 0),
            new TextKey("skill_sailing", 0),
            new TextKey("skill_sailing_description", 0),
            new TextKey("ship_fast_karve", 0),
            new TextKey("ship_fast_longship", 0),
            new TextKey("ship_knarr", 0),
            new TextKey("ship_busse", 0),
            new TextKey("ship_big_busse", 0),
            new TextKey("ship_small_byrding", 0),
            new TextKey("ship_byrding", 0),
            new TextKey("ship_greater_byrding", 0),
            new TextKey("ship_fast_karve_description", 0),
            new TextKey("ship_fast_longship_description", 0),
            new TextKey("ship_knarr_description", 0),
            new TextKey("ship_busse_description", 0),
            new TextKey("ship_big_busse_description", 0),
            new TextKey("ship_small_byrding_description", 0),
            new TextKey("ship_byrding_description", 0),
            new TextKey("ship_greater_byrding_description", 0),
            new TextKey("rudder_use", 1),
            new TextKey("storage_name", 1),
            new TextKey("gate_open", 0),
            new TextKey("gate_close", 0),
            new TextKey("gate_refused_moving", 0),
            new TextKey("ability_wind_call", 0),
            new TextKey("ability_lookout", 0),
            new TextKey("ability_needs_level", 2),
            new TextKey("ability_cooldown", 2),
            new TextKey("ability_ready", 0),
            new TextKey("wind_called", 2),
            new TextKey("wind_called_self", 2),
            new TextKey("wind_called_by", 3),
            new TextKey("lookout_sent", 1),
            new TextKey("compass_north", 0),
            new TextKey("compass_north_east", 0),
            new TextKey("compass_east", 0),
            new TextKey("compass_south_east", 0),
            new TextKey("compass_south", 0),
            new TextKey("compass_south_west", 0),
            new TextKey("compass_west", 0),
            new TextKey("compass_north_west", 0),
            new TextKey("panel_speed", 0),
            new TextKey("panel_speed_factor", 0),
            new TextKey("panel_radius", 0),
            new TextKey("square_wind", 0),
            new TextKey("square_wind_text", 1),
            new TextKey("unit_meters_per_second", 0),
            new TextKey("unit_knots", 0),
            new TextKey("unit_kilometers_per_hour", 0),
            new TextKey(UnitMetres, 0),
        });
    }
}