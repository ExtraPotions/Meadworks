// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Config;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Bestiary.Runtime.Settings
{
    /// <summary>
    /// Binds every row of <see cref="BestiarySettings"/> through Crucible (C01) in
    /// <c>BepInEx/config/com.meadworks.bestiary.cfg</c>. ServerAuthoritative rows are tracked so configuration managers
    /// show them read-only with the C03 reason while the gate blocks them. Values are read at use time through the gate
    /// (<see cref="MeadworksConfig.Effective{T}"/>) and clamped to their section 4 range. Biome lists bind as the game's
    /// <c>Heightmap.Biome</c> flags (same values as <see cref="BiomeMask"/>), so ECP's config text imports unchanged.
    /// </summary>
    internal sealed class BestiaryConfig
    {
        private readonly Dictionary<SettingSpec, ConfigEntryBase> entries = new Dictionary<SettingSpec, ConfigEntryBase>();

        public BestiaryConfig(ConfigFile file)
        {
            foreach (var spec in BestiarySettings.All)
                entries.Add(spec, Bind(file, spec));
            foreach (var pair in entries)
                if (pair.Key.Scope != SettingScope.Local)
                    MeadworksBlockedOptions.Track(Plugin.Guid, pair.Value, Map(pair.Key.Scope));
        }

        public ConfigEntryBase Raw(SettingSpec spec) => entries[spec];

        public ConfigEntry<T> Entry<T>(SettingSpec spec) => (ConfigEntry<T>)entries[spec];

        /// <summary>The value to act on: through the authority gate, then clamped to its range.</summary>
        public T Get<T>(SettingSpec spec)
        {
            var value = MeadworksConfig.Effective(Entry<T>(spec), Plugin.Guid, Map(spec.Scope), (T)spec.Default);
            return spec.Clamp(value);
        }

        public T Get<T>(string id) => Get<T>(BestiarySettings.Find(id) ?? throw new ArgumentException("Unknown Bestiary setting " + id, nameof(id)));

        public bool Flag(string id) => Get<bool>(id);

        /// <summary>A biome list setting as the portable mask.</summary>
        public BiomeMask Biomes(string id)
        {
            var spec = BestiarySettings.Find(id) ?? throw new ArgumentException("Unknown Bestiary setting " + id, nameof(id));
            var value = MeadworksConfig.Effective(Entry<Heightmap.Biome>(spec), Plugin.Guid, Map(spec.Scope), (Heightmap.Biome)(int)(BiomeMask)spec.Default);
            return (BiomeMask)(int)value;
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
            if (spec.Scope == SettingScope.Cheat)
                throw new NotSupportedException(spec.Id + ": Bestiary has no cheat settings (design 5).");
            switch (spec.Default)
            {
                case bool b: return Bind(file, spec, b, null);
                case int i: return Bind(file, spec, i, new AcceptableValueRange<int>((int)spec.Minimum!, (int)spec.Maximum!));
                case float f: return Bind(file, spec, f, new AcceptableValueRange<float>((float)spec.Minimum!, (float)spec.Maximum!));
                case string s: return Bind(file, spec, s, null);
                case BiomeMask m: return Bind(file, spec, (Heightmap.Biome)(int)m, null);
                default: throw new NotSupportedException(spec.Id + ": unsupported value type " + spec.ValueType.Name + ".");
            }
        }

        private ConfigEntry<T> Bind<T>(ConfigFile file, SettingSpec spec, T value, AcceptableValueBase? acceptable) =>
            MeadworksConfig.Bind(file, spec.Section, spec.Key, value, new ConfigDescription(spec.Description, acceptable, Dependencies(spec)), Map(spec.Scope));

        private object[] Dependencies(SettingSpec spec)
        {
            string? family = null;
            if (ReferenceEquals(spec, BestiarySettings.Find(BestiarySettings.Mimic + ".Chance"))
                || ReferenceEquals(spec, BestiarySettings.Find(BestiarySettings.Mimic + ".Chests"))) family = BestiarySettings.Mimic;
            else if (ReferenceEquals(spec, BestiarySettings.Find(BestiarySettings.Slinger + ".Share"))
                || ReferenceEquals(spec, BestiarySettings.Find(BestiarySettings.Slinger + ".Nests"))
                || ReferenceEquals(spec, BestiarySettings.Find(BestiarySettings.Slinger + ".Biomes"))) family = BestiarySettings.Slinger;
            if (family == null) return Array.Empty<object>();
            string parent = family + ".Enabled";
            return new object[] { new ConfigDependency(() => Flag(BestiarySettings.General + ".Enabled") && Flag(parent),
                () => "Requires " + BestiarySettings.General + ": Enabled and " + family + ": Enabled.") };
        }
    }
}
