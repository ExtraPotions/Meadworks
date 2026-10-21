// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Finds/FindFile.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using UnityEngine;
using YamlConfig;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Saga.Finds*.yml: registered with the synced configuration (written from the embedded default when
    /// missing, sent from the server to every player while the server binds the configuration, reloaded a few seconds
    /// after an edit, editable in game) and holding the tables of the last file set that parsed without errors. The
    /// hub applies a model as soon as it parses; the tables hold only names, so that is safe before the game's item
    /// database exists, on a dedicated server as on a client.
    /// </summary>
    public static class FindFile
    {
        public const string Pattern = "Saga.Finds*.yml";
        public const string SyncKey = Keys.FindsSync;
        public const string Resource = "Saga.config.Saga.Finds.yml";

        private static FindModel current = new FindModel();

        public static void Register(NativeConfiguration config)
        {
            config.AddYaml(new YamlFileSet(Pattern, SyncKey, () => new FindModel(), Apply)
            {
                DefaultContent = NativeConfiguration.EmbeddedResource(typeof(FindFile).Assembly, Resource),
            });
        }

        /// <summary>
        /// One find for a felled tree, picked by weight: from the tree's own table when the file lists its prefab,
        /// otherwise from its biome's. Null when the table is missing or empty, or every weight is 0.
        /// </summary>
        public static FindEntry Pick(string treePrefab, Heightmap.Biome biome)
        {
            FindModel model = current;
            if (!string.IsNullOrEmpty(treePrefab) && model.ByTree.TryGetValue(treePrefab, out List<FindEntry> tree))
                return PickFrom(tree);
            return model.ByBiome.TryGetValue(biome, out List<FindEntry> table) ? PickFrom(table) : null;
        }

        /// <summary>
        /// One find of a table, picked by weight (a find's share is weight / the table's total); null when every weight
        /// is 0. Shared with the Pickaxes finds (<see cref="MineFindFile"/>).
        /// </summary>
        public static FindEntry PickFrom(List<FindEntry> table)
        {
            float total = 0f;
            foreach (FindEntry find in table)
                total += find.Weight;
            if (total <= 0f)
                return null;
            float roll = Random.value * total;
            foreach (FindEntry find in table)
            {
                if (find.Weight > 0f && (roll -= find.Weight) < 0f)
                    return find;
            }
            return table.FindLast(f => f.Weight > 0f);
        }

        private static void Apply(YamlModel model)
        {
            current = (FindModel)model;
            FindPrefabs.ResetWarnings();
            NativeSaga.Log.LogInfo($"Finds applied: {current.ByBiome.Count} biomes and {current.ByTree.Count} trees, {current.Count} finds.");
        }
    }
}
