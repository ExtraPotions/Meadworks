// Cairn - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Edge.Plugin, Wildblood.Plugin and Ember.Plugin (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Radius;
using ExtraPotions.Cairn.Skill;
using ExtraPotions.Cairn.Visibility;
using ExtraPotions.Cairn.Localization;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Cairn.Runtime
{
    /// <summary>
    /// Cairn's configuration surface (CA02a). Every setting of <see cref="CairnSettings.All"/> is bound through Crucible C01
    /// with its scope; the import marker through <see cref="MeadworksConfig.BindHidden{T}"/>. Cairn binds no cheat. Values
    /// are read at use time. Main thread only.
    /// </summary>
    public sealed class CairnConfig
    {
        private const string Product = Plugin.Guid;

        private readonly ConfigFile file;
        private readonly Dictionary<CairnSetting, ConfigEntryBase> entries = new Dictionary<CairnSetting, ConfigEntryBase>();

        private CairnConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in CairnSettings.All) BindSetting(setting);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
        }

        /// <summary>Binds every Cairn setting on the plugin's config file and tracks blocked options (C03).</summary>
        public static CairnConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new CairnConfig(file);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local && !p.Key.Hidden))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            log.Verbose("Configuration bound: " + Count(SettingScope.Local) + " Local, " + Count(SettingScope.ServerAuthoritative)
                + " ServerAuthoritative, " + CairnSettings.All.Count(s => s.Hidden) + " hidden.");
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

        public ConfigEntryBase Entry(CairnSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Cairn setting.", nameof(setting));

        /// <summary>The value Cairn acts on now: the server's value for ServerAuthoritative settings when Cairn runs there.</summary>
        public T Get<T>(CairnSetting<T> setting)
        {
            // A key setting reads as its BepInEx text ("LeftAlt", "None"), so generic text rows can show it.
            if (Entry(setting) is ConfigEntry<KeyboardShortcut> key) return setting.Normalize((T)(object)key.Value.Serialize());
            var entry = (ConfigEntry<T>)Entry(setting);
            if (setting.Scope == SettingScope.ServerAuthoritative)
                return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
            return setting.Normalize(entry.Value);
        }

        /// <summary>The player's entry of a key setting from <see cref="CairnSettings.KeyboardShortcuts"/> (all are Local).</summary>
        public ConfigEntry<KeyboardShortcut> ShortcutEntry(CairnSetting<string> setting) =>
            Entry(setting) as ConfigEntry<KeyboardShortcut> ?? throw new ArgumentException(setting.Id + " is not a keyboard shortcut.", nameof(setting));

        /// <summary>The radius values in effect, held at Pathfinder's defaults when <paramref name="held"/> (Q1).</summary>
        public RadiusValues Radius(bool held)
        {
            var r = new RadiusValues(Get(CairnSettings.Radius.MinimumRadius), Get(CairnSettings.Radius.MaximumRadius),
                Get(CairnSettings.Radius.LandExploreRadius), Get(CairnSettings.Radius.SeaExploreRadius), Get(CairnSettings.Radius.AltitudeRadiusBonus),
                Get(CairnSettings.Radius.ForestRadiusPenalty), Get(CairnSettings.Radius.DaylightRadiusScale), Get(CairnSettings.Radius.WeatherRadiusScale));
            return held ? r.Held() : r;
        }

        public SkillValues Skill => new SkillValues(Get(CairnSettings.Skill.Enabled), Get(CairnSettings.Skill.ExperiencePerSquareKilometre),
            Get(CairnSettings.Skill.LocationDiscoveryExperience), Get(CairnSettings.Skill.NewBiomeExperience), Get(CairnSettings.Skill.LandRadiusAt100),
            Get(CairnSettings.Skill.WeatherPenaltyReductionAt100), Get(CairnSettings.Skill.NightPenaltyReductionAt100),
            Get(CairnSettings.Skill.VantageLevel), Get(CairnSettings.Skill.VantageCooldown));

        /// <summary>Whether Cairn removes <paramref name="effect"/> now (toggle, server allowance, Q1 hold).</summary>
        public bool Removes(VisibilityEffect effect, bool held) =>
            VisibilityPolicy.Removes(effect, Get(CairnSettings.Visibility.Remove(effect)), Get(CairnSettings.VisibilityServer.Allow(effect)), held);

        /// <summary>The graded fraction in force for a Visibility Strength setting, gated by the Allow Removing of every effect it covers.</summary>
        public float Strength(CairnSetting<float> setting, bool held, params VisibilityEffect[] gates)
        {
            var allowed = true;
            var gateLike = false;
            foreach (var effect in gates)
            {
                allowed &= Get(CairnSettings.VisibilityServer.Allow(effect));
                gateLike |= VisibilityPolicy.IsGateLike(effect);
            }
            return VisibilityPolicy.Strength(Get(setting), allowed, held, gateLike);
        }

        public void Shutdown()
        {
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
        }

        private void BindSetting(CairnSetting setting)
        {
            ConfigEntryBase entry;
            switch (setting)
            {
                // Typed as KeyboardShortcut so MeadWorks key-clash checks (Almanac) and native-binding warnings see it.
                case CairnSetting<string> key when CairnSettings.KeyboardShortcuts.Contains(key):
                    entry = MeadworksConfig.Bind(file, key.Section, key.Key, ParseShortcut(key.Default), Describe<KeyboardShortcut>(key, null), Map(key.Scope));
                    break;
                case CairnSetting<bool> b: entry = Bind(b, null); break;
                case CairnSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                case CairnSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                case CairnSetting<string> s: entry = Bind(s, null); break;
                default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
            }
            entries.Add(setting, entry);
        }

        private ConfigDescription Describe<T>(CairnSetting setting, AcceptableValueBase? acceptable)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, CairnSettings.Hud.ShowDay) ||
                ReferenceEquals(setting, CairnSettings.Hud.ShowTime) ||
                ReferenceEquals(setting, CairnSettings.Hud.ShowWeather) ||
                ReferenceEquals(setting, CairnSettings.Hud.ShowForecast) ||
                ReferenceEquals(setting, CairnSettings.Hud.ShowCompass) ||
                ReferenceEquals(setting, CairnSettings.Hud.ShowHeading) ||
                ReferenceEquals(setting, CairnSettings.Hud.ShowMarkers) ||
                ReferenceEquals(setting, CairnSettings.Hud.TempestWarnings) ||
                ReferenceEquals(setting, CairnSettings.Radius.ShowRadius) ||
                ReferenceEquals(setting, CairnSettings.Radius.ShowRadiusInputs))
                dependency = new ConfigDependency(() => Get(CairnSettings.Hud.Enabled), () => "Requires " + CairnSettings.Hud.Enabled.Section + ": " + CairnSettings.Hud.Enabled.Key + ".");
            if (ReferenceEquals(setting, CairnSettings.Radius.MinimumRadius) ||
                ReferenceEquals(setting, CairnSettings.Radius.MaximumRadius) ||
                ReferenceEquals(setting, CairnSettings.Radius.LandExploreRadius) ||
                ReferenceEquals(setting, CairnSettings.Radius.SeaExploreRadius) ||
                ReferenceEquals(setting, CairnSettings.Radius.AltitudeRadiusBonus) ||
                ReferenceEquals(setting, CairnSettings.Radius.ForestRadiusPenalty) ||
                ReferenceEquals(setting, CairnSettings.Radius.DaylightRadiusScale) ||
                ReferenceEquals(setting, CairnSettings.Radius.WeatherRadiusScale))
                dependency = new ConfigDependency(() => Get(CairnSettings.Radius.Enabled), () => "Requires " + CairnSettings.Radius.Enabled.Section + ": " + CairnSettings.Radius.Enabled.Key + ".");
            if (ReferenceEquals(setting, CairnSettings.Skill.ExperiencePerSquareKilometre) ||
                ReferenceEquals(setting, CairnSettings.Skill.LocationDiscoveryExperience) ||
                ReferenceEquals(setting, CairnSettings.Skill.NewBiomeExperience) ||
                ReferenceEquals(setting, CairnSettings.Skill.LandRadiusAt100) ||
                ReferenceEquals(setting, CairnSettings.Skill.WeatherPenaltyReductionAt100) ||
                ReferenceEquals(setting, CairnSettings.Skill.NightPenaltyReductionAt100) ||
                ReferenceEquals(setting, CairnSettings.Skill.VantageLevel) ||
                ReferenceEquals(setting, CairnSettings.Skill.VantageCooldown))
                dependency = new ConfigDependency(() => Get(CairnSettings.Skill.Enabled), () => "Requires " + CairnSettings.Skill.Enabled.Section + ": " + CairnSettings.Skill.Enabled.Key + ".");
            if (ReferenceEquals(setting, CairnSettings.Hud.Use24Hour))
                dependency = new ConfigDependency(() => Get(CairnSettings.Hud.Enabled) && Get(CairnSettings.Hud.ShowTime), () => "Requires HUD: Enabled and " + CairnSettings.Hud.ShowTime.Key + ".");
            if (ReferenceEquals(setting, CairnSettings.Map.ShowAllNamesKey) || ReferenceEquals(setting, CairnSettings.Map.HoverScale))
                dependency = new ConfigDependency(() => Get(CairnSettings.Map.TidyPins), () => "Requires " + CairnSettings.Map.Section + ": " + CairnSettings.Map.TidyPins.Key + ".");
            if (ReferenceEquals(setting, CairnSettings.Hud.StripDegrees) || ReferenceEquals(setting, CairnSettings.Hud.CompassWidth))
                dependency = new ConfigDependency(() => Get(CairnSettings.Hud.Enabled) && Get(CairnSettings.Hud.ShowCompass), () => "Requires HUD: Enabled and " + CairnSettings.Hud.ShowCompass.Key + ".");
            if (ReferenceEquals(setting, CairnSettings.Hud.MaxMarkers) || ReferenceEquals(setting, CairnSettings.Hud.MaxDistance))
                dependency = new ConfigDependency(() => Get(CairnSettings.Hud.Enabled) && Get(CairnSettings.Hud.ShowMarkers), () => "Requires HUD: Enabled and " + CairnSettings.Hud.ShowMarkers.Key + ".");
            var presentation = new ConfigPresentation<T>(Plugin.Guid, (_, language) =>
            {
                if (CairnText.Translations == null) return null;
                // These existing helpers follow Cairn's current game/catalog language.
                string? label = CairnText.SettingName(setting); string? help = CairnText.SettingHelp(setting);
                var helpKey = ReferenceEquals(setting, CairnSettings.Map.TidyPins) ? CairnTextKeys.TidyPinsDescription : CairnTextKeys.SettingHelp(setting);
                if (label == CairnTextKeys.Setting(setting) || label == "setting_remove" || label == "setting_allow_removing") label = null;
                if (help == helpKey || help == "help_remove" || help == "help_allow_removing") help = null;
                return new ConfigPresentationText(label: label, help: help);
            });
            return new ConfigDescription(setting.Description, acceptable, dependency == null
                ? new object[] { presentation } : new object[] { dependency, presentation });
        }

        private ConfigEntry<T> Bind<T>(CairnSetting<T> setting, AcceptableValueBase? acceptable)
        {
            if (setting.Scope == SettingScope.Cheat)
                return MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, acceptable);
            if (setting.Hidden)
                return MeadworksConfig.BindHidden(file, setting.Section, setting.Key, setting.Default, setting.Description, Map(setting.Scope), acceptable);
            return MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe<T>(setting, acceptable), Map(setting.Scope));
        }

        /// <summary>BepInEx shortcut text; empty or unreadable text is no key.</summary>
        public static KeyboardShortcut ParseShortcut(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return KeyboardShortcut.Empty;
            try { return KeyboardShortcut.Deserialize(text); }
            catch (Exception) { return KeyboardShortcut.Empty; }
        }

        private static int Count(SettingScope scope) => CairnSettings.All.Count(s => s.Scope == scope && !s.Hidden);
    }
}
