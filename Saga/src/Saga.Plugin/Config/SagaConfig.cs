// Saga - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Edge.Plugin and Wildblood.Plugin (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Powers;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Saga.Runtime
{
    /// <summary>
    /// Saga's configuration surface (Y02a, design section 4). Every setting of <see cref="SagaSettings.All"/> is bound
    /// through Crucible C01 with its scope: cheats through <see cref="MeadworksConfig.BindCheat{T}(ConfigFile,string,string,T,string,AcceptableValueBase)"/>,
    /// product-managed entries (YAML carriers, import marker) through <see cref="MeadworksConfig.BindHidden{T}"/>, the rest
    /// through <see cref="MeadworksConfig.Bind{T}(ConfigFile,string,string,T,string,ConfigScope,AcceptableValueBase)"/>.
    /// Values are read at use time. Main thread only.
    /// </summary>
    public sealed class SagaConfig
    {
        private const string Product = Plugin.Guid;

        private readonly ConfigFile file;
        private readonly Dictionary<SagaSetting, ConfigEntryBase> entries = new Dictionary<SagaSetting, ConfigEntryBase>();

        private SagaConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in SagaSettings.All) BindSetting(setting);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
        }

        /// <summary>Binds every Saga setting on the plugin's config file and tracks blocked options (C03).</summary>
        public static SagaConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new SagaConfig(file);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local && !p.Key.Hidden))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            log.Verbose("Configuration bound: " + Count(SettingScope.Local) + " Local, " + Count(SettingScope.ServerAuthoritative)
                + " ServerAuthoritative, " + Count(SettingScope.Cheat) + " Cheat, " + SagaSettings.All.Count(s => s.Hidden) + " hidden.");
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

        public ConfigEntryBase Entry(SagaSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Saga setting.", nameof(setting));

        public object GetBoxed(SagaSetting setting) {
            if(setting is SagaSetting<bool> b)return Get(b);
            if(setting is SagaSetting<float> f)return Get(f);
            if(setting is SagaSetting<int> i)return Get(i);
            if(setting is SagaSetting<string> s)return Get(s);
            if(setting is SagaSetting<SkillGainNotices> n)return Get(n);
            if(setting is SagaSetting<PowerDisplayMode> p)return Get(p);
            if(setting is SagaSetting<Resistance> r)return Get(r);
            throw new ArgumentException("Unsupported Saga setting "+setting.Id);
        }

        /// <summary>The value Saga acts on now.</summary>
        public T Get<T>(SagaSetting<T> setting)
        {
            var entry = (ConfigEntry<T>)Entry(setting);
            switch (setting.Scope)
            {
                case SettingScope.Cheat:
                    return SagaCheats.Effective(setting, CheatLocked(), entry.Value);
                case SettingScope.ServerAuthoritative:
                    return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
                default:
                    return setting.Normalize(entry.Value);
            }
        }

        /// <summary>The forced mastery level of every power, or null (off unless an authority allows the cheat).</summary>
        public int? MasteryOverride => SagaCheats.MasteryOverride(CheatLocked(), ((ConfigEntry<bool>)Entry(SagaSettings.Cheats.MasteryOverride)).Value,
            Get(SagaSettings.Cheats.MasteryOverrideLevel));

        /// <summary>Whether Saga may act on a cheat now; tells the player why not (once per option per session).</summary>
        public bool TryUseCheat(SagaSetting cheat, string? label = null)
        {
            if (cheat == null || cheat.Scope != SettingScope.Cheat) throw new ArgumentException("Expected a Saga cheat.", nameof(cheat));
            return MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat, label);
        }

        public void Shutdown()
        {
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
        }

        private static bool CheatLocked() => MeadworksAuthority.Decide(Product, ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        private void BindSetting(SagaSetting setting)
        {
            ConfigEntryBase entry;
            switch (setting)
            {
                case SagaSetting<bool> b: entry = Bind(b, null); break;
                case SagaSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                case SagaSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                case SagaSetting<string> s: entry = Bind(s, null); break;
                case SagaSetting<SkillGainNotices> notices: entry = Bind(notices, null); break;
                case SagaSetting<PowerDisplayMode> display: entry = Bind(display, null); break;
                case SagaSetting<Resistance> resistance: entry = Bind(resistance, null); break;
                default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
            }
            entries.Add(setting, entry);
        }

        private ConfigDescription Describe(SagaSetting setting, AcceptableValueBase? acceptable)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, SagaSettings.Powers.MaxAttunedPowers) ||
                ReferenceEquals(setting, SagaSettings.Powers.KillCreditRange) ||
                ReferenceEquals(setting, SagaSettings.Powers.BlockPowerActivation) ||
                ReferenceEquals(setting, SagaSettings.Powers.ShowLockedFeatures))
                dependency = new ConfigDependency(() => Get(SagaSettings.General.Enabled) && Get(SagaSettings.General.PowersEnabled),
                    () => "Requires General: Enabled and Powers Enabled.");
            if (ReferenceEquals(setting, SagaSettings.Sneak.SpeedAt100) || ReferenceEquals(setting, SagaSettings.Sneak.VisibilitySkillFactor))
                dependency = new ConfigDependency(() => Get(SagaSettings.General.Enabled) && Get(SagaSettings.General.SneakEnabled),
                    () => "Requires General: Enabled and Sneak Enabled.");
            return new ConfigDescription(setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(SagaSetting<T> setting, AcceptableValueBase? acceptable)
        {
            if (setting.Scope == SettingScope.Cheat)
                return MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, acceptable);
            if (setting.Hidden)
                return MeadworksConfig.BindHidden(file, setting.Section, setting.Key, setting.Default, setting.Description, Map(setting.Scope), acceptable);
            return MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable), Map(setting.Scope));
        }

        private static int Count(SettingScope scope) => SagaSettings.All.Count(s => s.Scope == scope && !s.Hidden);
    }
}
