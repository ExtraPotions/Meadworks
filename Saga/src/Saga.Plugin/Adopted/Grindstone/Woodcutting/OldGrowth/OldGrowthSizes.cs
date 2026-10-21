// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/OldGrowth/OldGrowthSizes.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The scales each kind of tree grows at, so a fallen tree's size can be told within its own kind. Every source
    /// sets the tree's local scale directly, so all of them compare with the fallen tree's transform.localScale:
    /// <list type="bullet">
    /// <item>The world generator: ZoneSystem.PlaceVegetation rolls Random.Range(m_scaleMin, m_scaleMax) for each enabled
    /// ZoneVegetation entry and applies it with ZNetView.SetLocalScale. ZoneSystem.Start fills m_vegetation
    /// (SetupLocations: its own list, every LocationList and AltBiome) on every machine, dedicated servers and clients
    /// alike, and m_prefab is a direct prefab reference.</item>
    /// <item>Saplings: Plant.Grow rolls Random.Range(m_minScale, m_maxScale) for the tree it grows (m_grownPrefabs).</item>
    /// <item>A tree prefab whose own root scale is not 1 (AshlandsTree6, AshlandsTree6_big) can also stand at that scale.</item>
    /// </list>
    /// The table is built on first use for each ZoneSystem (once per world session), keyed by the tree's prefab name
    /// (<see cref="FellContext.TreePrefab"/>) and by its species, the log prefab (<see cref="FellContext.Species"/>). A tree
    /// uses its own range and falls back to its species' range; an empty or zero-width range is unknown.
    /// </summary>
    public static class OldGrowthSizes
    {
        private static readonly Dictionary<string, SizeRange> ByTree = new Dictionary<string, SizeRange>();
        private static readonly Dictionary<string, SizeRange> BySpecies = new Dictionary<string, SizeRange>();
        private static ZoneSystem builtFor;

        /// <summary>
        /// How big a tree of this scale is within its kind: 0 at the smallest scale the kind grows at, 1 at the largest,
        /// clamped. -1 when the kind's range is unknown.
        /// </summary>
        public static float Of(string treePrefab, string species, float scale)
        {
            if (!Ready())
                return -1f;
            SizeRange range = Find(treePrefab, species);
            return range.Known ? Mathf.Clamp01((scale - range.Min) / (range.Max - range.Min)) : -1f;
        }

        private static SizeRange Find(string treePrefab, string species)
        {
            if (treePrefab != null && ByTree.TryGetValue(treePrefab, out SizeRange tree) && tree.Known)
                return tree;
            return species != null && BySpecies.TryGetValue(species, out SizeRange kind) ? kind : default;
        }

        private static bool Ready()
        {
            ZoneSystem zones = ZoneSystem.instance;
            if (zones == null || ZNetScene.instance == null)
                return false;
            if (builtFor != zones)
                Build(zones);
            return true;
        }

        private static void Build(ZoneSystem zones)
        {
            ByTree.Clear();
            BySpecies.Clear();
            AddVegetation(zones.m_vegetation);
            AddPrefabs(PrefabIndex.Scene());
            builtFor = zones;
            NativeSaga.Log.LogInfo($"Old growth: size ranges for {ByTree.Count} trees of {BySpecies.Count} kinds.");
        }

        private static void AddVegetation(List<ZoneSystem.ZoneVegetation> vegetation)
        {
            foreach (ZoneSystem.ZoneVegetation veg in vegetation)
            {
                TreeBase tree = veg != null && veg.m_enable && veg.m_prefab != null ? veg.m_prefab.GetComponent<TreeBase>() : null;
                if (tree != null)
                    Add(tree, veg.m_scaleMin, veg.m_scaleMax);
            }
        }

        /// <summary>Tree prefabs scaled away from 1, and the trees saplings grow into (<see cref="PrefabIndex"/>).</summary>
        private static void AddPrefabs(PrefabIndex.ScenePrefabs prefabs)
        {
            foreach (TreeBase tree in prefabs.Trees)
            {
                float scale = tree.transform.localScale.x;
                if (!Mathf.Approximately(scale, 1f))
                    Add(tree, scale, scale);
            }
            foreach (Plant plant in prefabs.Plants)
                AddSapling(plant);
        }

        private static void AddSapling(Plant plant)
        {
            foreach (GameObject grown in plant.m_grownPrefabs)
            {
                TreeBase tree = grown != null ? grown.GetComponent<TreeBase>() : null;
                if (tree != null)
                    Add(tree, plant.m_minScale, plant.m_maxScale);
            }
        }

        /// <summary>Widens the tree's and its species' ranges; names are read as <see cref="Felling"/> reads them.</summary>
        private static void Add(TreeBase tree, float from, float to)
        {
            SizeRange range = new SizeRange(Mathf.Min(from, to), Mathf.Max(from, to));
            string species = tree.m_logPrefab != null ? tree.m_logPrefab.name : WoodSkill.PrefabName(tree);
            Widen(ByTree, WoodSkill.PrefabName(tree), range);
            Widen(BySpecies, species, range);
        }

        private static void Widen(Dictionary<string, SizeRange> table, string key, SizeRange range) =>
            table[key] = table.TryGetValue(key, out SizeRange known) ? known.Union(range) : range;

        /// <summary>A scale range; default (0..0) is unknown.</summary>
        private readonly struct SizeRange
        {
            public SizeRange(float min, float max)
            {
                Min = min;
                Max = max;
            }

            public float Min { get; }
            public float Max { get; }
            public bool Known => Max > Min;

            public SizeRange Union(SizeRange other) => new SizeRange(Mathf.Min(Min, other.Min), Mathf.Max(Max, other.Max));
        }
    }
}
