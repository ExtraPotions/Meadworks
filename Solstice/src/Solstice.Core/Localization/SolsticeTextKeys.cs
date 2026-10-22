// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localisation keys Solstice uses (tokens meadworks_solstice_<key>).
using System;
using System.Collections.Generic;
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Localization
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
    public static class SolsticeTextKeys
    {
        public const string BlockedSetByServer = "blocked_set_by_server";
        public const string BlockedVanillaServer = "blocked_vanilla_server";
        public const string BlockedProductDisabled = "blocked_product_disabled";
        public const string BlockedNotAdmin = "blocked_not_admin";
        public const string BlockedAuthorityPending = "blocked_authority_pending";
        public const string OffNoCrucible = "off_no_crucible";
        public const string OffNoSolstice = "off_no_solstice";
        public const string SeasonStarted = "season_started";
        public const string CommandStatus = "cmd_status";
        public const string CommandStatusOverride = "cmd_status_override";
        public const string CommandInactive = "cmd_inactive";
        public const string CommandHelp = "cmd_help";
        public const string CommandWaiting = "cmd_waiting";
        public const string PlantFrostResistant = "plant_frost_resistant";
        public const string PlantHeatProtected = "plant_heat_protected";
        public const string PlantSurvivedWinter = "plant_survived_winter";
        public const string PlantFreezing = "plant_freezing";
        public const string PlantWillPerish = "plant_will_perish";
        public const string PlantExposed = "plant_exposed";
        public const string StatusOverheat = "status_overheat";
        public const string StatusOverheatTip = "status_overheat_tip";
        public const string StatusSeasonTip = "status_season_tip";
        public const string HudSeason = "hud_season";
        public const string HudHeat = "hud_heat";

        /// <summary>The key of a season's display name: <c>season_spring</c> and so on.</summary>
        public static string SeasonName(Season season) => "season_" + season.ToString().ToLowerInvariant();

        public static IReadOnlyList<TextKey> All { get; } = Array.AsReadOnly(new[]
        {
            new TextKey("step_aside_seasons", 1),
            new TextKey("off_no_crucible", 0),
            new TextKey("off_no_solstice", 0),
            new TextKey("blocked_set_by_server", 0),
            new TextKey("blocked_vanilla_server", 0),
            new TextKey("blocked_product_disabled", 0),
            new TextKey("blocked_not_admin", 0),
            new TextKey("blocked_authority_pending", 0),
            new TextKey("cheat_season_override", 0),
            new TextKey("season_spring", 0),
            new TextKey("season_summer", 0),
            new TextKey("season_fall", 0),
            new TextKey("season_winter", 0),
            new TextKey("season_started", 1),
            new TextKey("cmd_status", 3),
            new TextKey("cmd_status_override", 3),
            new TextKey("cmd_inactive", 0),
            new TextKey("cmd_help", 0),
            new TextKey("cmd_waiting", 0),
            new TextKey("plant_frost_resistant", 0),
            new TextKey("plant_heat_protected", 0),
            new TextKey("plant_survived_winter", 0),
            new TextKey("plant_freezing", 1),
            new TextKey("plant_will_perish", 0),
            new TextKey("plant_exposed", 0),
            new TextKey("status_overheat", 0),
            new TextKey("status_overheat_tip", 0),
            new TextKey("status_season_tip", 2),
            new TextKey("hud_season", 2),
            new TextKey("hud_heat", 0),
            new TextKey("cfg_visuals_heading", 0),
            new TextKey("cfg_visuals__world_tint_name", 0),
            new TextKey("cfg_visuals__world_tint_help", 0),
            new TextKey("cfg_visuals__snow_cover_name", 0),
            new TextKey("cfg_visuals__snow_cover_help", 0),
            new TextKey("cfg_visuals__frozen_water_name", 0),
            new TextKey("cfg_visuals__frozen_water_help", 0),
            new TextKey("cfg_visuals__minimap_tint_name", 0),
            new TextKey("cfg_visuals__minimap_tint_help", 0),
            new TextKey("cfg_visuals__season_indicator_name", 0),
            new TextKey("cfg_visuals__season_indicator_help", 0),
            new TextKey("cfg_visuals__season_icons_name", 0),
            new TextKey("cfg_visuals__season_icons_help", 0),
            new TextKey("cfg_visuals__heat_meter_name", 0),
            new TextKey("cfg_visuals__heat_meter_help", 0),
            new TextKey("cfg_music_heading", 0),
            new TextKey("cfg_music__seasonal_music_name", 0),
            new TextKey("cfg_music__seasonal_music_help", 0),
            new TextKey("cfg_general_heading", 0),
            new TextKey("cfg_calendar_heading", 0),
            new TextKey("cfg_spring_heading", 0),
            new TextKey("cfg_summer_heading", 0),
            new TextKey("cfg_fall_heading", 0),
            new TextKey("cfg_winter_heading", 0),
            new TextKey("cfg_protection_heading", 0),
            new TextKey("cfg_climate_heading", 0),
            new TextKey("cfg_notices_heading", 0),
            new TextKey("cfg_cheats_heading", 0),
            new TextKey("cfg_general__enabled_name", 0),
            new TextKey("cfg_general__enabled_help", 0),
            new TextKey("cfg_protection__heat_protects_crops_name", 0),
            new TextKey("cfg_protection__heat_protects_crops_help", 0),
            new TextKey("cfg_protection__shield_generator_protects_name", 0),
            new TextKey("cfg_protection__shield_generator_protects_help", 0),
            new TextKey("cfg_protection__shield_generator_protects_in_winter_only_name", 0),
            new TextKey("cfg_protection__shield_generator_protects_in_winter_only_help", 0),
            new TextKey("cfg_protection__winter_hardy_crops_name", 0),
            new TextKey("cfg_protection__winter_hardy_crops_help", 0),
            new TextKey("cfg_protection__crops_perish_name", 0),
            new TextKey("cfg_protection__crops_perish_help", 0),
            new TextKey("cfg_protection__perish_from_day_name", 0),
            new TextKey("cfg_protection__perish_from_day_help", 0),
            new TextKey("cfg_protection__freeze_seconds_name", 0),
            new TextKey("cfg_protection__freeze_seconds_help", 0),
            new TextKey("cfg_protection__winter_harvest_chance_name", 0),
            new TextKey("cfg_protection__winter_harvest_chance_help", 0),
            new TextKey("cfg_protection__crops_to_control_name", 0),
            new TextKey("cfg_protection__crops_to_control_help", 0),
            new TextKey("cfg_climate__season_status_name", 0),
            new TextKey("cfg_climate__season_status_help", 0),
            new TextKey("cfg_climate__warm_clothes_overheat_name", 0),
            new TextKey("cfg_climate__warm_clothes_overheat_help", 0),
            new TextKey("cfg_climate__cooling_foods_name", 0),
            new TextKey("cfg_climate__cooling_foods_help", 0),
            new TextKey("cfg_notices__season_message_name", 0),
            new TextKey("cfg_notices__season_message_help", 0),
            new TextKey("cfg_cheats__season_override_name", 0),
            new TextKey("cfg_cheats__season_override_help", 0),
            new TextKey("cfg_calendar__days_in_spring_name", 0),
            new TextKey("cfg_calendar__days_in_spring_help", 0),
            new TextKey("cfg_spring__plants_growth_multiplier_name", 0),
            new TextKey("cfg_spring__plants_growth_multiplier_help", 0),
            new TextKey("cfg_spring__beehive_production_multiplier_name", 0),
            new TextKey("cfg_spring__beehive_production_multiplier_help", 0),
            new TextKey("cfg_spring__food_drain_multiplier_name", 0),
            new TextKey("cfg_spring__food_drain_multiplier_help", 0),
            new TextKey("cfg_spring__stamina_drain_multiplier_name", 0),
            new TextKey("cfg_spring__stamina_drain_multiplier_help", 0),
            new TextKey("cfg_spring__fireplace_drain_multiplier_name", 0),
            new TextKey("cfg_spring__fireplace_drain_multiplier_help", 0),
            new TextKey("cfg_spring__sap_collecting_speed_multiplier_name", 0),
            new TextKey("cfg_spring__sap_collecting_speed_multiplier_help", 0),
            new TextKey("cfg_spring__rested_buff_duration_multiplier_name", 0),
            new TextKey("cfg_spring__rested_buff_duration_multiplier_help", 0),
            new TextKey("cfg_spring__wind_intensity_multiplier_name", 0),
            new TextKey("cfg_spring__wind_intensity_multiplier_help", 0),
            new TextKey("cfg_spring__rain_protection_name", 0),
            new TextKey("cfg_spring__rain_protection_help", 0),
            new TextKey("cfg_calendar__days_in_summer_name", 0),
            new TextKey("cfg_calendar__days_in_summer_help", 0),
            new TextKey("cfg_summer__plants_growth_multiplier_name", 0),
            new TextKey("cfg_summer__plants_growth_multiplier_help", 0),
            new TextKey("cfg_summer__beehive_production_multiplier_name", 0),
            new TextKey("cfg_summer__beehive_production_multiplier_help", 0),
            new TextKey("cfg_summer__food_drain_multiplier_name", 0),
            new TextKey("cfg_summer__food_drain_multiplier_help", 0),
            new TextKey("cfg_summer__stamina_drain_multiplier_name", 0),
            new TextKey("cfg_summer__stamina_drain_multiplier_help", 0),
            new TextKey("cfg_summer__fireplace_drain_multiplier_name", 0),
            new TextKey("cfg_summer__fireplace_drain_multiplier_help", 0),
            new TextKey("cfg_summer__sap_collecting_speed_multiplier_name", 0),
            new TextKey("cfg_summer__sap_collecting_speed_multiplier_help", 0),
            new TextKey("cfg_summer__rested_buff_duration_multiplier_name", 0),
            new TextKey("cfg_summer__rested_buff_duration_multiplier_help", 0),
            new TextKey("cfg_summer__wind_intensity_multiplier_name", 0),
            new TextKey("cfg_summer__wind_intensity_multiplier_help", 0),
            new TextKey("cfg_summer__rain_protection_name", 0),
            new TextKey("cfg_summer__rain_protection_help", 0),
            new TextKey("cfg_calendar__days_in_fall_name", 0),
            new TextKey("cfg_calendar__days_in_fall_help", 0),
            new TextKey("cfg_fall__plants_growth_multiplier_name", 0),
            new TextKey("cfg_fall__plants_growth_multiplier_help", 0),
            new TextKey("cfg_fall__beehive_production_multiplier_name", 0),
            new TextKey("cfg_fall__beehive_production_multiplier_help", 0),
            new TextKey("cfg_fall__food_drain_multiplier_name", 0),
            new TextKey("cfg_fall__food_drain_multiplier_help", 0),
            new TextKey("cfg_fall__stamina_drain_multiplier_name", 0),
            new TextKey("cfg_fall__stamina_drain_multiplier_help", 0),
            new TextKey("cfg_fall__fireplace_drain_multiplier_name", 0),
            new TextKey("cfg_fall__fireplace_drain_multiplier_help", 0),
            new TextKey("cfg_fall__sap_collecting_speed_multiplier_name", 0),
            new TextKey("cfg_fall__sap_collecting_speed_multiplier_help", 0),
            new TextKey("cfg_fall__rested_buff_duration_multiplier_name", 0),
            new TextKey("cfg_fall__rested_buff_duration_multiplier_help", 0),
            new TextKey("cfg_fall__wind_intensity_multiplier_name", 0),
            new TextKey("cfg_fall__wind_intensity_multiplier_help", 0),
            new TextKey("cfg_fall__rain_protection_name", 0),
            new TextKey("cfg_fall__rain_protection_help", 0),
            new TextKey("cfg_calendar__days_in_winter_name", 0),
            new TextKey("cfg_calendar__days_in_winter_help", 0),
            new TextKey("cfg_winter__plants_growth_multiplier_name", 0),
            new TextKey("cfg_winter__plants_growth_multiplier_help", 0),
            new TextKey("cfg_winter__beehive_production_multiplier_name", 0),
            new TextKey("cfg_winter__beehive_production_multiplier_help", 0),
            new TextKey("cfg_winter__food_drain_multiplier_name", 0),
            new TextKey("cfg_winter__food_drain_multiplier_help", 0),
            new TextKey("cfg_winter__stamina_drain_multiplier_name", 0),
            new TextKey("cfg_winter__stamina_drain_multiplier_help", 0),
            new TextKey("cfg_winter__fireplace_drain_multiplier_name", 0),
            new TextKey("cfg_winter__fireplace_drain_multiplier_help", 0),
            new TextKey("cfg_winter__sap_collecting_speed_multiplier_name", 0),
            new TextKey("cfg_winter__sap_collecting_speed_multiplier_help", 0),
            new TextKey("cfg_winter__rested_buff_duration_multiplier_name", 0),
            new TextKey("cfg_winter__rested_buff_duration_multiplier_help", 0),
            new TextKey("cfg_winter__wind_intensity_multiplier_name", 0),
            new TextKey("cfg_winter__wind_intensity_multiplier_help", 0),
            new TextKey("cfg_winter__rain_protection_name", 0),
            new TextKey("cfg_winter__rain_protection_help", 0),
        });
    }
}
