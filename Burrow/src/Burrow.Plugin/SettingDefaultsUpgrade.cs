// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: applies ExtraPotions.Burrow.SettingDefaults to the player's cfg once (changed defaults after release).
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Burrow;
using ExtraPotions.Burrow.Embedded.SyncedConfig;

namespace Burrow
{
    /// <summary>
    /// Run after every module has bound its settings: each setting whose default changed and that still holds its old
    /// default moves to the new default, once per cfg, recorded in a hidden marker entry. Values the player chose are kept.
    /// </summary>
    internal static class SettingDefaultsUpgrade
    {
        public static void Run(SyncedConfiguration synced)
        {
            ConfigEntry<int> marker = synced.Bind("General", "Setting Defaults Version", 0,
                "Records that Burrow's changed setting defaults were applied for this player. Managed by Burrow.", synced: false,
                tags: new ConfigurationManagerAttributes { Browsable = false, ReadOnly = true });
            if (marker.Value >= SettingDefaults.Current)
                return;
            ConfigFile config = synced.Config;
            var entries = new Dictionary<string, (ConfigEntryBase Entry, SettingDefaults.Change Change)>();
            foreach (SettingDefaults.Change change in SettingDefaults.Changes)
            {
                int slash = change.Id.IndexOf('/');
                var definition = new ConfigDefinition(change.Id.Substring(0, slash), change.Id.Substring(slash + 1));
                if (config.ContainsKey(definition))
                    entries[change.Id] = (config[definition], change);
            }
            List<string> moved = SettingDefaults.Plan(marker.Value, entries.Values.Select(pair =>
                new HotkeyDefaults.Saved(pair.Change.Id, pair.Entry.GetSerializedValue(), pair.Change.OldDefault)));
            bool saveOnSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                foreach (string id in moved)
                    entries[id].Entry.SetSerializedValue(entries[id].Change.NewDefault);
                marker.Value = SettingDefaults.Current;
            }
            finally
            {
                config.SaveOnConfigSet = saveOnSet;
            }
            config.Save();
            if (moved.Count > 0)
                Plugin.Log.LogInfo("Setting defaults updated: " + string.Join(", ", moved.Select(id => id + " = " + entries[id].Change.NewDefault))
                    + " (they held the old default). Values you changed were kept.");
        }
    }
}
