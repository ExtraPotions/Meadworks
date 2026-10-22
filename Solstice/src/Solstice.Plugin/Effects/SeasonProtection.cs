// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from Seasons 1.10.6 by shudnal (https://github.com/shudnal/Seasons), commit 4054af6e8823d787921d673607e4942554872b66,
// Unlicense (public domain), Seasons.cs (IsIgnoredPosition, IsShieldedPosition, ProtectedWithHeat, the position caches).
// See THIRD_PARTY_NOTICES/Seasons-Unlicense.txt.
// Changed by expDARE, 2026: the facts are read here and the decision is the Solstice.Core rule (ProtectionRules); the caches are kept as in
// the reference, and are dropped when the world, the settings or the season change.
using System.Collections.Generic;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Effects;
using UnityEngine;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Whether a place is outside the seasons: indoors (a greenhouse), terrain the seasons never reach, or inside a shield generator dome;
    /// and whether a fire keeps crops warm. Main thread only.
    /// </summary>
    public static class SeasonProtection
    {
        const int CacheLimit = 15000;

        static readonly Dictionary<Vector2, bool> IgnoredTerrain = new Dictionary<Vector2, bool>();
        static readonly Dictionary<Vector3, bool> ShieldedPlaces = new Dictionary<Vector3, bool>();
        static int shieldChangeId;
        static int shieldGeneratorCache;

        /// <summary>The place is outside the seasonal effects. False while Solstice has no current season.</summary>
        public static bool IsProtected(Vector3 position)
        {
            var season = SolsticeSeasons.Current;
            if (season == null) return false;
            bool interior = Character.InInterior(position);
            bool ashlandsOrDeepNorth = false, highMountain = false;
            if (!interior) Terrain(position, out ashlandsOrDeepNorth, out highMountain);
            bool shielded = !interior && !ashlandsOrDeepNorth && !highMountain && IsShielded(position);
            return SolsticeSeasons.Protection.Decide(new PositionFacts(interior, ashlandsOrDeepNorth, highMountain, shielded), season.Value) != PositionProtection.None;
        }

        /// <summary>A fire (heat area) keeps crops alive in a growthless winter, if the server allows it.</summary>
        public static bool ProtectedWithHeat(Vector3 position) =>
            SolsticeSeasons.HeatProtectsCrops && EffectArea.IsPointInsideArea(position, EffectArea.Type.Heat);

        static void Terrain(Vector3 position, out bool ashlandsOrDeepNorth, out bool highMountain)
        {
            ashlandsOrDeepNorth = highMountain = false;
            var generator = WorldGenerator.instance;
            if (generator == null) return;
            var key = new Vector2(position.x, position.z);
            if (IgnoredTerrain.TryGetValue(key, out var cached))
            {
                // The cache keeps one answer per place; the two facts only matter together.
                ashlandsOrDeepNorth = cached;
                return;
            }
            if (IgnoredTerrain.Count > CacheLimit) IgnoredTerrain.Clear();
            var biome = generator.GetBiome(position);
            ashlandsOrDeepNorth = biome == Heightmap.Biome.AshLands || biome == Heightmap.Biome.DeepNorth
                || biome == Heightmap.Biome.Mountain && generator.GetBaseHeight(position.x, position.z, false) > WorldGenerator.mountainBaseHeightMin + 0.05f;
            IgnoredTerrain[key] = ashlandsOrDeepNorth;
        }

        static bool IsShielded(Vector3 position)
        {
            if (SolsticeSeasons.Current is Season season && !SolsticeSeasons.Protection.ShieldActive(season)) return false;
            int id = ShieldGenerator.m_instanceChangeID;
            if (shieldChangeId != id)
            {
                ShieldedPlaces.Clear();
                shieldChangeId = id;
            }
            if (ShieldedPlaces.TryGetValue(position, out var shielded)) return shielded;
            if (ShieldedPlaces.Count > CacheLimit) ShieldedPlaces.Clear();
            shielded = ShieldGenerator.IsInsideShieldCached(position, ref shieldGeneratorCache);
            ShieldedPlaces[position] = shielded;
            return shielded;
        }

        /// <summary>Drops the caches (world left, season changed).</summary>
        public static void Reset()
        {
            IgnoredTerrain.Clear();
            ShieldedPlaces.Clear();
            shieldChangeId = 0;
            shieldGeneratorCache = 0;
        }
    }
}
