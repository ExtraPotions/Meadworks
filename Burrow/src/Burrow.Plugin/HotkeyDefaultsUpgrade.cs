// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: applies ExtraPotions.Burrow.HotkeyDefaults to the player's cfg once (settings simplification).
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Burrow.Reach;
using Burrow.Stow;
using ExtraPotions.Burrow;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using UnityEngine;

namespace Burrow
{
    /// <summary>
    /// Run after every module has bound its settings: each hotkey that is now unbound by default and still holds its old
    /// default moves to the new default, once per cfg, recorded in a hidden marker entry. Customized keys are kept.
    /// </summary>
    internal static class HotkeyDefaultsUpgrade
    {
        public static void Run(SyncedConfiguration synced)
        {
            ConfigEntry<int> marker = synced.Bind("General", "Hotkey Defaults Version", 0,
                "Records that Burrow's hotkey defaults update ran for this player. Managed by Burrow.", synced: false,
                tags: new ConfigurationManagerAttributes { Browsable = false, ReadOnly = true });
            if (marker.Value >= HotkeyDefaults.Current)
                return;
            var old = new Dictionary<ConfigEntry<KeyboardShortcut>, KeyboardShortcut>
            {
                [ReachSettings.PullModifier] = new KeyboardShortcut(KeyCode.LeftAlt),
                [ReachSettings.ToggleKey] = new KeyboardShortcut(KeyCode.R, KeyCode.LeftAlt),
                [ReachSettings.LinkKey] = new KeyboardShortcut(KeyCode.L, KeyCode.LeftAlt),
                [StowSettings.TakeAllKey] = new KeyboardShortcut(KeyCode.G, KeyCode.LeftShift),
                [StowSettings.TopUpKey] = new KeyboardShortcut(KeyCode.R),
                [StowSettings.SortContainerKey] = new KeyboardShortcut(KeyCode.Y),
                [StowSettings.FavouriteItemKey] = new KeyboardShortcut(KeyCode.F),
                [StowSettings.FavouriteSlotKey] = new KeyboardShortcut(KeyCode.F, KeyCode.LeftShift),
                [StowSettings.JunkKey] = new KeyboardShortcut(KeyCode.J),
                [StowSettings.TrashKey] = new KeyboardShortcut(KeyCode.Delete),
                [StowSettings.DestroyJunkKey] = new KeyboardShortcut(KeyCode.Delete, KeyCode.LeftShift),
                [StowSettings.FeedSourceKey] = new KeyboardShortcut(KeyCode.J, KeyCode.LeftAlt),
                [StowSettings.RouteModifier] = new KeyboardShortcut(KeyCode.LeftControl),
                [StowSettings.StoreOneKey] = new KeyboardShortcut(KeyCode.V),
                [StowSettings.FindKey] = new KeyboardShortcut(KeyCode.Z),
                [StowSettings.DumpKey] = new KeyboardShortcut(KeyCode.D, KeyCode.LeftAlt),
                [StowSettings.CyclePreviousKey] = new KeyboardShortcut(KeyCode.LeftArrow),
                [StowSettings.CycleNextKey] = new KeyboardShortcut(KeyCode.RightArrow),
            };
            var byId = old.Keys.ToDictionary(Id);
            List<string> moved = HotkeyDefaults.Plan(marker.Value,
                old.Select(pair => new HotkeyDefaults.Saved(Id(pair.Key), Text(pair.Key.Value), Text(pair.Value))));
            ConfigFile config = synced.Config;
            bool saveOnSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                foreach (string id in moved)
                    byId[id].Value = KeyboardShortcut.Empty;
                marker.Value = HotkeyDefaults.Current;
            }
            finally
            {
                config.SaveOnConfigSet = saveOnSet;
            }
            config.Save();
            if (moved.Count > 0)
                Plugin.Log.LogInfo("Hotkey defaults updated: " + string.Join(", ", moved)
                    + " now start unbound (they held the old default). Customized keys were kept; rebind any of them in Almanac.");
        }

        private static string Id(ConfigEntry<KeyboardShortcut> entry) => entry.Definition.Section + "/" + entry.Definition.Key;

        private static string Text(KeyboardShortcut shortcut) => TomlTypeConverter.ConvertToString(shortcut, typeof(KeyboardShortcut));
    }

    /// <summary>The configuration-manager tag (read by name): hidden, product-managed entries (Crucible C01 hidden convention).</summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public bool? Browsable;
        public bool? ReadOnly;
    }
}
