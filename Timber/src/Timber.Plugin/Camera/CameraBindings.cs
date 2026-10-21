// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original thin C01/C03 adapter; camera algorithms remain in their adopted files.
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Timber.Runtime;
using Plugin = ExtraPotions.Timber.Runtime.Plugin;

namespace Timber.Camera
{
    public sealed class CameraValue<T>
    {
        readonly ConfigEntry<T> entry;
        readonly ConfigScope scope;
        readonly T safe;
        public CameraValue(ConfigEntry<T> entry, ConfigScope scope, T safe)
        { this.entry = entry; this.scope = scope; this.safe = safe; }
        public T Value { get { T own = MeadworksConfig.Effective(entry, Plugin.Guid, scope, safe);
            return scope == ConfigScope.Local && Plugin.PlayerLocks != null ? Plugin.PlayerLocks.Effective(entry, own) : own; } }
    }

    public sealed class CameraBindings
    {
        readonly ConfigFile config;
        readonly List<ConfigEntryBase> tracked = new List<ConfigEntryBase>();
        public CameraBindings(ConfigFile config) { this.config = config; }
        public CameraValue<T> Bind<T>(string section, string key, T value, string description,
            bool synced = true, AcceptableValueBase? acceptableValues = null)
            => Bind(section, key, value, description, synced, acceptableValues, new object[0]);

        public CameraValue<T> Bind<T>(string section, string key, T value, string description,
            bool synced, AcceptableValueBase? acceptableValues, params object[] tags)
        {
            var scope = synced ? ConfigScope.ServerAuthoritative : ConfigScope.Local;
            var entry = MeadworksConfig.Bind(config, section, key, value, new ConfigDescription(description, acceptableValues, tags), scope);
            MeadworksBlockedOptions.Track(Plugin.Guid, entry, scope);
            tracked.Add(entry);
            return new CameraValue<T>(entry, scope, value);
        }
        public void Dispose()
        {
            foreach (var entry in tracked) MeadworksBlockedOptions.Untrack(entry);
            tracked.Clear();
        }
    }
}
