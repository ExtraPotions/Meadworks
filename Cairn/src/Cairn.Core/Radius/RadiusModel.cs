// Cairn - ExtraPotions Meadworks exploration radius.
// Portions Copyright 2026 Crystal Ferrai, Pathfinder 2.2.5 (Pathfinder/PathfinderPlugin.cs at
// f8a2cb3edca0417d54281d1353680dff46c4ed62, https://github.com/CrystalFerrai/ValheimMods).
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Changed by expDARE, 2026: the radius computation (GetExploreRadius, GetColorMagnitude, GetLocationModifier) and the
// config clamps (ClampConfig) moved out of Unity into a pure function over explicit inputs; ConditionalConfigSync and the
// HUD text objects removed; Minimum Radius is used as at most Maximum Radius instead of rewriting the other entry; added
// Keel's sea factor, the Exploration skill's land factor and penalty reductions, the Q1 hold and the radius-off rule.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later AND Apache-2.0
using System;
using System.Collections.Generic;
using ExtraPotions.Cairn.Config;

namespace ExtraPotions.Cairn.Radius
{
    /// <summary>A light colour already multiplied by its intensity; alpha is ignored (Pathfinder).</summary>
    public readonly struct Rgb
    {
        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public float R { get; }
        public float G { get; }
        public float B { get; }

        public float Magnitude => (float)Math.Sqrt(R * R + G * G + B * B);
    }

    /// <summary>The biomes the location term distinguishes.</summary>
    public enum RadiusBiome { Other, Meadows, BlackForest, Plains }

    /// <summary>
    /// What the game reports this frame. <see cref="FogDensity"/> must be the game's density before Cairn removes distance
    /// fog (design A5: clearing fog visually does not widen the radius).
    /// </summary>
    public sealed class RadiusInputs
    {
        public bool Interior { get; set; }

        /// <summary>The local player or a player within 21 m is attached to a ship.</summary>
        public bool ShipNearby { get; set; }

        public Rgb DirectionalLight { get; set; }
        public Rgb AmbientLight { get; set; }
        public float FogDensity { get; set; }

        /// <summary>Names of the current environment's particle systems.</summary>
        public IReadOnlyList<string> ParticleSystems { get; set; } = Array.Empty<string>();

        /// <summary>Height above the water level in metres.</summary>
        public float Altitude { get; set; }

        public RadiusBiome Biome { get; set; }

        /// <summary><c>WorldGenerator.InForest</c> at the player (used in Meadows).</summary>
        public bool InForest { get; set; }

        /// <summary><c>WorldGenerator.GetForestFactor</c> at the player (used in Plains).</summary>
        public float ForestFactor { get; set; } = 1f;
    }

    /// <summary>The radius settings in effect (design 4.1), already normalized.</summary>
    public readonly struct RadiusValues
    {
        public RadiusValues(float minimum, float maximum, float land, float sea, float altitudeBonus, float forestPenalty, float daylightScale, float weatherScale)
        {
            Minimum = minimum;
            Maximum = maximum;
            Land = land;
            Sea = sea;
            AltitudeBonus = altitudeBonus;
            ForestPenalty = forestPenalty;
            DaylightScale = daylightScale;
            WeatherScale = weatherScale;
        }

        public static RadiusValues Defaults => new RadiusValues(
            CairnSettings.Radius.MinimumRadius.Default, CairnSettings.Radius.MaximumRadius.Default, CairnSettings.Radius.LandExploreRadius.Default,
            CairnSettings.Radius.SeaExploreRadius.Default, CairnSettings.Radius.AltitudeRadiusBonus.Default, CairnSettings.Radius.ForestRadiusPenalty.Default,
            CairnSettings.Radius.DaylightRadiusScale.Default, CairnSettings.Radius.WeatherRadiusScale.Default);

        public float Minimum { get; }
        public float Maximum { get; }
        public float Land { get; }
        public float Sea { get; }
        public float AltitudeBonus { get; }
        public float ForestPenalty { get; }
        public float DaylightScale { get; }
        public float WeatherScale { get; }

        public RadiusValues With(float? minimum = null, float? maximum = null, float? land = null, float? sea = null) =>
            new RadiusValues(minimum ?? Minimum, maximum ?? Maximum, land ?? Land, sea ?? Sea, AltitudeBonus, ForestPenalty, DaylightScale, WeatherScale);

        /// <summary>User decision Q1: land, sea and maximum capped at Pathfinder's defaults.</summary>
        public RadiusValues Held() => With(
            maximum: Math.Min(Maximum, GatePolicy.HeldMaximumRadius),
            land: Math.Min(Land, GatePolicy.HeldLandRadius),
            sea: Math.Min(Sea, GatePolicy.HeldSeaRadius));
    }

    /// <summary>
    /// Meadworks factors (design A8, D3, D4): Keel's Sailing sea factor (or GrindstoneSkills' API factor), the Exploration
    /// skill's land factor, and the skill's weather and night penalty reductions (shares 0 to 1).
    /// </summary>
    public readonly struct RadiusFactors
    {
        public RadiusFactors(float sea, float land, float weatherReduction, float nightReduction)
        {
            Sea = Finite(sea, 1f, 0f, float.MaxValue);
            Land = Finite(land, 1f, 0f, float.MaxValue);
            WeatherReduction = Finite(weatherReduction, 0f, 0f, 1f);
            NightReduction = Finite(nightReduction, 0f, 0f, 1f);
        }

        public static RadiusFactors None => new RadiusFactors(1f, 1f, 0f, 0f);

        public float Sea { get; }
        public float Land { get; }
        public float WeatherReduction { get; }
        public float NightReduction { get; }

        internal static float Finite(float value, float fallback, float min, float max) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Min(max, Math.Max(min, value));
    }

    /// <summary>The computed radius and the terms Pathfinder's variables display shows.</summary>
    public readonly struct RadiusResult
    {
        public RadiusResult(float radius, float baseRadius, float multiplier, float light, float weather, float altitude, float location, float factor)
        {
            Radius = radius;
            Base = baseRadius;
            Multiplier = multiplier;
            LightTerm = light;
            WeatherTerm = weather;
            AltitudeTerm = altitude;
            LocationTerm = location;
            Factor = factor;
        }

        public float Radius { get; }
        public float Base { get; }
        public float Multiplier { get; }
        public float LightTerm { get; }
        public float WeatherTerm { get; }
        public float AltitudeTerm { get; }
        public float LocationTerm { get; }

        /// <summary>The Meadworks factor applied after Pathfinder's multiplier (Keel at sea, skill on land).</summary>
        public float Factor { get; }
    }

    public static class RadiusModel
    {
        /// <summary>Pathfinder: a longship is about 20 units long; players within 21 m share the pilot's sea radius.</summary>
        public const float ShipShareRange = 21f;

        public static RadiusResult Compute(RadiusInputs inputs, RadiusValues values, RadiusFactors factors)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            var maximum = Clamp(values.Maximum, 0f, 10000f);
            var minimum = Math.Min(Clamp(values.Minimum, 0f, 10000f), maximum);
            var land = Clamp(values.Land, 0f, 2000f);
            var sea = Clamp(values.Sea, 0f, 2000f);
            var altitudeBonus = Clamp(values.AltitudeBonus, 0f, 2f);
            var forestPenalty = Clamp(values.ForestPenalty, 0f, 1f);
            var daylightScale = Clamp(values.DaylightScale, 0f, 1f);
            var weatherScale = Clamp(values.WeatherScale, 0f, 1f);

            if (inputs.Interior)
            {
                // Dungeons sit high above the world; exploring the map from inside one makes no sense.
                var inside = Math.Max(land * 0.2f, 10f);
                return new RadiusResult(inside, land, 1f, 0f, 0f, 0f, 0f, 1f);
            }

            var baseRadius = inputs.ShipNearby ? sea : land;
            var multiplier = 1f;

            // The larger of directional and ambient light, minus 1, becomes a term of the multiplier.
            var light = Math.Max(inputs.DirectionalLight.Magnitude, inputs.AmbientLight.Magnitude);
            if (!IsFinite(light)) light = 1f;
            var lightTerm = (light - 1f) * daylightScale;
            if (lightTerm < 0f) lightTerm *= 1f - factors.NightReduction;
            multiplier += lightTerm;

            // Weather: some particle systems heavily obstruct view; a snowstorm lowers visibility more by day than by night.
            var particles = 0f;
            foreach (var name in inputs.ParticleSystems ?? Array.Empty<string>())
            {
                if (string.Equals(name, "Mist", StringComparison.OrdinalIgnoreCase)) particles += 0.5f;
                if (string.Equals(name, "SnowStorm", StringComparison.OrdinalIgnoreCase)) particles += 0.7f * light;
            }

            // Fog density ranges about 0.001 to 0.15 in environment data; times 10 gives a meaningful range.
            var density = IsFinite(inputs.FogDensity) ? inputs.FogDensity : 0f;
            var fog = Clamp(density * 10f + particles, 0f, 1.5f);
            var weatherTerm = -fog * weatherScale * (1f - factors.WeatherReduction);
            multiplier += weatherTerm;

            // Sea level is about 30, the tallest common mountains about 220; the bonus stops at 400 above water.
            var altitude = Clamp(IsFinite(inputs.Altitude) ? inputs.Altitude : 0f, 0f, 400f);
            var adjustedAltitude = altitude / 100f * Math.Max(0.05f, 1f - particles);
            var altitudeTerm = adjustedAltitude * altitudeBonus;
            multiplier += altitudeTerm;

            var locationTerm = Location(inputs, adjustedAltitude, altitudeBonus, forestPenalty, daylightScale);
            multiplier += locationTerm;

            if (multiplier > 5f) multiplier = 5f;
            if (multiplier < 0.2f) multiplier = 0.2f;

            var factor = inputs.ShipNearby ? factors.Sea : factors.Land;
            var radius = Clamp(baseRadius * multiplier * factor, minimum, maximum);
            return new RadiusResult(radius, baseRadius, multiplier, lightTerm, weatherTerm, altitudeTerm, locationTerm, factor);
        }

        /// <summary>Cairn's radius switched off (design 4.1 Enabled): the game's radius with Keel's and the skill's factors only.</summary>
        public static float Disabled(float gameRadius, bool shipNearby, RadiusFactors factors)
        {
            var radius = IsFinite(gameRadius) && gameRadius > 0f ? gameRadius : 0f;
            return radius * (shipNearby ? factors.Sea : factors.Land);
        }

        // Forest thresholds follow the game's minimap mask colours (Pathfinder).
        private static float Location(RadiusInputs inputs, float altitude, float altitudeBonus, float forestPenalty, float daylightScale)
        {
            var penalty = forestPenalty + altitude * altitudeBonus * forestPenalty;
            switch (inputs.Biome)
            {
                case RadiusBiome.BlackForest:
                    // Small extra penalty for the Black Forest's high daylight values.
                    return -penalty - 0.25f * daylightScale;
                case RadiusBiome.Meadows:
                    return inputs.InForest ? -penalty : 0f;
                case RadiusBiome.Plains:
                    // Small extra bonus for the Plains' low daylight values.
                    return (inputs.ForestFactor < 0.8f ? -penalty : 0f) + 0.1f * daylightScale;
                default:
                    return 0f;
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Clamp(float value, float min, float max) => IsFinite(value) ? Math.Min(max, Math.Max(min, value)) : min;
    }
}
