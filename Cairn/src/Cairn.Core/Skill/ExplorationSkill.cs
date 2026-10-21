// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Exploration skill (design D1 to D5). The perk model (a value "At 100" reached linearly from level
// 0) follows GrindstoneSkills 0.14.1 as a pattern only; no GrindstoneSkills file is copied.
using System;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Radius;

namespace ExtraPotions.Cairn.Skill
{
    /// <summary>The Exploration skill settings in effect (design 4.5), already normalized.</summary>
    public readonly struct SkillValues
    {
        public SkillValues(bool enabled, float experiencePerSquareKilometre, float locationDiscoveryExperience, float newBiomeExperience,
            float landRadiusAt100, float weatherPenaltyReductionAt100, float nightPenaltyReductionAt100, int vantageLevel, float vantageCooldown)
        {
            Enabled = enabled;
            ExperiencePerSquareKilometre = experiencePerSquareKilometre;
            LocationDiscoveryExperience = locationDiscoveryExperience;
            NewBiomeExperience = newBiomeExperience;
            LandRadiusAt100 = landRadiusAt100;
            WeatherPenaltyReductionAt100 = weatherPenaltyReductionAt100;
            NightPenaltyReductionAt100 = nightPenaltyReductionAt100;
            VantageLevel = vantageLevel;
            VantageCooldown = vantageCooldown;
        }

        public static SkillValues Defaults => new SkillValues(CairnSettings.Skill.Enabled.Default, CairnSettings.Skill.ExperiencePerSquareKilometre.Default,
            CairnSettings.Skill.LocationDiscoveryExperience.Default, CairnSettings.Skill.NewBiomeExperience.Default,
            CairnSettings.Skill.LandRadiusAt100.Default, CairnSettings.Skill.WeatherPenaltyReductionAt100.Default,
            CairnSettings.Skill.NightPenaltyReductionAt100.Default, CairnSettings.Skill.VantageLevel.Default, CairnSettings.Skill.VantageCooldown.Default);

        public bool Enabled { get; }
        public float ExperiencePerSquareKilometre { get; }
        public float LocationDiscoveryExperience { get; }
        public float NewBiomeExperience { get; }
        public float LandRadiusAt100 { get; }
        public float WeatherPenaltyReductionAt100 { get; }
        public float NightPenaltyReductionAt100 { get; }
        public int VantageLevel { get; }
        public float VantageCooldown { get; }

        public SkillValues With(bool? enabled = null, int? vantageLevel = null) =>
            new SkillValues(enabled ?? Enabled, ExperiencePerSquareKilometre, LocationDiscoveryExperience, NewBiomeExperience, LandRadiusAt100,
                WeatherPenaltyReductionAt100, NightPenaltyReductionAt100, vantageLevel ?? VantageLevel, VantageCooldown);
    }

    public static class ExplorationSkill
    {
        /// <summary>Skill identity registered through Crucible CS1: product-independent, like Keel's meadworks_sailing (user decision 2026-10-08, replacing design D1's cairn_exploration).</summary>
        public const string SkillId = "meadworks_exploration";

        /// <summary>The game's <c>Skills.SkillType</c> number of <see cref="SkillId"/> (pinned; checked against CS1 at run time).</summary>
        public const int GameType = 1828632269;

        /// <summary>English name: CS1's fallback and, in lower case, the console word.</summary>
        public const string Name = "Exploration";

        /// <summary>English description: CS1's fallback when the catalog lacks the key.</summary>
        public const string Description = "Uncovering the map. Widens your map radius on land and softens the loss to darkness and weather.";

        /// <summary>C05 key of the skill name (CS1 <c>textKey</c>); CS1 reads the description from this key plus <c>_description</c>.</summary>
        public const string TextKey = "skill_exploration";

        /// <summary>Prefab whose piece icon is the skill icon (design D1: the map table, read at run time, never shipped).</summary>
        public const string IconPrefab = "piece_cartographytable";

        /// <summary>Highest speed in meters per second that still counts as standing still for Vantage.</summary>
        public const float VantageStillSpeed = 0.2f;

        /// <summary>Seconds standing still before Vantage fires (design D5).</summary>
        public const float VantageStillSeconds = 3f;

        /// <summary>Minimum height above water for Vantage (design D5).</summary>
        public const float VantageMinimumAltitude = 30f;

        /// <summary>The share of an "At 100" percent value at <paramref name="level"/>: linear from 0 at level 0, level clamped 0 to 100.</summary>
        public static float Share(float at100Percent, float level)
        {
            if (float.IsNaN(level) || float.IsNaN(at100Percent) || float.IsInfinity(at100Percent)) return 0f;
            var l = Math.Min(100f, Math.Max(0f, level));
            return at100Percent / 100f * (l / 100f);
        }

        /// <summary>Radius factors (design A8, D3, D4): Keel's sea factor passes through; the skill adds its land factor and reductions while enabled.</summary>
        public static RadiusFactors Factors(float level, SkillValues values, float keelSeaFactor)
        {
            if (!values.Enabled) return new RadiusFactors(keelSeaFactor, 1f, 0f, 0f);
            return new RadiusFactors(keelSeaFactor, 1f + Share(values.LandRadiusAt100, level),
                Share(values.WeatherPenaltyReductionAt100, level), Share(values.NightPenaltyReductionAt100, level));
        }

        /// <summary>Experience for <paramref name="newTexels"/> newly explored minimap texels of <paramref name="texelMetres"/> meters (design D2).</summary>
        public static float AreaExperience(int newTexels, float texelMetres, SkillValues values)
        {
            if (!values.Enabled || newTexels <= 0 || !(texelMetres > 0f) || float.IsInfinity(texelMetres)) return 0f;
            var squareKilometres = newTexels * (double)texelMetres * texelMetres / 1_000_000d;
            return (float)(squareKilometres * values.ExperiencePerSquareKilometre);
        }

        public static float DiscoveryExperience(SkillValues values) => values.Enabled ? values.LocationDiscoveryExperience : 0f;

        public static float BiomeExperience(SkillValues values) => values.Enabled ? values.NewBiomeExperience : 0f;

        /// <summary>Vantage is available at its level; a level above 100 turns it off.</summary>
        public static bool VantageUnlocked(float level, SkillValues values) =>
            values.Enabled && values.VantageLevel <= 100 && !float.IsNaN(level) && level >= values.VantageLevel;

        /// <summary>Vantage uncovers once at twice the current radius.</summary>
        public static float VantageRadius(float currentRadius) => currentRadius * 2f;

        /// <summary>Standing still for Vantage: on the ground and no faster than <see cref="VantageStillSpeed"/>.</summary>
        public static bool StandingStill(float speed, bool onGround) => onGround && !float.IsNaN(speed) && speed <= VantageStillSpeed;

        /// <summary>
        /// Whether Vantage's still timer resets this frame: moving, off the ground, or inside a dungeon or other interior
        /// (interiors sit far above the water, so the altitude rule alone would let them fire).
        /// </summary>
        public static bool VantageResets(float speed, bool onGround, bool interior) => interior || !StandingStill(speed, onGround);

        /// <summary>The radius Vantage doubles: Cairn's current radius when valid, else the game's, else 0.</summary>
        public static float VantageBase(float current, float game)
        {
            if (current > 0f && !float.IsInfinity(current)) return current;
            if (game > 0f && !float.IsInfinity(game)) return game;
            return 0f;
        }
    }

    /// <summary>
    /// Vantage (design D5): standing still for 3 s on ground at least 30 m above water uncovers once at twice the radius,
    /// then waits its cooldown. One tracker per local player; times in seconds.
    /// </summary>
    public sealed class VantageTracker
    {
        private float still;
        private float? lastFired;

        /// <summary>
        /// Advances by <paramref name="deltaSeconds"/>; true when Vantage fires now. Vantage is off while Cairn's radius
        /// module steps aside for Pathfinder (<paramref name="radiusSteppedAside"/>, user decision 2026-10-08).
        /// </summary>
        public bool Tick(float now, float deltaSeconds, bool moving, float altitude, float level, SkillValues values, bool radiusSteppedAside = false)
        {
            if (radiusSteppedAside || moving || float.IsNaN(deltaSeconds) || deltaSeconds < 0f)
            {
                still = 0f;
                return false;
            }
            still += deltaSeconds;
            if (still < ExplorationSkill.VantageStillSeconds) return false;
            if (!ExplorationSkill.VantageUnlocked(level, values) || !(altitude >= ExplorationSkill.VantageMinimumAltitude)) return false;
            if (lastFired.HasValue && now - lastFired.Value < values.VantageCooldown) return false;
            lastFired = now;
            still = 0f;
            return true;
        }

        public void Reset()
        {
            still = 0f;
            lastFired = null;
        }
    }
}
