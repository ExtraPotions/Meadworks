// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: the categories follow TimeWeatherHud 1.1.0's README (no license; no code or art used).
// The environment names are the game's identifiers as listed by NoFogBruh 2.1.2's WeatherEventsComponent (names only).
using System;
using System.Collections.Generic;

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>The weather marks the HUD shows (design C2).</summary>
    public enum WeatherCategory { Clear, Cloudy, Mist, Rain, Thunderstorm, Snow, Blizzard, AshRain, Boss, Indoors }

    public static class WeatherCategories
    {
        /// <summary>Fog density from which an unknown environment counts as cloudy.</summary>
        public const float CloudyFogDensity = 0.02f;

        private static readonly Dictionary<string, WeatherCategory> Table = Build();

        /// <summary>The known environment names, case-insensitive.</summary>
        public static IReadOnlyDictionary<string, WeatherCategory> Known => Table;

        /// <summary>
        /// The category of the current environment. Inside a dungeon or cave it is Indoors; an unknown (modded) environment
        /// is Cloudy when it is foggy or rainy, else Clear.
        /// </summary>
        public static WeatherCategory Of(string? environment, float fogDensity, bool rainy, bool interior = false)
        {
            if (interior) return WeatherCategory.Indoors;
            if (environment != null && Table.TryGetValue(environment.Trim(), out var known)) return known;
            return rainy || fogDensity >= CloudyFogDensity ? WeatherCategory.Cloudy : WeatherCategory.Clear;
        }

        /// <summary>Tempest's event environments (Tempest EnvironmentPlan names) and the event token each belongs to. Names
        /// only: Cairn needs no Tempest reference, and without Tempest these environments never occur.</summary>
        private static readonly Dictionary<string, string> EventTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tempest_Blizzard"] = "blizzard", ["Tempest_Whiteout"] = "blizzard", ["Tempest_Storm"] = "thunderstorm",
            ["Tempest_LightningStorm"] = "lightning_storm", ["Tempest_Fog"] = "dense_fog", ["Tempest_AshStorm"] = "ash_storm",
            ["Tempest_MeteorShower"] = "meteor_shower",
        };

        /// <summary>The Tempest event token of a Tempest event environment, or null for any other environment.</summary>
        public static string? EventToken(string? environment) =>
            environment != null && EventTokens.TryGetValue(environment.Trim(), out var token) ? token : null;

        private static Dictionary<string, WeatherCategory> Build()
        {
            var t = new Dictionary<string, WeatherCategory>(StringComparer.OrdinalIgnoreCase);
            void Add(WeatherCategory c, params string[] names) { foreach (var n in names) t.Add(n, c); }
            Add(WeatherCategory.Clear, "Clear", "Twilight_Clear", "Heath clear", "Mistlands_clear");
            Add(WeatherCategory.Cloudy, "Darklands_dark", "Ghosts", "Morkhalla", "TheHollow", "JotunInvasion_meadows", "JotunInvasion_blackforest",
                "JotunInvasion_swamp", "JotunInvasion_mountain", "JotunInvasion_plains", "JotunInvasion_mistlands");
            Add(WeatherCategory.Mist, "Misty", "DeepForest Mist", "Ashlands_misty", "Tempest_Fog");
            Add(WeatherCategory.Rain, "Rain", "LightRain", "SwampRain", "Mistlands_rain");
            Add(WeatherCategory.Thunderstorm, "ThunderStorm", "Mistlands_thunder", "nofogts", "Ashlands_storm", "Ashlands_SeaStorm", "Tempest_Storm",
                "Tempest_LightningStorm");
            Add(WeatherCategory.Snow, "Snow", "Twilight_Snow");
            // Tempest's event environments (Tempest EnvironmentPlan): without them the event read as Cloudy.
            Add(WeatherCategory.Blizzard, "SnowStorm", "Twilight_SnowStorm", "Tempest_Blizzard", "Tempest_Whiteout");
            Add(WeatherCategory.AshRain, "Ashrain", "Ashlands_ashrain", "Ashlands_CinderRain", "Ashlands_meteorshower", "Tempest_AshStorm", "Tempest_MeteorShower");
            Add(WeatherCategory.Boss, "Eikthyr", "Eikthyr_no_lightning", "GDKing", "Bonemass", "Moder", "GoblinKing", "Queen", "Fader", "DN_Bossroom");
            Add(WeatherCategory.Indoors, "Caves", "CavesHildir", "Crypt", "CryptHildir", "SunkenCrypt", "InfectedMine");
            return t;
        }
    }
}
