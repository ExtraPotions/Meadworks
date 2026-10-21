// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Foraging/Forage/ForageFile.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using YamlConfig;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Saga.Forage*.yml: registered with the synced configuration (written from the embedded default when
    /// missing, sent from the server to every player while the server binds the configuration, reloaded a few seconds
    /// after an edit, editable in game) and holding the entries of the last file set that parsed without errors. The
    /// picker's client reads them to decide what is forage and the experience of a pick.
    /// </summary>
    public static class ForageFile
    {
        public const string Pattern = "Saga.Forage*.yml";
        public const string SyncKey = Keys.ForageSync;
        public const string Resource = "Saga.config.Saga.Forage.yml";

        private static ForageModel current = new ForageModel();

        public static void Register(NativeConfiguration config)
        {
            config.AddYaml(new YamlFileSet(Pattern, SyncKey, () => new ForageModel(), Apply)
            {
                DefaultContent = NativeConfiguration.EmbeddedResource(typeof(ForageFile).Assembly, Resource),
            });
        }

        /// <summary>The entry of an item prefab, or null when the file does not list it.</summary>
        public static ForageEntry Find(string itemPrefab) =>
            !string.IsNullOrEmpty(itemPrefab) && current.Items.TryGetValue(itemPrefab, out ForageEntry entry) ? entry : null;

        private static void Apply(YamlModel model)
        {
            current = (ForageModel)model;
            NativeSaga.Log.LogInfo($"Forage applied: {current.Items.Count} items.");
        }
    }
}
