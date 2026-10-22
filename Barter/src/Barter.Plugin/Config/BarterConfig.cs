// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Barter.Config;
using ExtraPotions.Barter.Pricing;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>
    /// Barter's configuration surface. Every setting of <see cref="BarterSettings.All"/> is bound through
    /// <see cref="MeadworksConfig.Bind{T}(ConfigFile,string,string,T,string,ConfigScope,AcceptableValueBase)"/> with its scope.
    /// ServerAuthoritative settings read the server's value in multiplayer (<see cref="MeadworksConfig.Effective{T}"/>) and are
    /// kept read-only in configuration managers while a server owns them (<see cref="MeadworksBlockedOptions.Track"/>);
    /// Local settings are the player's own. Main thread only.
    /// </summary>
    public sealed class BarterConfig
    {
        private const string Product = Plugin.Guid;

        private readonly ConfigFile file;
        private readonly Dictionary<BarterSetting, ConfigEntryBase> entries = new Dictionary<BarterSetting, ConfigEntryBase>();

        public event EventHandler Changed;

        private BarterConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in BarterSettings.All) BindSetting(setting);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
            file.SettingChanged += OnSettingChanged;
        }

        /// <summary>Binds every Barter setting on the plugin's config file and starts tracking the ones a server owns.</summary>
        public static BarterConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            var config = new BarterConfig(file);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local))
                MeadworksBlockedOptions.Track(Product, pair.Value, ConfigScope.ServerAuthoritative);
            log?.Verbose("Configuration bound: " + config.entries.Count(p => p.Key.Scope == SettingScope.Local) + " Local, "
                + config.entries.Count(p => p.Key.Scope == SettingScope.ServerAuthoritative) + " ServerAuthoritative.");
            return config;
        }

        public ConfigEntryBase Entry(BarterSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Barter setting.", nameof(setting));

        /// <summary>The value Barter acts on now: the server's for a ServerAuthoritative setting, the player's for a Local one.</summary>
        public T Get<T>(BarterSetting<T> setting)
        {
            var entry = (ConfigEntry<T>)Entry(setting);
            return setting.Scope == SettingScope.ServerAuthoritative
                ? setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default))
                : setting.Normalize(entry.Value);
        }

        public T GetFor<T>(BarterSetting<T> setting, string trader) => TraderPolicies.Get(setting, trader, Get(setting));

        /// <summary>The purse rules in force, from the (server's) settings.</summary>
        public PurseRules Purse()
        {
            return new PurseRules
            {
                Enabled = Get(BarterSettings.Purse.Enabled),
                Flexible = Get(BarterSettings.Purse.FlexiblePricing),
                Minimum = Get(BarterSettings.Purse.MinimumBalance),
                Maximum = Get(BarterSettings.Purse.MaximumBalance),
                DailyIncrease = Get(BarterSettings.Purse.DailyIncrease),
                DailyDecrease = Get(BarterSettings.Purse.DailyDecrease),
                Discount = Get(BarterSettings.Purse.Discount),
                Markup = Get(BarterSettings.Purse.Markup),
                ReplenishDays = Get(BarterSettings.Purse.ReplenishDays),
            };
        }

        public void Shutdown()
        {
            file.SettingChanged -= OnSettingChanged;
            Changed = null;
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs args) => Changed?.Invoke(this, EventArgs.Empty);

        private void BindSetting(BarterSetting setting)
        {
            ConfigEntryBase entry;
            switch (setting)
            {
                case BarterSetting<bool> b: entry = Bind(b, null); break;
                case BarterSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum, (int)i.Maximum)); break;
                case BarterSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum, (float)f.Maximum)); break;
                case BarterSetting<string> s: entry = Bind(s, null); break;
                default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
            }
            entries.Add(setting, entry);
        }

        private ConfigEntry<T> Bind<T>(BarterSetting<T> setting, AcceptableValueBase acceptable) =>
            MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, setting.Description,
                setting.Scope == SettingScope.Local ? ConfigScope.Local : ConfigScope.ServerAuthoritative, acceptable);
    }
}
