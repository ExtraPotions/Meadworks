// Keel - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Edge.Plugin and Ember.Plugin (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Keel.Config;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// Keel's configuration surface (K02a, design section 4). Every setting of <see cref="KeelSettings.All"/> is bound
    /// through Crucible C01 with its scope: cheats through <c>MeadworksConfig.BindCheat</c> with their Mischief category,
    /// the rest through <c>MeadworksConfig.Bind</c>; the two key settings bind as <see cref="KeyboardShortcut"/>. Values
    /// are read at use time, so edits, reloads and server pushes apply at once. Main thread only.
    /// </summary>
    public sealed class KeelConfig
    {
        private const string Product = Plugin.Guid;

        private static readonly HashSet<KeelSetting> Shortcuts = new HashSet<KeelSetting>(KeelSettings.KeyboardShortcuts);

        private readonly ConfigFile file;
        private readonly Dictionary<KeelSetting, ConfigEntryBase> entries = new Dictionary<KeelSetting, ConfigEntryBase>();

        private KeelConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in KeelSettings.All) BindSetting(setting);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
        }

        public ConfigFile File => file;
        public PlayerSettingsLocks? PlayerLocks { get; private set; }
        public void Tick() => PlayerLocks?.Tick();

        /// <summary>Binds every Keel setting on the plugin's config file and tracks blocked options (C03).</summary>
        public static KeelConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new KeelConfig(file);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local && !p.Key.Hidden))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            config.PlayerLocks = new PlayerSettingsLocks(file, log);
            log.Verbose("Configuration bound: " + Count(SettingScope.Local) + " Local, " + Count(SettingScope.ServerAuthoritative)
                + " ServerAuthoritative, " + Count(SettingScope.Cheat) + " Cheat.");
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

        /// <summary>The Hex Mischief category of each cheat (Crucible MischiefGate).</summary>
        public static MischiefCategory CategoryOf(KeelSetting cheat)
        {
            if (cheat == KeelSettings.Cheats.AshlandsImmuneShips) return MischiefCategory.Progress;
            if (cheat == KeelSettings.Cheats.InvulnerableShips) return MischiefCategory.World;
            if (cheat == KeelSettings.Cheats.Tailwind || cheat == KeelSettings.Cheats.FastSailing) return MischiefCategory.Personal;
            throw new ArgumentException(cheat?.Id + " is not a Keel cheat.", nameof(cheat));
        }

        public ConfigEntryBase Entry(KeelSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Keel setting.", nameof(setting));

        /// <summary>The value Keel acts on now. Key settings use <see cref="Shortcut"/>.</summary>
        public T Get<T>(KeelSetting<T> setting)
        {
            if (Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is a keyboard shortcut; use Shortcut.", nameof(setting));
            var entry = (ConfigEntry<T>)Entry(setting);
            switch (setting.Scope)
            {
                case SettingScope.Cheat:
                    return KeelCheats.Effective(setting, CheatLocked(), entry.Value);
                case SettingScope.ServerAuthoritative:
                    return setting.Normalize(KeelAuthority.Effective(entry, ConfigScope.ServerAuthoritative, setting.Default));
                default:
                    return setting.Normalize(PlayerLocks != null ? PlayerLocks.Effective(entry, entry.Value) : entry.Value);
            }
        }

        /// <summary>A keyboard shortcut setting after any current server player-setting lock.</summary>
        public KeyboardShortcut Shortcut(KeelSetting<string> setting)
        {
            if (!Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is not a keyboard shortcut.", nameof(setting));
            var entry = (ConfigEntry<KeyboardShortcut>)Entry(setting);
            return PlayerLocks?.Effective(entry, entry.Value) ?? entry.Value;
        }

        /// <summary>The player's own entry of a keyboard shortcut setting, whatever a server lock says (see <see cref="Shortcut"/>).</summary>
        public ConfigEntry<KeyboardShortcut> ShortcutEntry(KeelSetting<string> setting)
        {
            if (!Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is not a keyboard shortcut.", nameof(setting));
            return (ConfigEntry<KeyboardShortcut>)Entry(setting);
        }

        /// <summary>True while a server lock replaces the player's own value of a keyboard shortcut (then <see cref="Shortcut"/> differs from the entry).</summary>
        public bool ShortcutLocked(KeelSetting<string> setting) => PlayerLocks?.Holds(ShortcutEntry(setting)) == true;

        /// <summary>Ships the Ashlands immunity cheat covers now (none unless an authority allows it and it is on).</summary>
        public ShipList AshlandsImmune => KeelCheats.ShipsInEffect(KeelSettings.Cheats.AshlandsImmuneShips, CheatLocked(),
            Raw(KeelSettings.Cheats.AshlandsImmuneShips), KeelSettings.Cheats.AshlandsImmuneList, Get(KeelSettings.Cheats.AshlandsImmuneList));

        /// <summary>Ships the invulnerability cheat covers now (none unless an authority allows it and it is on).</summary>
        public ShipList Invulnerable => KeelCheats.ShipsInEffect(KeelSettings.Cheats.InvulnerableShips, CheatLocked(),
            Raw(KeelSettings.Cheats.InvulnerableShips), KeelSettings.Cheats.InvulnerableList, Get(KeelSettings.Cheats.InvulnerableList));

        /// <summary>Whether Keel may act on a cheat now; tells the player why not (once per option per session).</summary>
        public bool TryUseCheat(KeelSetting cheat, string? label = null)
        {
            if (cheat == null || cheat.Scope != SettingScope.Cheat) throw new ArgumentException("Expected a Keel cheat.", nameof(cheat));
            return MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat, label);
        }

        public void Shutdown()
        {
            PlayerLocks?.Dispose(); PlayerLocks = null;
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
        }

        private bool Raw(KeelSetting<bool> cheat) => ((ConfigEntry<bool>)Entry(cheat)).Value;

        private static bool CheatLocked() => KeelAuthority.Decide(ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        private void BindSetting(KeelSetting setting)
        {
            ConfigEntryBase entry;
            if (Shortcuts.Contains(setting))
            {
                var text = (string)setting.DefaultValue;
                entry = MeadworksConfig.Bind(file, setting.Section, setting.Key, text.Length == 0 ? KeyboardShortcut.Empty : KeyboardShortcut.Deserialize(text),
                    setting.Description, Map(setting.Scope));
            }
            else
            {
                switch (setting)
                {
                    case KeelSetting<bool> b: entry = Bind(b, null); break;
                    case KeelSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                    case KeelSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                    case KeelSetting<string> s: entry = Bind(s, null); break;
                    case KeelSetting<SpeedUnit> u: entry = Bind(u, null); break;
                    default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
                }
            }
            entries.Add(setting, entry);
        }

        private ConfigDescription Describe(KeelSetting setting, AcceptableValueBase? acceptable)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, KeelSettings.Sailing.ShipHealthAt100) ||
                ReferenceEquals(setting, KeelSettings.Sailing.ShipSpeedAt100) ||
                ReferenceEquals(setting, KeelSettings.Sailing.ExplorationRadiusAt100) ||
                ReferenceEquals(setting, KeelSettings.Sailing.HelmExperiencePerKilometre) ||
                ReferenceEquals(setting, KeelSettings.Sailing.CrewExperienceShare))
                dependency = new ConfigDependency(() => Get(KeelSettings.Sailing.Enabled), () => "Requires " + KeelSettings.Sailing.Enabled.Section + ": " + KeelSettings.Sailing.Enabled.Key + ".");
            return new ConfigDescription(setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(KeelSetting<T> setting, AcceptableValueBase? acceptable)
        {
            if (setting.Scope == SettingScope.Cheat)
                return MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, CategoryOf(setting), acceptable);
            if (setting.Hidden)
                return MeadworksConfig.BindHidden(file, setting.Section, setting.Key, setting.Default, setting.Description, Map(setting.Scope), acceptable);
            return MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable), Map(setting.Scope));
        }

        private static int Count(SettingScope scope) => KeelSettings.All.Count(s => s.Scope == scope && !s.Hidden);
    }
}
