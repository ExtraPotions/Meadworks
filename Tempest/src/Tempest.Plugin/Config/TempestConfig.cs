// Tempest - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Keel.Plugin and Ember.Plugin (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Config;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Weather;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// Tempest's configuration surface (TP06a, design section 4, TP11). Every setting of <see cref="TempestSettings.All"/> is
    /// bound through Crucible C01 with its scope: cheats through <c>MeadworksConfig.BindCheat</c> with their Mischief
    /// category, the rest through <c>MeadworksConfig.Bind</c>. Values are read at use time, so edits, reloads and server
    /// pushes apply at once. Main thread only.
    /// </summary>
    public sealed class TempestConfig
    {
        private const string Product = Plugin.Guid;

        private readonly ConfigFile file;
        private readonly Dictionary<TempestSetting, ConfigEntryBase> entries = new Dictionary<TempestSetting, ConfigEntryBase>();
        private readonly HashSet<string> droppedLogged = new HashSet<string>(StringComparer.Ordinal);
        private MeadworksLog? log;
        private readonly List<string> upgradeNotes = new List<string>();

        private TempestConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in TempestSettings.All) BindSetting(setting);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
        }

        public ConfigFile File => file;

        /// <summary>Raised after any Tempest entry changed (local edit, reload or server push).</summary>
        public event Action? Changed;

        public static TempestConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new TempestConfig(file) { log = log };
            foreach (var note in config.upgradeNotes) log.Info(note);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local && !p.Key.Hidden))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            file.SettingChanged += config.OnSettingChanged;
            file.ConfigReloaded += config.OnReloaded;
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
        public static MischiefCategory CategoryOf(TempestSetting cheat) =>
            TempestSettings.CategoryOf(cheat) == CheatCategory.World ? MischiefCategory.World : MischiefCategory.Personal;

        public ConfigEntryBase Entry(TempestSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Tempest setting.", nameof(setting));

        /// <summary>The value Tempest acts on now.</summary>
        public T Get<T>(TempestSetting<T> setting)
        {
            var entry = (ConfigEntry<T>)Entry(setting);
            switch (setting.Scope)
            {
                case SettingScope.Cheat:
                    return TempestCheats.Effective(setting, CheatLocked(), entry.Value);
                case SettingScope.ServerAuthoritative:
                    return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
                default:
                    return setting.Normalize(entry.Value);
            }
        }

        /// <summary>The schedule rules of design 4.1 from the current values.</summary>
        public ScheduleRules Rules()
        {
            var s = TempestSettings.Schedule;
            return TempestSettings.Rules(Get(s.Enabled), Get(s.BlizzardChance), Get(s.StormChance), Get(s.MinimumDays), Get(s.WarningSeconds),
                Get(s.ActiveMinSeconds), Get(s.ActiveMaxSeconds), Biomes(s.BlizzardBiomes, EventKind.Blizzard), Biomes(s.StormBiomes, EventKind.Thunderstorm),
                Get(s.DryStormChance), Get(s.FogChance), Biomes(s.FogBiomes, EventKind.DenseFog), Get(s.AshStormChance), Biomes(s.AshStormBiomes, EventKind.AshStorm),
                Get(s.MeteorShowerChance), Biomes(s.MeteorShowerBiomes, EventKind.MeteorShower));
        }

        /// <summary>A biome list as the scheduler uses it; names it drops are logged once per configured value.</summary>
        private string Biomes(TempestSetting<string> setting, EventKind kind)
        {
            var raw = MeadworksConfig.Effective((ConfigEntry<string>)Entry(setting), Product, ConfigScope.ServerAuthoritative, setting.Default);
            var dropped = BiomeSets.Dropped(kind, raw);
            if (dropped.Count > 0 && droppedLogged.Add(setting.Key + "=" + raw))
                log?.Warning(setting.Key + ": dropped " + string.Join(", ", dropped) + ". Tempest events never occur in the Mountain or Mistlands, and only a blizzard reaches the Deep North; this kind covers only "
                    + BiomeSets.Format(BiomeSets.Allowed(kind)) + ".");
            return setting.Normalize(raw);
        }

        /// <summary>The weather filter from the current values (TP11).</summary>
        public WeatherFilter Filter()
        {
            var f = TempestSettings.Filter;
            return new WeatherFilter(s => Get(f.Switches[s]), Get(f.OtherDisabled), TempestSeasons.Weights(this));
        }

        /// <summary>Whether Tempest may act on a cheat now; tells the player why not (once per option per session).</summary>
        public bool TryUseCheat(TempestSetting<bool> cheat, string? label = null)
        {
            if (cheat == null || cheat.Scope != SettingScope.Cheat) throw new ArgumentException("Expected a Tempest cheat.", nameof(cheat));
            return Get(cheat) && MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat, label);
        }

        public void Shutdown()
        {
            file.SettingChanged -= OnSettingChanged;
            file.ConfigReloaded -= OnReloaded;
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
            Changed = null;
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e) => Changed?.Invoke();

        private void OnReloaded(object sender, EventArgs e) => Changed?.Invoke();

        private static bool CheatLocked() => MeadworksAuthority.Decide(Product, ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        private void BindSetting(TempestSetting setting)
        {
            ConfigEntryBase entry;
            switch (setting)
            {
                case TempestSetting<bool> b: entry = Bind(b, null); break;
                case TempestSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                case TempestSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                case TempestSetting<string> s: entry = Upgrade(s, Bind(s, null)); break;
                case TempestSetting<FreezeLevel> l: entry = Bind(l, null); break;
                default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
            }
            entries.Add(setting, entry);
        }

        // A saved value that is exactly an earlier default moves to the current one and is written back (the constructor saves);
        // a customized Blizzard Biomes list without the Deep North stays and is named once in the log.
        private ConfigEntry<string> Upgrade(TempestSetting<string> setting, ConfigEntry<string> entry)
        {
            string? upgraded = DefaultUpgrades.Upgrade(setting, entry.Value);
            if (upgraded != null)
            {
                entry.Value = upgraded;
                upgradeNotes.Add(setting.Section + " / " + setting.Key + ": saved earlier default updated to \"" + upgraded + "\".");
            }
            else if (DefaultUpgrades.MissesDeepNorth(setting, entry.Value))
                upgradeNotes.Add(setting.Section + " / " + setting.Key + ": customized list kept; add DeepNorth to let blizzards reach the Deep North as a whiteout.");
            return entry;
        }

        private ConfigDescription Describe(TempestSetting setting, AcceptableValueBase? acceptable)
        {
            ConfigDependency? dependency = null;
            if (!ReferenceEquals(setting, TempestSettings.Schedule.Enabled) && TempestSettings.Schedule.Entries().Contains(setting))
                dependency = new ConfigDependency(() => Get(TempestSettings.Schedule.Enabled), () => "Requires " + TempestSettings.Schedule.Enabled.Section + ": " + TempestSettings.Schedule.Enabled.Key + ".");
            var hazards = TempestSettings.Effects.HazardsAffectPlayers;
            if (ReferenceEquals(setting, TempestSettings.Effects.HazardDamage))
                dependency = new ConfigDependency(() => Get(hazards), () => "Requires " + hazards.Section + ": " + hazards.Key + ".");
            return new ConfigDescription(setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(TempestSetting<T> setting, AcceptableValueBase? acceptable)
        {
            if (setting.Scope == SettingScope.Cheat)
                return MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, CategoryOf(setting), acceptable);
            if (setting.Hidden)
                return MeadworksConfig.BindHidden(file, setting.Section, setting.Key, setting.Default, setting.Description, Map(setting.Scope), acceptable);
            return MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable), Map(setting.Scope));
        }

        private static int Count(SettingScope scope) => TempestSettings.All.Count(s => s.Scope == scope && !s.Hidden);
    }
}
