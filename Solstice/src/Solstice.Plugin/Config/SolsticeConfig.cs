// Solstice - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Tempest.Plugin and Keel.Plugin (expDARE).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Config;
using ExtraPotions.Solstice.Effects;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Solstice's configuration surface. Every setting of <see cref="SolsticeSettings.All"/> is bound through Crucible C01 with its
    /// scope: the cheat through <c>MeadworksConfig.BindCheat</c> with its Mischief category, the rest through
    /// <c>MeadworksConfig.Bind</c>. Values are read at use time, so edits, reloads and server pushes apply at once. Main thread only.
    /// </summary>
    public sealed class SolsticeConfig
    {
        private const string Product = Plugin.Guid;

        private readonly ConfigFile file;
        private readonly Dictionary<SolsticeSetting, ConfigEntryBase> entries = new Dictionary<SolsticeSetting, ConfigEntryBase>();

        private SolsticeConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in SolsticeSettings.All) BindSetting(setting);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
        }

        public ConfigFile File => file;

        /// <summary>Raised after any Solstice entry changed (local edit, reload or server push).</summary>
        public event Action? Changed;

        public static SolsticeConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new SolsticeConfig(file);
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

        /// <summary>The Hex Mischief category of the cheat (Crucible MischiefGate).</summary>
        public static MischiefCategory CategoryOf(SolsticeSetting cheat) =>
            SolsticeSettings.CategoryOf(cheat) == CheatCategory.World ? MischiefCategory.World : MischiefCategory.Personal;

        public ConfigEntryBase Entry(SolsticeSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Solstice setting.", nameof(setting));

        /// <summary>The value Solstice acts on now.</summary>
        public T Get<T>(SolsticeSetting<T> setting)
        {
            var entry = (ConfigEntry<T>)Entry(setting);
            switch (setting.Scope)
            {
                case SettingScope.Cheat:
                    return SolsticeCheats.Effective(setting, CheatLocked(), entry.Value);
                case SettingScope.ServerAuthoritative:
                    return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
                default:
                    return setting.Normalize(entry.Value);
            }
        }

        /// <summary>The calendar from the current season lengths.</summary>
        public SeasonCalendar Calendar() => SolsticeSettings.CalendarFrom(Get);

        /// <summary>The shield dome rules from the current values.</summary>
        public ProtectionRules Protection() => SolsticeSettings.RulesFrom(Get);

        /// <summary>The effects of one season from the current values.</summary>
        public SeasonEffects Effects(Season season) => SolsticeSettings.Seasons.For(season).Resolve(Get, Get);

        /// <summary>The season the Season Override cheat holds the world in, or null (off, or the cheat is blocked).</summary>
        public Season? HeldSeason(out bool blocked)
        {
            var cheat = SolsticeSettings.Cheats.SeasonOverride;
            var wanted = ((ConfigEntry<SeasonOverride>)Entry(cheat)).Value;
            blocked = wanted != SeasonOverride.Off && CheatLocked();
            return Get(cheat).ToSeason();
        }

        /// <summary>Tells the player why a wanted cheat is blocked (once per option per session, by Crucible).</summary>
        public void ReportBlockedCheat(SolsticeSetting<SeasonOverride> cheat, string? label = null)
        {
            if (cheat == null || cheat.Scope != SettingScope.Cheat) throw new ArgumentException("Expected a Solstice cheat.", nameof(cheat));
            MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat, label);
        }

        public void Shutdown()
        {
            file.SettingChanged -= OnSettingChanged;
            file.ConfigReloaded -= OnReloaded;
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
            Changed = null;
        }

        /// <summary>Writes import values that still equal the setting's default. Returns how many changed.</summary>
        public int ApplyImport(IReadOnlyList<ImportChange> changes)
        {
            if (changes == null) return 0;
            int applied = 0;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var change in changes)
                {
                    var setting = SolsticeSettings.Find(change.Id);
                    if (setting == null || !entries.TryGetValue(setting, out var entry)) continue;
                    if (ApplyOne(setting, entry, change.Text)) applied++;
                }
            }
            finally
            {
                file.SaveOnConfigSet = save;
                if (applied > 0) file.Save();
            }
            return applied;
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e) => Changed?.Invoke();

        private void OnReloaded(object sender, EventArgs e) => Changed?.Invoke();

        private static bool CheatLocked() => MeadworksAuthority.Decide(Product, ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        private void BindSetting(SolsticeSetting setting)
        {
            ConfigEntryBase entry;
            switch (setting)
            {
                case SolsticeSetting<bool> b: entry = Bind(b, null); break;
                case SolsticeSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                case SolsticeSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                case SolsticeSetting<string> t: entry = Bind(t, null); break;
                case SolsticeSetting<SeasonOverride> o: entry = Bind(o, null); break;
                default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
            }
            entries.Add(setting, entry);
        }

        private ConfigEntry<T> Bind<T>(SolsticeSetting<T> setting, AcceptableValueBase? acceptable)
        {
            if (setting.Scope == SettingScope.Cheat)
                return MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, CategoryOf(setting), acceptable);
            if (setting.Hidden)
                return MeadworksConfig.BindHidden(file, setting.Section, setting.Key, setting.Default, setting.Description, Map(setting.Scope), acceptable);
            return MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, setting.Description, Map(setting.Scope), acceptable);
        }

        private static int Count(SettingScope scope) => SolsticeSettings.All.Count(s => s.Scope == scope && !s.Hidden);

        static bool ApplyOne(SolsticeSetting setting, ConfigEntryBase entry, string text)
        {
            switch (setting)
            {
                case SolsticeSetting<bool> toggle:
                    if (!bool.TryParse(text, out var flag)) return false;
                    var boolEntry = (ConfigEntry<bool>)entry;
                    if (!SeasonConfigImport.ShouldApply(boolEntry.Value ? "true" : "false", toggle.Default ? "true" : "false", flag ? "true" : "false")) return false;
                    boolEntry.Value = toggle.Normalize(flag);
                    return true;
                case SolsticeSetting<int> integer:
                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole)) return false;
                    whole = integer.Normalize(whole);
                    var intEntry = (ConfigEntry<int>)entry;
                    if (!SeasonConfigImport.ShouldApply(intEntry.Value.ToString(CultureInfo.InvariantCulture), integer.Default.ToString(CultureInfo.InvariantCulture), whole.ToString(CultureInfo.InvariantCulture))) return false;
                    intEntry.Value = whole;
                    return true;
                case SolsticeSetting<float> number:
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)) return false;
                    amount = number.Normalize(amount);
                    var floatEntry = (ConfigEntry<float>)entry;
                    if (!SeasonConfigImport.ShouldApply(Format(floatEntry.Value), Format(number.Default), Format(amount))) return false;
                    floatEntry.Value = amount;
                    return true;
                default:
                    return false;
            }
        }

        static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
