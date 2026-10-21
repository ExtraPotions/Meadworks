// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Rewards/SnagFile.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using YamlConfig;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Saga.Snags*.yml: registered with the synced configuration on every machine (written from the embedded
    /// default when missing, sent from the server to every player while the server binds the configuration, reloaded a
    /// few seconds after an edit, editable in game) and holding the tables of the last file set that parsed without
    /// errors. The tables hold only names, so applying them before the item database exists is safe. The same pattern as
    /// the Mine Finds (<see cref="MineFindFile"/>), per biome only.
    /// </summary>
    public static class SnagFile
    {
        public const string Pattern = "Saga.Snags*.yml";
        public const string SyncKey = Keys.SnagsSync;
        public const string Resource = "Saga.config.Saga.Snags.yml";

        private static SnagModel current = new SnagModel();

        public static void Register(NativeConfiguration config)
        {
            config.AddYaml(new YamlFileSet(Pattern, SyncKey, () => new SnagModel(), Apply)
            {
                DefaultContent = NativeConfiguration.EmbeddedResource(typeof(SnagFile).Assembly, Resource),
            });
        }

        /// <summary>One snag for a cast in this biome, picked by weight; null when the biome has none.</summary>
        public static FindEntry Pick(Heightmap.Biome biome) =>
            current.ByBiome.TryGetValue(biome, out List<FindEntry> table) ? FindFile.PickFrom(table) : null;

        private static void Apply(YamlModel model)
        {
            current = (SnagModel)model;
            FindPrefabs.ResetWarnings();
            NativeSaga.Log.LogInfo($"Snags applied: {current.ByBiome.Count} biomes, {current.Count} snags.");
        }
    }
}
