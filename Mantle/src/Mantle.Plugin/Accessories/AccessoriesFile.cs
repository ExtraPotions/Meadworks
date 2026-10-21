// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.SyncedConfig;
using ExtraPotions.Mantle.Embedded.YamlConfig;
using Mantle.Core;
using Mantle.Crafting;

namespace Mantle.Accessories
{
    /// <summary>
    /// Mantle.Accessories.yml: the ring and belt registry's overrides (<see cref="AccessoriesModel"/>). Synced from the server
    /// (Charter article <c>mantle_accessories</c>), hot reloaded and editable in game like the other YAML files, so the server's
    /// stats and tags win on every client. Applying it rebuilds the registry from the built-in entries, makes the recipes and
    /// the item texts follow, and lays the inventory out again on the next frame, so a newly tagged or untagged item finds its
    /// slot. Items Mantle makes are built when the game starts and cannot be added here; tagging an existing prefab can.
    /// </summary>
    public static class AccessoriesFile
    {
        public const string DefaultResource = "Mantle.config.Mantle.Accessories.yml";

        public static YamlFileSet Set { get; private set; }

        /// <summary>The registry from the built-in entries alone, in use until the file's values arrive.</summary>
        public static void UseBuiltIn() => AccessoryRegistry.Load(AccessoryRegistry.Build(AccessoryCatalog.All, null));

        public static void Initialize(SyncedConfiguration synced)
        {
            UseBuiltIn();
            Set = synced.AddYaml(new YamlFileSet("Mantle.Accessories*.yml", "mantle_accessories", () => new AccessoriesModel(), Apply)
            {
                DefaultContent = SyncedConfiguration.EmbeddedResource(typeof(AccessoriesFile).Assembly, DefaultResource),
                EditorLabel = () => "Edit rings and belts",
            });
        }

        private static void Apply(YamlModel model)
        {
            AccessoriesModel read = (AccessoriesModel)model;
            List<string> warnings = new List<string>();
            AccessoryRegistry.Load(AccessoryRegistry.Build(AccessoryCatalog.All, read.Overrides, warnings));
            foreach (string warning in warnings)
                Plugin.Log.LogWarning("Mantle.Accessories.yml: " + warning);
            CraftRecipes.RefreshAll();
            AccessoryWords.DescribeAll();
            InventoryModule.RequestLayout();
            Plugin.Log.LogInfo($"Mantle: rings and belts applied, {AccessoryRegistry.Current.All.Count} items in the registry, {read.Overrides.Count} set in the file.");
        }

        /// <summary>The registry goes back to the built-in entries with the module (shutdown).</summary>
        public static void Reset() => AccessoryRegistry.Load(null);
    }
}
