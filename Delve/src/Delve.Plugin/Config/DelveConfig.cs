// Delve - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Keel.Plugin and Edge.Plugin (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Delve.Runtime
{
    /// <summary>
    /// Delve's configuration surface (DV02a, design section 4). Every setting of <see cref="DelveSettings.All"/> is bound
    /// through Crucible C01 with its scope (ServerAuthoritative, so Almanac and Thing show <c>ServerOnly</c> on a remote
    /// client); the import marker is hidden; the Regenerate cheat binds through <c>MeadworksConfig.BindCheat</c> with Mischief
    /// category World (user decision 2026-10-08). Values are read at use time, so edits and reloads apply within one check.
    /// Main thread only.
    /// </summary>
    public sealed class DelveConfig
    {
        private const string Product = Plugin.Guid;

        private readonly ConfigFile file;
        private readonly Dictionary<DelveSetting, ConfigEntryBase> entries = new Dictionary<DelveSetting, ConfigEntryBase>();

        private DelveConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in DelveSettings.All) BindSetting(setting);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
        }

        public ConfigFile File => file;

        /// <summary>Binds every Delve setting on the plugin's config file and tracks blocked options (C03).</summary>
        public static DelveConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new DelveConfig(file);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local && !p.Key.Hidden))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            log.Verbose("Configuration bound: " + DelveSettings.Visible.Count + " settings (ServerAuthoritative plus one cheat).");
            return config;
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

        public ConfigEntryBase Entry(DelveSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Delve setting.", nameof(setting));

        /// <summary>The value Delve acts on now.</summary>
        public T Get<T>(DelveSetting<T> setting)
        {
            var entry = (ConfigEntry<T>)Entry(setting);
            if (setting.Scope == SettingScope.Cheat)
                return DelveCheats.Effective(setting, CheatLocked(), entry.Value);
            if (setting.Scope == SettingScope.ServerAuthoritative)
                return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
            return setting.Normalize(entry.Value);
        }

        /// <summary>A kind's delay in its clock's own range (design 4: days 0.25 to 3650, hours 0.25 to 87600).</summary>
        public double Delay(DelveKind kind)
        {
            var k = DelveSettings.Kind(kind);
            return ClockRules.Clamp(Get(k.Clock), Get(k.Delay));
        }

        /// <summary>
        /// Whether an admin's regenerate-now request may skip the delay now; tells the caller why not once per session
        /// (C03) when the Mischief gate locks the cheat.
        /// </summary>
        public RegenerateVerdict Regenerate(bool isAdmin)
        {
            var cheat = DelveSettings.Cheats.RegenerateWithoutWaiting;
            var verdict = DelveCheats.Regenerate(isAdmin, CheatLocked(), ((ConfigEntry<bool>)Entry(cheat)).Value);
            if (verdict == RegenerateVerdict.CheatLocked) MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat);
            return verdict;
        }

        /// <summary>
        /// The verdict for a caller whose Mischief decision the authority made for them (a remote admin's kick command:
        /// <c>MeadworksMischief.DecideForPeer</c>); the cheat's value is the authority's own (server-bound).
        /// </summary>
        public RegenerateVerdict RegenerateFor(bool isAdmin, bool lockedForCaller)
        {
            var cheat = DelveSettings.Cheats.RegenerateWithoutWaiting;
            return DelveCheats.Regenerate(isAdmin, lockedForCaller || CheatLocked(), ((ConfigEntry<bool>)Entry(cheat)).Value);
        }

        private static bool CheatLocked() => MeadworksAuthority.Decide(Product, ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        public void Shutdown()
        {
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
        }

        private void BindSetting(DelveSetting setting)
        {
            ConfigEntryBase entry;
            switch (setting)
            {
                case DelveSetting<bool> b: entry = Bind(b, null); break;
                case DelveSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                case DelveSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                case DelveSetting<string> s: entry = Bind(s, null); break;
                case DelveSetting<ClockKind> c: entry = Bind(c, null); break;
                default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
            }
            entries.Add(setting, entry);
        }

        private ConfigDescription Describe(DelveSetting setting, AcceptableValueBase? acceptable)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, DelveSettings.Rooms.MinRooms) ||
                ReferenceEquals(setting, DelveSettings.Rooms.MaxRooms))
                dependency = new ConfigDependency(() => Get(DelveSettings.Rooms.Enabled), () => "Requires " + DelveSettings.Rooms.Enabled.Section + ": " + DelveSettings.Rooms.Enabled.Key + ".");
            if (ReferenceEquals(setting, DelveSettings.Rooms.ForestCryptMin) ||
                ReferenceEquals(setting, DelveSettings.Rooms.ForestCryptMax))
                dependency = new ConfigDependency(() => Get(DelveSettings.Rooms.Enabled) && Get(DelveSettings.Rooms.ForestCryptOverride), () => "Requires Rooms: Enabled and " + DelveSettings.Rooms.ForestCryptOverride.Key + ".");
            if (ReferenceEquals(setting, DelveSettings.Rooms.SunkenCryptMin) ||
                ReferenceEquals(setting, DelveSettings.Rooms.SunkenCryptMax))
                dependency = new ConfigDependency(() => Get(DelveSettings.Rooms.Enabled) && Get(DelveSettings.Rooms.SunkenCryptOverride), () => "Requires Rooms: Enabled and " + DelveSettings.Rooms.SunkenCryptOverride.Key + ".");
            if (ReferenceEquals(setting, DelveSettings.Rooms.CaveMin) ||
                ReferenceEquals(setting, DelveSettings.Rooms.CaveMax))
                dependency = new ConfigDependency(() => Get(DelveSettings.Rooms.Enabled) && Get(DelveSettings.Rooms.CaveOverride), () => "Requires Rooms: Enabled and " + DelveSettings.Rooms.CaveOverride.Key + ".");
            return new ConfigDescription(setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(DelveSetting<T> setting, AcceptableValueBase? acceptable)
        {
            if (setting.Scope == SettingScope.Cheat)
                return MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, MischiefCategory.World, acceptable);
            if (setting.Hidden)
                return MeadworksConfig.BindHidden(file, setting.Section, setting.Key, setting.Default, setting.Description, Map(setting.Scope), acceptable);
            return MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable), Map(setting.Scope));
        }
    }
}
