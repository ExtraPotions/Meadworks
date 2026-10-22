// Solstice - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from Seasons 1.10.6 by shudnal (https://github.com/shudnal/Seasons), commit 4054af6e8823d787921d673607e4942554872b66,
// Unlicense (public domain), Seasons.cs (IsIgnoredPosition, IsShieldedPosition, IsShieldProtectionActive, ProtectedWithHeat, PlantWillSurviveWinter)
// and SeasonState/SeasonStatePatches.cs (Pickable_UpdateHealth_PlantsPerishInWinter). See THIRD_PARTY_NOTICES/Seasons-Unlicense.txt.
// Changed by expDARE, 2026: the decisions are pure functions of facts the plugin reads from the game (indoors, terrain, shield dome, fire);
// the hardy crop list is parsed from one server setting; the reference's default values are kept.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Effects
{
    /// <summary>Why a place is outside the seasons.</summary>
    public enum PositionProtection { None, Interior, Terrain, Shield }

    /// <summary>What the game says about a place.</summary>
    public readonly struct PositionFacts
    {
        public PositionFacts(bool interior, bool ashlandsOrDeepNorth, bool highMountain, bool insideShield)
        {
            Interior = interior;
            AshlandsOrDeepNorth = ashlandsOrDeepNorth;
            HighMountain = highMountain;
            InsideShield = insideShield;
        }

        /// <summary>Inside a closed, roofed building (a greenhouse, <c>Character.InInterior</c>).</summary>
        public bool Interior { get; }
        public bool AshlandsOrDeepNorth { get; }
        public bool HighMountain { get; }
        public bool InsideShield { get; }
    }

    /// <summary>The shield generator settings, both on by default as in the reference.</summary>
    public sealed class ProtectionRules
    {
        readonly bool shield;
        readonly bool winterOnly;

        public ProtectionRules(bool shieldProtects, bool shieldWinterOnly)
        {
            shield = shieldProtects;
            winterOnly = shieldWinterOnly;
        }

        /// <summary>The shield dome protects in this season (IsShieldProtectionActive).</summary>
        public bool ShieldActive(Season season) => shield && (!winterOnly || season == Season.Winter);

        /// <summary>Indoors first, then terrain the seasons never reach, then a shield dome (all year, or winter only).</summary>
        public PositionProtection Decide(PositionFacts facts, Season season)
        {
            if (facts.Interior) return PositionProtection.Interior;
            if (facts.AshlandsOrDeepNorth || facts.HighMountain) return PositionProtection.Terrain;
            if (ShieldActive(season) && facts.InsideShield) return PositionProtection.Shield;
            return PositionProtection.None;
        }
    }

    /// <summary>The crops that live through winter: the server's list of grown prefab names, or any plant that tolerates cold.</summary>
    public sealed class WinterCrops
    {
        readonly HashSet<string> names;

        WinterCrops(HashSet<string> names) => this.names = names;

        public static WinterCrops Parse(string? list) =>
            new WinterCrops(new HashSet<string>((list ?? "").Split(',').Select(n => n.Trim().ToLowerInvariant()).Where(n => n.Length > 0), StringComparer.Ordinal));

        public static string Normalize(string? list) => string.Join(",", (list ?? "").Split(',').Select(n => n.Trim()).Where(n => n.Length > 0));

        public bool Survives(bool toleratesCold, IEnumerable<string> grownPrefabNames) =>
            toleratesCold || grownPrefabNames.Any(n => names.Contains((n ?? "").ToLowerInvariant()));
    }

    /// <summary>The winter rule for planted crops (Plant.UpdateHealth).</summary>
    public static class ColdPlants
    {
        /// <summary>A healthy plant is too cold when winter stops growth and nothing saves it: not hardy, no fire nearby.</summary>
        public static bool IsTooCold(bool healthy, Season season, float growthMultiplier, bool hardy, bool heatProtected) =>
            healthy && season == Season.Winter && growthMultiplier == 0f && !hardy && !heatProtected;

        /// <summary>A plant put in the ground (age 0) in a growthless winter is judged as if it were 11 seconds old, so it is not skipped as brand new.</summary>
        public static double AdjustedAge(double secondsSincePlanted, Season season, float growthMultiplier) =>
            secondsSincePlanted == 0d && growthMultiplier == 0f && season == Season.Winter ? 11d : secondsSincePlanted;
    }
}
