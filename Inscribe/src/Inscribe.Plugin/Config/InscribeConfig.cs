// Inscribe - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Wildblood.Plugin, Ember.Plugin and Kindle.Plugin (expDARE), without the World scope.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Inscribe.Config;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// Inscribe's configuration surface (R02a, design section 4). Every setting of <see cref="InscribeSettings.All"/> is
    /// bound through <see cref="MeadworksConfig.Bind{T}(ConfigFile,string,string,T,string,ConfigScope,AcceptableValueBase)"/>
    /// with its scope (cheats through <see cref="MeadworksConfig.BindCheat{T}(ConfigFile,string,string,T,string,AcceptableValueBase)"/>).
    /// Values are read at use time. Main thread only.
    /// </summary>
    public sealed class InscribeConfig
    {
        private const string Product = Plugin.Guid;

        private static readonly HashSet<InscribeSetting> Shortcuts = BuildShortcuts();

        private static HashSet<InscribeSetting> BuildShortcuts()
        {
            // Initialize the catalog before its nested holders (Mono initializes the containing type eagerly).
            _ = InscribeSettings.All;
            // The only shortcut, the Enhancement Window Key, was removed with enhancement (0.2.0).
            return new HashSet<InscribeSetting>();
        }

        private readonly ConfigFile file;
        private readonly Dictionary<InscribeSetting, ConfigEntryBase> entries = new Dictionary<InscribeSetting, ConfigEntryBase>();

        private InscribeConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in InscribeSettings.All) BindSetting(setting);
                var legacy = InscribeSettings.Interface.StationButton;
                entries.Add(legacy, MeadworksConfig.BindHidden(file, legacy.Section, legacy.Key, legacy.Default, legacy.Description));
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
        }

        /// <summary>Binds every Inscribe setting on the plugin's config file and tracks blocked options (C03).</summary>
        public static InscribeConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new InscribeConfig(file);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
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

        public ConfigEntryBase Entry(InscribeSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not an Inscribe setting.", nameof(setting));

        /// <summary>The value Inscribe acts on now. Shortcut settings use <see cref="Shortcut"/>.</summary>
        public T Get<T>(InscribeSetting<T> setting)
        {
            if (Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is a keyboard shortcut; use Shortcut.", nameof(setting));
            var entry = (ConfigEntry<T>)Entry(setting);
            switch (setting.Scope)
            {
                case SettingScope.Cheat:
                    return InscribeCheats.Effective(setting, CheatLocked(), entry.Value);
                case SettingScope.ServerAuthoritative:
                    return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
                default:
                    return setting.Normalize(entry.Value);
            }
        }

        public KeyboardShortcut Shortcut(InscribeSetting<string> setting)
        {
            if (!Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is not a keyboard shortcut.", nameof(setting));
            return ((ConfigEntry<KeyboardShortcut>)Entry(setting)).Value;
        }

        /// <summary>Whether Inscribe may act on a cheat now; tells the player why not (once per option per session).</summary>
        public bool TryUseCheat(InscribeSetting cheat, string? label = null)
        {
            if (cheat == null || cheat.Scope != SettingScope.Cheat) throw new ArgumentException("Expected an Inscribe cheat.", nameof(cheat));
            return MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat, label);
        }

        public void Shutdown()
        {
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
        }

        private static bool CheatLocked() => MeadworksAuthority.Decide(Product, ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        private void BindSetting(InscribeSetting setting)
        {
            ConfigEntryBase entry;
            if (Shortcuts.Contains(setting))
            {
                var text = (string)setting.DefaultValue;
                entry = MeadworksConfig.Bind(file, setting.Section, setting.Key, text == "" ? KeyboardShortcut.Empty : KeyboardShortcut.Deserialize(text), Describe(setting, null), Map(setting.Scope));
            }
            else
            {
                switch (setting)
                {
                    case InscribeSetting<bool> b: entry = Bind(b, null); break;
                    case InscribeSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                    case InscribeSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                    case InscribeSetting<string> s: entry = Bind(s, null); break;
                    case InscribeSetting<ConfirmMode> m: entry = Bind(m, null); break;
                    case InscribeSetting<TooltipDetail> t: entry = Bind(t, null); break;
                    case InscribeSetting<ExtraPotions.Inscribe.Runes.RecastFocus> focus: entry = Bind(focus, null); break;
                    case InscribeSetting<ExtraPotions.Inscribe.Runes.RecastSide> side: entry = Bind(side, null); break;
                    default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
                }
            }
            entries.Add(setting, entry);
        }

        private ConfigDescription Describe(InscribeSetting setting, AcceptableValueBase? acceptable, string? text = null)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, InscribeSettings.Glow.Intensity) ||
                ReferenceEquals(setting, InscribeSettings.Glow.Range) ||
                ReferenceEquals(setting, InscribeSettings.Glow.MaxLights) ||
                ReferenceEquals(setting, InscribeSettings.Glow.RefreshSeconds) ||
                ReferenceEquals(setting, InscribeSettings.Glow.GlowRunes) ||
                ReferenceEquals(setting, InscribeSettings.Glow.LegendaryGlint))
                dependency = new ConfigDependency(() => Get(InscribeSettings.Glow.GroundGlow), () => "Requires " + InscribeSettings.Glow.GroundGlow.Section + ": " + InscribeSettings.Glow.GroundGlow.Key + ".");
            return new ConfigDescription(text ?? setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(InscribeSetting<T> setting, AcceptableValueBase? acceptable) =>
            setting.Scope == SettingScope.Cheat
                ? MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, acceptable)
                : MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable), Map(setting.Scope));

        private static int Count(SettingScope scope) => InscribeSettings.All.Count(s => s.Scope == scope);
    }
}
