// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Edge.Plugin (expDARE), plus hotkeys bound as BepInEx KeyboardShortcut.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Config;

namespace ExtraPotions.Threshold.Runtime
{
    /// <summary>
    /// Threshold's configuration surface (T02a, design section 4). Every setting of <see cref="ThresholdSettings.All"/> is
    /// bound through Crucible C01 with its scope: the cheat through <c>MeadworksConfig.BindCheat</c>, the import marker
    /// through <c>BindHidden</c>, the rest through <c>Bind</c>. Values are read at use time. Main thread only.
    /// </summary>
    public sealed class ThresholdConfig
    {
        private const string Product = Plugin.Guid;

        private readonly ConfigFile file;
        private readonly Dictionary<ThresholdSetting, ConfigEntryBase> entries = new Dictionary<ThresholdSetting, ConfigEntryBase>();
        private ThresholdPlayerLocks? locks;

        private ThresholdConfig(ConfigFile file, MeadworksLog log)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in ThresholdSettings.All) BindSetting(setting);
                MeadworksConfig.EnsureSchema(file, ThresholdDefaultsMigration.Schema,
                    new[] { new MigrationStep(0, 1, () => MigrateDefaults(log)) });
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
        }

        /// <summary>Binds every Threshold setting on the plugin's config file and tracks blocked options (C03).</summary>
        public static ThresholdConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new ThresholdConfig(file, log);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local && !p.Key.Hidden))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            var locals = config.entries.Where(p => ThresholdSettings.LockableLocalIds.Contains(p.Key.Id))
                .ToDictionary(p => p.Key.Id, p => p.Value, StringComparer.Ordinal);
            config.locks = new ThresholdPlayerLocks(file, (ConfigEntry<string>)config.Entry(ThresholdSettings.General.LockedPlayerSettings), locals, log);
            log.Verbose("Configuration bound: " + Count(SettingScope.Local) + " Local, " + Count(SettingScope.ServerAuthoritative)
                + " ServerAuthoritative, " + Count(SettingScope.Cheat) + " Cheat, " + ThresholdSettings.All.Count(s => s.Hidden) + " hidden.");
            return config;
        }

        /// <summary>Moves stored values that still equal an old input default to the new default (once, by schema).</summary>
        private void MigrateDefaults(MeadworksLog log)
        {
            var moved = new List<string>();
            foreach (var change in ThresholdDefaultsMigration.Changes)
            {
                var entry = (ConfigEntry<KeyboardShortcut>)entries[change.Key];
                var next = ThresholdDefaultsMigration.Migrate(change.Key, entry.Value.Serialize());
                if (next == null) continue;
                entry.Value = KeyboardShortcut.Deserialize(next);
                moved.Add(change.Key.Id);
            }
            if (moved.Count > 0)
                log.Info("Moved unchanged old input defaults to the new defaults: " + string.Join(", ", moved) + ". Customized values were kept.");
        }

        public static ConfigScope Map(SettingScope scope)
        {
            switch (scope)
            {
                case SettingScope.Local: return ConfigScope.Local;
                case SettingScope.ServerAuthoritative: return ConfigScope.ServerAuthoritative;
                case SettingScope.Cheat: return ConfigScope.Cheat;
                default: throw new ArgumentException("Unknown scope.", nameof(scope));
            }
        }

        public ConfigEntryBase Entry(ThresholdSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Threshold setting.", nameof(setting));

        /// <summary>The value Threshold acts on now. Hotkeys are read through <see cref="Shortcut"/>.</summary>
        public T Get<T>(ThresholdSetting<T> setting)
        {
            if (setting.IsShortcut) throw new ArgumentException(setting.Id + " is a hotkey; use Shortcut.", nameof(setting));
            var entry = (ConfigEntry<T>)Entry(setting);
            switch (setting.Scope)
            {
                case SettingScope.Cheat:
                    return ThresholdCheats.Effective(setting, CheatLocked(), entry.Value);
                case SettingScope.ServerAuthoritative:
                    return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
                default:
                    return setting.Normalize(locks != null ? locks.Effective(entry, entry.Value) : entry.Value);
            }
        }

        public KeyboardShortcut Shortcut(ThresholdSetting<string> hotkey)
        {
            if (hotkey == null || !hotkey.IsShortcut) throw new ArgumentException("Expected a Threshold hotkey.", nameof(hotkey));
            var entry = (ConfigEntry<KeyboardShortcut>)Entry(hotkey);
            return locks != null ? locks.Effective(entry, entry.Value) : entry.Value;
        }

        /// <summary>Republishes and re-reads Locked Player Settings after setting changes or a server sync.</summary>
        public void Tick() => locks?.Tick();

        /// <summary>Ignore Teleport Restrictions in effect (off unless an authority allows it).</summary>
        public bool IgnoreTeleportRestrictions => Get(ThresholdSettings.Cheats.IgnoreTeleportRestrictions);

        /// <summary>Whether Threshold may act on a cheat now; tells the player why not (once per option per session).</summary>
        public bool TryUseCheat(ThresholdSetting cheat, string? label = null)
        {
            if (cheat == null || cheat.Scope != SettingScope.Cheat) throw new ArgumentException("Expected a Threshold cheat.", nameof(cheat));
            return MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat, label);
        }

        public void Shutdown()
        {
            locks?.Dispose();
            locks = null;
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
        }

        private static bool CheatLocked() => MeadworksAuthority.Decide(Product, ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        private void BindSetting(ThresholdSetting setting)
        {
            ConfigEntryBase entry;
            switch (setting)
            {
                case ThresholdSetting<string> s when s.IsShortcut:
                    entry = MeadworksConfig.Bind(file, s.Section, s.Key, KeyboardShortcut.Deserialize(s.Default), s.Description, ConfigScope.Local);
                    break;
                case ThresholdSetting<bool> b: entry = Bind(b, null); break;
                case ThresholdSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                case ThresholdSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                case ThresholdSetting<string> s: entry = Bind(s, null); break;
                case ThresholdSetting<ReturnToHubMode> r: entry = Bind(r, null); break;
                case ThresholdSetting<HubEditing> h: entry = Bind(h, null); break;
                case ThresholdSetting<SortOrder> o: entry = Bind(o, null); break;
                case ThresholdSetting<TeleportMode> t: entry = Bind(t, null); break;
                case ThresholdSetting<OreTransportMode> ore: entry = Bind(ore, null); break;
                case ThresholdSetting<AccessMode> m: entry = Bind(m, null); break;
                default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
            }
            entries.Add(setting, entry);
        }

        private ConfigDescription Describe(ThresholdSetting setting, AcceptableValueBase? acceptable, string? text = null)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, ThresholdSettings.Fuel.FuelItem) ||
                ReferenceEquals(setting, ThresholdSettings.Fuel.FuelAmount))
                dependency = new ConfigDependency(() => Get(ThresholdSettings.Fuel.FuelEnabled), () => "Requires " + ThresholdSettings.Fuel.FuelEnabled.Section + ": " + ThresholdSettings.Fuel.FuelEnabled.Key + ".");
            if (ReferenceEquals(setting, ThresholdSettings.Tames.TameRadius) ||
                ReferenceEquals(setting, ThresholdSettings.Tames.MaxTames))
                dependency = new ConfigDependency(() => Get(ThresholdSettings.Tames.BringTames), () => "Requires " + ThresholdSettings.Tames.BringTames.Section + ": " + ThresholdSettings.Tames.BringTames.Key + ".");
            if (ReferenceEquals(setting, ThresholdSettings.Access.DefaultAccessMode))
                dependency = new ConfigDependency(() => Get(ThresholdSettings.Access.AccessModesEnabled), () => "Requires " + ThresholdSettings.Access.AccessModesEnabled.Section + ": " + ThresholdSettings.Access.AccessModesEnabled.Key + ".");
            if (ReferenceEquals(setting, ThresholdSettings.Jumps.QuickPortalRange) ||
                ReferenceEquals(setting, ThresholdSettings.Jumps.QuickPortalSeconds))
                dependency = new ConfigDependency(() => Get(ThresholdSettings.Jumps.QuickPortals), () => "Requires " + ThresholdSettings.Jumps.QuickPortals.Section + ": " + ThresholdSettings.Jumps.QuickPortals.Key + ".");
            return new ConfigDescription(text ?? setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(ThresholdSetting<T> setting, AcceptableValueBase? acceptable)
        {
            if (setting.Scope == SettingScope.Cheat)
                return MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, acceptable);
            if (setting.Hidden)
                return MeadworksConfig.BindHidden(file, setting.Section, setting.Key, setting.Default, setting.Description, Map(setting.Scope), acceptable);
            return MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable), Map(setting.Scope));
        }

        private static int Count(SettingScope scope) => ThresholdSettings.All.Count(s => s.Scope == scope && !s.Hidden);
    }
}
