// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Thin native config boundary; Grindstone perk/experience algorithms remain in the adopted files.
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    public sealed class CookingSetting<T>
    {
        public ConfigEntry<T> Entry { get; }
        public ConfigScope Scope { get; }
        readonly T safe;
        internal CookingSetting(ConfigEntry<T> entry, ConfigScope scope, T safe)
        { Entry = entry; Scope = scope; this.safe = safe; }
        public T Value => MeadworksConfig.Effective(Entry, Plugin.Guid, Scope, safe);
    }

    public static class CookingSettings
    {
        static readonly List<ConfigEntryBase> entries = new List<ConfigEntryBase>();
        public static IReadOnlyList<ConfigEntryBase> Entries => entries.AsReadOnly();
        public static CookingSetting<T> Bind<T>(ConfigFile config, string section, string key, T value,
            string description, AcceptableValueBase acceptableValues = null, ConfigScope scope = ConfigScope.ServerAuthoritative)
            => Bind(config, section, key, value, description, acceptableValues, scope, new object[0]);

        public static CookingSetting<T> Bind<T>(ConfigFile config, string section, string key, T value,
            string description, AcceptableValueBase acceptableValues, ConfigScope scope, params object[] tags)
        {
            var entry = MeadworksConfig.Bind(config, section, key, value, new ConfigDescription(description, acceptableValues, tags), scope);
            MeadworksBlockedOptions.Track(Plugin.Guid, entry, scope);
            entries.Add(entry);
            return new CookingSetting<T>(entry, scope, value);
        }
        public static void Initialize(ConfigFile config)
        {
            Shutdown();
            KitchenSettings.Initialize(config);
            ExperienceSettings.Initialize(config);
            DisplaySettings.Initialize(config);
        }
        public static void Shutdown()
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
