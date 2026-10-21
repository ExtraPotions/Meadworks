// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Finds/MineFindFile.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using YamlConfig;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Saga.MineFinds*.yml: registered with the synced configuration on every machine (written from the
    /// embedded default when missing, sent from the server to every player while the server binds the configuration,
    /// reloaded a few seconds after an edit, editable in game) and holding the tables of the last file set that parsed
    /// without errors. The hub applies a model as soon as it parses; the tables hold only names, so that is safe before
    /// the game's item database exists, on a dedicated server as on a client. The same pattern as the Woodcutting
    /// <see cref="FindFile"/>, with deposit kinds in place of trees.
    /// </summary>
    public static class MineFindFile
    {
        public const string Pattern = "Saga.MineFinds*.yml";
        public const string SyncKey = Keys.MineFindsSync;
        public const string Resource = "Saga.config.Saga.MineFinds.yml";

        private static MineFindModel current = new MineFindModel();

        public static void Register(NativeConfiguration config)
        {
            config.AddYaml(new YamlFileSet(Pattern, SyncKey, () => new MineFindModel(), Apply)
            {
                DefaultContent = NativeConfiguration.EmbeddedResource(typeof(MineFindFile).Assembly, Resource),
            });
        }

        /// <summary>
        /// One find for a broken chunk, picked by weight: from its rock kind's table when the file lists the kind,
        /// otherwise from its biome's. Null when the table is missing or empty, or every weight is 0.
        /// </summary>
        public static FindEntry Pick(string kind, Heightmap.Biome biome)
        {
            MineFindModel model = current;
            if (!string.IsNullOrEmpty(kind) && model.ByDeposit.TryGetValue(kind, out List<FindEntry> deposit))
                return FindFile.PickFrom(deposit);
            return model.ByBiome.TryGetValue(biome, out List<FindEntry> table) ? FindFile.PickFrom(table) : null;
        }

        private static void Apply(YamlModel model)
        {
            current = (MineFindModel)model;
            FindPrefabs.ResetWarnings();
            NativeSaga.Log.LogInfo($"Mine finds applied: {current.ByBiome.Count} biomes and {current.ByDeposit.Count} deposits, {current.Count} finds.");
        }
    }
}
