// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Reads <c>item_tiers:</c> (item-tier.md; item levels 1-8 since classes-and-tiers.md section 2) and <c>biomes:</c>.
    /// Prefab names are checked against the game later, as warnings.
    /// </summary>
    internal static class TierMapParser
    {
        /// <summary>The highest item level and biome tier (8, Deep North).</summary>
        public const int MaxTier = 8;

        public static ItemTierMaps ParseItemTiers(MapReader root)
        {
            MapReader? sub = root.Sub("item_tiers");
            if (sub == null)
            {
                return new ItemTierMaps();
            }
            MapReader r = sub.Value;
            r.Unknown("items", "materials", "material_depth", "stations", "station_levels", "fallback_tier");
            return new ItemTierMaps
            {
                Items = YamlLists.IntMap(r, "items", 1, MaxTier),
                Materials = YamlLists.IntMap(r, "materials", 1, MaxTier),
                MaterialDepth = r.Int("material_depth", 3, 0, 10),
                Stations = YamlLists.IntMap(r, "stations", 1, MaxTier),
                StationLevels = ReadStationLevels(r),
                FallbackTier = r.Int("fallback_tier", 1, 1, MaxTier),
            };
        }

        private static Dictionary<string, IReadOnlyDictionary<int, int>> ReadStationLevels(MapReader r)
        {
            Dictionary<string, IReadOnlyDictionary<int, int>> result = new Dictionary<string, IReadOnlyDictionary<int, int>>();
            MapReader? sub = r.Sub("station_levels");
            if (sub == null)
            {
                return result;
            }
            foreach (KeyValuePair<string, YamlNode> pair in YamlLists.Pairs(sub.Value.Map))
            {
                result[pair.Key] = LevelRow(sub.Value, pair.Key);
            }
            return result;
        }

        private static Dictionary<int, int> LevelRow(MapReader sub, string station)
        {
            Dictionary<int, int> row = new Dictionary<int, int>();
            foreach (KeyValuePair<string, int> level in YamlLists.IntMap(sub, station, 1, MaxTier))
            {
                if (Numbers.TryInt(level.Key, out int minLevel) && minLevel >= 1)
                {
                    row[minLevel] = level.Value;
                }
                else
                {
                    sub.Issues.Error($"{sub.At(station)}.{level.Key}", sub.Node(station), "keys are station levels (whole numbers from 1)");
                }
            }
            return row;
        }

        public static Dictionary<string, int> ParseBiomes(MapReader root) => YamlLists.IntMap(root, "biomes", 1, MaxTier);
    }
}
