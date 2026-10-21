// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mantle.Slots;

namespace Mantle.Settings
{
    /// <summary>
    /// Applies <see cref="KeepOnDeathMigration"/> to this side's .cfg once, right after Keep On Death is bound. Nothing
    /// binds Keep Slots On Death any more, so BepInEx keeps its line as an orphaned entry; the marker is added as another
    /// orphaned line in the same section, so no configuration manager shows it and the next load skips the carry-over.
    /// </summary>
    internal static class KeepOnDeathUpgrade
    {
        private static readonly PropertyInfo? Orphans =
            typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Run(ConfigFile config, ConfigEntry<KeptGroups> keepOnDeath, ManualLogSource log)
        {
            if (Orphans?.GetValue(config) is not Dictionary<ConfigDefinition, string> orphans)
                return;
            string section = keepOnDeath.Definition.Section;
            ConfigDefinition old = new ConfigDefinition(section, KeepOnDeathMigration.OldKey);
            ConfigDefinition marker = new ConfigDefinition(section, KeepOnDeathMigration.MarkerKey);
            orphans.TryGetValue(old, out string? oldValue);
            bool migrated = orphans.ContainsKey(marker);
            if (!KeepOnDeathMigration.ShouldMark(oldValue, migrated))
                return;
            KeptGroups? groups = KeepOnDeathMigration.Plan(oldValue, migrated, keepOnDeath.Value);
            orphans[marker] = "true";
            bool save = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                if (groups.HasValue)
                    keepOnDeath.Value = groups.Value;
            }
            finally
            {
                config.SaveOnConfigSet = save;
            }
            config.Save();
            if (groups.HasValue)
                log.LogInfo($"Keep Slots On Death carried over to Keep On Death: {groups.Value}.");
        }
    }
}
