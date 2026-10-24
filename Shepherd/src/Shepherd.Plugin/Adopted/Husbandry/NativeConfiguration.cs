// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>Native reference values read through Crucible's synchronized authority gate.</summary>
    public sealed class HusbandrySetting<T>
    {
        public ConfigEntry<T> Entry { get; }
        public ConfigScope Scope { get; }
        readonly T safe;
        internal HusbandrySetting(ConfigEntry<T> entry, ConfigScope scope, T safe)
        { Entry = entry; Scope = scope; this.safe = safe; }
        public T Value => Scope == ConfigScope.Cheat
            ? MeadworksMischief.Effective(Entry, Plugin.Guid, MischiefCategory.World, safe)
            : MeadworksConfig.Effective(Entry, Plugin.Guid, Scope, safe);
    }

    public sealed class NativeConfiguration
    {
        readonly ConfigFile file;
        readonly List<ConfigEntryBase> entries = new List<ConfigEntryBase>();
        public IReadOnlyList<ConfigEntryBase> Entries => entries.AsReadOnly();
        public NativeConfiguration(ConfigFile file) { this.file = file; }
        public HusbandrySetting<T> Bind<T>(string section, string key, T value, string description,
            bool synced = true, AcceptableValueBase? acceptableValues = null)
            => Bind(section, key, value, description, synced, acceptableValues, new object[0]);

        public HusbandrySetting<T> Bind<T>(string section, string key, T value, string description,
            bool synced, AcceptableValueBase? acceptableValues, params object[] tags)
        {
            var scope = synced ? ConfigScope.ServerAuthoritative : ConfigScope.Local;
            var entry = MeadworksConfig.Bind(file, section, key, value, new ConfigDescription(description, acceptableValues, tags), scope);
            MeadworksBlockedOptions.Track(Plugin.Guid, entry, scope);
            entries.Add(entry);
            return new HusbandrySetting<T>(entry, scope, value);
        }
        public HusbandrySetting<T> BindHidden<T>(string section, string key, T value, string description,
            bool synced = true, AcceptableValueBase? acceptableValues = null)
        {
            var scope = synced ? ConfigScope.ServerAuthoritative : ConfigScope.Local;
            var entry = MeadworksConfig.BindHidden(file, section, key, value, description, scope, acceptableValues);
            MeadworksBlockedOptions.Track(Plugin.Guid, entry, scope);
            entries.Add(entry);
            return new HusbandrySetting<T>(entry, scope, value);
        }
        public HusbandrySetting<bool> Cheat(string key, string description)
        {
            var entry = MeadworksConfig.BindCheat(file, "12 - Cheats", key, false, description, MischiefCategory.World);
            MeadworksBlockedOptions.Track(Plugin.Guid, entry, ConfigScope.Cheat);
            entries.Add(entry);
            return new HusbandrySetting<bool>(entry, ConfigScope.Cheat, false);
        }
        public void Dispose()
        {
            foreach (var entry in entries) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
        }
    }

    public static class Settings
    {
        public static AcceptableValueRange<float> UpTo(float max) => new AcceptableValueRange<float>(0f, max);
    }
}
