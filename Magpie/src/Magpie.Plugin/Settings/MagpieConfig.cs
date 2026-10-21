// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Magpie.Loot;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Magpie.Runtime.Settings
{
    /// <summary>
    /// Binds every row of <see cref="MagpieSettings"/> through Crucible (C01; cheats through <c>BindCheat</c> with their
    /// safe value and no Mischief category: the cheat stays admin-only, C02). Shared settings are tracked so configuration managers show
    /// them read-only while the gate blocks them (C03). Values are read at use time through the gate
    /// (<see cref="MeadworksConfig.Effective{T}"/>) and clamped to their section 4 range.
    /// </summary>
    internal sealed partial class MagpieConfig
    {
        private readonly Dictionary<SettingSpec, ConfigEntryBase> entries = new Dictionary<SettingSpec, ConfigEntryBase>();
        private readonly Dictionary<SettingSpec, (string Text, NameList List)> lists = new Dictionary<SettingSpec, (string, NameList)>();

        public MagpieConfig(ConfigFile file, MeadworksLog log)
        {
            this.file = file; this.log = log;
            try
            {
                foreach (var spec in MagpieSettings.All)
                    entries.Add(spec, Bind(file, spec));
                foreach (var pair in entries)
                    if (pair.Key.Scope != SettingScope.Local)
                        MeadworksBlockedOptions.Track(Plugin.Guid, pair.Value, Map(pair.Key.Scope));
                lockedValues = MeadworksConfig.BindHidden(file, MagpieSettings.LockedPlayerSettings.Section, LockedValuesKey, "",
                    "Managed by Magpie: the server's serialized values for Locked Player Settings.", ConfigScope.ServerAuthoritative);
                Jotunn.Managers.SynchronizationManager.OnConfigurationSynchronized += OnSynchronized;
                Refresh(force: true);
            }
            catch { Shutdown(); throw; }
        }

        public KeyboardShortcut ToggleKey
        {
            get
            {
                var held = Lock(MagpieSettings.PickupToggleKey);
                if (held.Locked) { try { return KeyboardShortcut.Deserialize(held.ServerText!); } catch { } }
                return Entry<KeyboardShortcut>(MagpieSettings.PickupToggleKey).Value;
            }
        }

        internal ConfigEntryBase Entry(SettingSpec spec) => entries[spec];

        public ConfigEntry<T> Entry<T>(SettingSpec spec) => (ConfigEntry<T>)entries[spec];

        /// <summary>The value to act on: through the authority gate, then clamped to its range.</summary>
        public T Get<T>(SettingSpec spec)
        {
            var entry = Entry<T>(spec);
            if (spec.Scope == SettingScope.Local)
            {
                var held = Lock(spec);
                if (held.Locked)
                {
                    try { return spec.Clamp((T)TomlTypeConverter.ConvertToValue(held.ServerText!, typeof(T))); }
                    catch { }
                }
                return spec.Clamp(entry.Value);
            }
            return spec.Clamp(MeadworksConfig.Effective(entry, Plugin.Guid, Map(spec.Scope), (T)spec.Default));
        }

        public bool Flag(SettingSpec spec) => Get<bool>(spec);

        /// <summary>A comma list setting, parsed once per distinct text.</summary>
        public NameList List(SettingSpec spec)
        {
            var text = Get<string>(spec) ?? "";
            if (lists.TryGetValue(spec, out var cached) && cached.Text == text) return cached.List;
            var list = NameList.Parse(text);
            lists[spec] = (text, list);
            return list;
        }

        /// <summary>
        /// Loot multiplier to apply now. A configured cheat the gate blocks reads 1, and the player is told once per session
        /// why (C03 blocked-option notice, center message or the log on a headless server).
        /// </summary>
        public int LootMultiplier()
        {
            var entry = Entry<int>(MagpieSettings.LootMultiplier);
            var configured = MagpieSettings.LootMultiplier.Clamp(entry.Value);
            if (configured <= 1) return 1;
            var allowed = MeadworksBlockedOptions.TryUse(Plugin.Guid, entry.Definition.Key, ConfigScope.Cheat, MagpieText.Text(Magpie.Text.MagpieTextKeys.LootMultiplier));
            return LootPolicy.EffectiveMultiplier(Get<int>(MagpieSettings.LootMultiplier), allowed);
        }

        public static ConfigScope Map(SettingScope scope)
        {
            switch (scope)
            {
                case SettingScope.Local: return ConfigScope.Local;
                case SettingScope.ServerAuthoritative: return ConfigScope.ServerAuthoritative;
                case SettingScope.Cheat: return ConfigScope.Cheat;
                default: throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown setting scope.");
            }
        }

        private ConfigEntryBase Bind(ConfigFile file, SettingSpec spec)
        {
            if (spec.Shortcut)
                return MeadworksConfig.Bind(file, spec.Section, spec.Key, KeyboardShortcut.Deserialize((string)spec.Default), new ConfigDescription(spec.Description, null, Dependencies(spec)), Map(spec.Scope));
            switch (spec.Default)
            {
                case bool b: return Bind(file, spec, b, null);
                case int i: return Bind(file, spec, i, new AcceptableValueRange<int>((int)spec.Minimum!, (int)spec.Maximum!));
                case float f: return Bind(file, spec, f, new AcceptableValueRange<float>((float)spec.Minimum!, (float)spec.Maximum!));
                case string s: return Bind(file, spec, s, null);
                case LootSources l: return Bind(file, spec, l, null);
                default: throw new NotSupportedException(spec.Id + ": unsupported value type " + spec.ValueType.Name + ".");
            }
        }

        private ConfigEntry<T> Bind<T>(ConfigFile file, SettingSpec spec, T value, AcceptableValueBase? acceptable) =>
            spec.Scope == SettingScope.Cheat
                ? MeadworksConfig.BindCheat(file, spec.Section, spec.Key, value, spec.Description, acceptable)
                : MeadworksConfig.Bind(file, spec.Section, spec.Key, value, new ConfigDescription(spec.Description, acceptable, Dependencies(spec)), Map(spec.Scope));

        private object[] Dependencies(SettingSpec spec)
        {
            SettingSpec? parent = null;
            bool grouped = ReferenceEquals(spec, MagpieSettings.GroupPickupMessages);
            if (grouped || ReferenceEquals(spec, MagpieSettings.AutoPickupAtStart) || ReferenceEquals(spec, MagpieSettings.PickupRange)
                || ReferenceEquals(spec, MagpieSettings.MagnetSpeed) || ReferenceEquals(spec, MagpieSettings.ScanInterval)
                || ReferenceEquals(spec, MagpieSettings.MaxPickupsPerScan) || ReferenceEquals(spec, MagpieSettings.IgnoreItems)
                || ReferenceEquals(spec, MagpieSettings.MaxPickupRange) || ReferenceEquals(spec, MagpieSettings.ShowPickupMessages)
                || ReferenceEquals(spec, MagpieSettings.SkipPickupEffects) || ReferenceEquals(spec, MagpieSettings.PickupToggleKey)
                || ReferenceEquals(spec, MagpieSettings.PickupTogglePadChord)) parent = MagpieSettings.PickupEnabled;
            else if (ReferenceEquals(spec, MagpieSettings.MergeRadius) || ReferenceEquals(spec, MagpieSettings.MergeMinCount)
                || ReferenceEquals(spec, MagpieSettings.MaxMergesPerScan) || ReferenceEquals(spec, MagpieSettings.MergeScanInterval)
                || ReferenceEquals(spec, MagpieSettings.MergeSettleTime) || ReferenceEquals(spec, MagpieSettings.MergeIgnoreItems)) parent = MagpieSettings.MergeEnabled;
            else if (ReferenceEquals(spec, MagpieSettings.ExcludedItems)) parent = MagpieSettings.FloatEnabled;
            if (parent == null) return Array.Empty<object>();
            return new object[] { new ConfigDependency(() => Flag(MagpieSettings.Enabled) && Flag(parent)
                && (!grouped || Flag(MagpieSettings.ShowPickupMessages)),
                () => "Requires " + MagpieSettings.Enabled.Section + ": Enabled and " + parent.Key
                    + (grouped ? " and " + MagpieSettings.ShowPickupMessages.Key : "") + ".") };
        }
    }
}
