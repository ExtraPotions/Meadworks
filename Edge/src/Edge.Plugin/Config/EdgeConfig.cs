// Edge - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Wildblood.Plugin and Ember.Plugin (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Edge.Config;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Edge.Runtime
{
    /// <summary>
    /// Edge's configuration surface (D02a, design section 4). Every setting of <see cref="EdgeSettings.All"/> is bound
    /// through Crucible C01 with its scope: cheats through <see cref="MeadworksConfig.BindCheat{T}(ConfigFile,string,string,T,string,AcceptableValueBase)"/>,
    /// product-managed entries (YAML carriers, import marker) through <see cref="MeadworksConfig.BindHidden{T}"/>, the rest
    /// through <see cref="MeadworksConfig.Bind{T}(ConfigFile,string,string,T,string,ConfigScope,AcceptableValueBase)"/>.
    /// Values are read at use time. Main thread only.
    /// </summary>
    public sealed class EdgeConfig
    {
        private const string Product = Plugin.Guid;

        private readonly ConfigFile file;
        private readonly Dictionary<EdgeSetting, ConfigEntryBase> entries = new Dictionary<EdgeSetting, ConfigEntryBase>();

        private EdgeConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in EdgeSettings.All) BindSetting(setting);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
        }

        /// <summary>Binds every Edge setting on the plugin's config file and tracks blocked options (C03).</summary>
        public static EdgeConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new EdgeConfig(file);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local && !p.Key.Hidden))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            log.Verbose("Configuration bound: " + Count(SettingScope.Local) + " Local, " + Count(SettingScope.ServerAuthoritative)
                + " ServerAuthoritative, " + Count(SettingScope.Cheat) + " Cheat, " + EdgeSettings.All.Count(s => s.Hidden) + " hidden.");
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

        public ConfigEntryBase Entry(EdgeSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not an Edge setting.", nameof(setting));

        /// <summary>The value Edge acts on now.</summary>
        public T Get<T>(EdgeSetting<T> setting)
        {
            var entry = (ConfigEntry<T>)Entry(setting);
            switch (setting.Scope)
            {
                case SettingScope.Cheat:
                    return EdgeCheats.Effective(setting, CheatLocked(), entry.Value);
                case SettingScope.ServerAuthoritative:
                    return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
                default:
                    return setting.Normalize(entry.Value);
            }
        }

        /// <summary>No Secondary Cooldowns in effect (off unless an authority allows it).</summary>
        public bool NoSecondaryCooldowns => Get(EdgeSettings.Cheats.NoSecondaryCooldowns);

        /// <summary>Whether Edge may act on a cheat now; tells the player why not (once per option per session).</summary>
        public bool TryUseCheat(EdgeSetting cheat, string? label = null)
        {
            if (cheat == null || cheat.Scope != SettingScope.Cheat) throw new ArgumentException("Expected an Edge cheat.", nameof(cheat));
            return MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat, label);
        }

        public void Shutdown()
        {
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
        }

        private static bool CheatLocked() => MeadworksAuthority.Decide(Product, ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        private void BindSetting(EdgeSetting setting)
        {
            ConfigEntryBase entry;
            switch (setting)
            {
                case EdgeSetting<bool> b: entry = Bind(b, null); break;
                case EdgeSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                case EdgeSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                case EdgeSetting<string> s: entry = Bind(s, null); break;
                case EdgeSetting<RangedPreset> r: entry = Bind(r, null); break;
                case EdgeSetting<BombPreset> bomb: entry = Bind(bomb, null); break;
                case EdgeSetting<SummonQualityPreset> q: entry = Bind(q, null); break;
                case EdgeSetting<SlopeMode> slope: entry = Bind(slope, null); break;
                case EdgeSetting<SwimMode> swim: entry = Bind(swim, null); break;
                default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
            }
            entries.Add(setting, entry);
        }

        private ConfigDescription Describe(EdgeSetting setting, AcceptableValueBase? acceptable, string? text = null)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, EdgeSettings.Slope.Mode) ||
                ReferenceEquals(setting, EdgeSettings.Slope.AttackHeight) ||
                ReferenceEquals(setting, EdgeSettings.Slope.AttackOffset) ||
                ReferenceEquals(setting, EdgeSettings.Slope.AdaptiveSlopeThreshold) ||
                ReferenceEquals(setting, EdgeSettings.Slope.AdaptiveMaxPitch))
                dependency = new ConfigDependency(() => Get(EdgeSettings.General.SlopeAssistEnabled), () => "Requires " + EdgeSettings.General.SlopeAssistEnabled.Section + ": " + EdgeSettings.General.SlopeAssistEnabled.Key + ".");
            if (ReferenceEquals(setting, EdgeSettings.Equipment.SwimMode) ||
                ReferenceEquals(setting, EdgeSettings.Equipment.RestoreDelay) ||
                ReferenceEquals(setting, EdgeSettings.Equipment.AutoEquipLastShield))
                dependency = new ConfigDependency(() => Get(EdgeSettings.General.EquipmentEnabled), () => "Requires " + EdgeSettings.General.EquipmentEnabled.Section + ": " + EdgeSettings.General.EquipmentEnabled.Key + ".");
            if (ReferenceEquals(setting, EdgeSettings.General.DodgeCancelOnlyBeforeHit) ||
                ReferenceEquals(setting, EdgeSettings.General.DodgeCancelExtraStaminaCost))
                dependency = new ConfigDependency(() => Get(EdgeSettings.General.DodgeCancel), () => "Requires " + EdgeSettings.General.DodgeCancel.Section + ": " + EdgeSettings.General.DodgeCancel.Key + ".");
            if (ReferenceEquals(setting, EdgeSettings.Interface.BowZoomFactor))
                dependency = new ConfigDependency(() => Get(EdgeSettings.Interface.BowZoom), () => "Requires " + EdgeSettings.Interface.BowZoom.Section + ": " + EdgeSettings.Interface.BowZoom.Key + ".");
            return new ConfigDescription(text ?? setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(EdgeSetting<T> setting, AcceptableValueBase? acceptable)
        {
            if (setting.Scope == SettingScope.Cheat)
                return MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, acceptable);
            if (setting.Hidden)
                return MeadworksConfig.BindHidden(file, setting.Section, setting.Key, setting.Default, setting.Description, Map(setting.Scope), acceptable);
            var description = ReferenceEquals(setting, EdgeSettings.General.DodgeCancel)
                ? EdgeText.Text("dodge_cancel_description")
                : ReferenceEquals(setting, EdgeSettings.Equipment.KeepCrossbowLoaded) ? EdgeText.Text("keep_crossbow_loaded_description")
                : ReferenceEquals(setting, EdgeSettings.Ranged.ArrowRecoveryChance) ? EdgeText.Text("arrow_recovery_chance_description")
                : ReferenceEquals(setting, EdgeSettings.Interface.BowZoom) ? EdgeText.Text("bow_zoom_description")
                : ReferenceEquals(setting, EdgeSettings.Interface.BowZoomFactor) ? EdgeText.Text("bow_zoom_factor_description")
                : ReferenceEquals(setting, EdgeSettings.Interface.DrawIndicator) ? EdgeText.Text("draw_indicator_description")
                : setting.Description;
            return MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable, description), Map(setting.Scope));
        }

        private static int Count(SettingScope scope) => EdgeSettings.All.Count(s => s.Scope == scope && !s.Hidden);
    }
}
