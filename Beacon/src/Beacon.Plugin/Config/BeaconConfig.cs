// Beacon - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Thing.Plugin and Tempest.Plugin (expDARE), without cheats.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Beacon.Config;
using ExtraPotions.Beacon.Joining;
using ExtraPotions.Beacon.Secrets;
using ExtraPotions.Beacon.Servers;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Managers;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Beacon.Runtime
{
    /// <summary>
    /// Beacon's configuration surface (BC04a, design section 5). Every setting of <see cref="BeaconSettings.All"/> is bound
    /// through Crucible C01; personal settings stay Local and the server allow-reconnect policy is authoritative.
    /// The panel key binds as a BepInEx <see cref="KeyboardShortcut"/>. Beacon has no cheats.
    /// Values are read at use time. Main thread only.
    /// </summary>
    public sealed class BeaconConfig
    {
        private readonly ConfigFile file;
        private readonly Dictionary<BeaconSetting, ConfigEntryBase> entries = new Dictionary<BeaconSetting, ConfigEntryBase>();
        readonly BeaconSyncReceipt receipt = new BeaconSyncReceipt();
        long policyGeneration = -1;
        internal ReconnectServerPolicy ReconnectPolicy { get; } = new ReconnectServerPolicy();

        private BeaconConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in BeaconSettings.All) BindSetting(setting);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
            SynchronizationManager.OnConfigurationSynchronized += OnSynchronized;
            MeadworksBlockedOptions.Track(Plugin.Guid, Entry(BeaconSettings.Server.AllowReconnect), ConfigScope.ServerAuthoritative);
        }

        public ConfigFile File => file;

        public static BeaconConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new BeaconConfig(file);
            log.Verbose("Configuration bound: " + (BeaconSettings.All.Count - 1) + " Local settings, one server policy, no cheats.");
            return config;
        }

        public ConfigEntryBase Entry(BeaconSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Beacon setting.", nameof(setting));

        public T Get<T>(BeaconSetting<T> setting)
        {
            if (setting.Hotkey) throw new ArgumentException(setting.Id + " is a keyboard shortcut; use Shortcut.", nameof(setting));
            var entry = (ConfigEntry<T>)Entry(setting);
            return setting.Normalize(setting.Scope == SettingScope.Local ? entry.Value
                : MeadworksConfig.Effective(entry, Plugin.Guid, ConfigScope.ServerAuthoritative, setting.Default));
        }

        public ConfigEntry<KeyboardShortcut> Shortcut(BeaconSetting<string> setting)
        {
            if (!setting.Hotkey) throw new ArgumentException(setting.Id + " is not a keyboard shortcut.", nameof(setting));
            return (ConfigEntry<KeyboardShortcut>)Entry(setting);
        }

        internal void BeginReconnectConnection(ServerAddress? target)
        { receipt.Reset(); policyGeneration = -1; ReconnectPolicy.Begin(target); }

        internal void RefreshReconnectPolicy()
        {
            var target = ReconnectPolicy.Target;
            if (target == null || !CruciblePlugin.IsReady || ZNet.instance == null || ZNet.instance.IsServer()
                || ZNet.GetConnectionStatus() != ZNet.ConnectionStatus.Connected) return;
            var session = CruciblePlugin.Session;
            if (session.Role != SessionRole.RemoteClient) return;
            if (session.AuthorityReady)
            {
                if (policyGeneration >= 0 && policyGeneration != session.Generation) ReconnectPolicy.Begin(target);
                policyGeneration = session.Generation;
            }
            var product = MeadworksAuthority.ServerProductOf(Plugin.Guid);
            if (MeadworksAuthority.Server == ServerCrucible.Absent || (session.AuthorityReady && product == ServerProduct.Missing))
            { ReconnectPolicy.Observe(target, authenticated: true, allowsReconnect: true); return; }
            if (product != ServerProduct.Present || receipt.CurrentGeneration != session.Generation
                || MeadworksAuthority.Decide(Plugin.Guid, ConfigScope.ServerAuthoritative).Source != ConfigValueSource.Server) return;
            ReconnectPolicy.Observe(target, authenticated: true, allowsReconnect: Get(BeaconSettings.Server.AllowReconnect));
        }

        void OnSynchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs args)
        {
            if (args.UpdatedPluginGUIDs == null || !args.UpdatedPluginGUIDs.Contains(Plugin.Guid)) return;
            receipt.Observe(); RefreshReconnectPolicy();
        }

        public void Shutdown()
        {
            SynchronizationManager.OnConfigurationSynchronized -= OnSynchronized;
            MeadworksBlockedOptions.Untrack(Entry(BeaconSettings.Server.AllowReconnect));
            receipt.Reset(); policyGeneration = -1; ReconnectPolicy.Reset(); entries.Clear();
        }

        private void BindSetting(BeaconSetting setting)
        {
            ConfigEntryBase entry;
            if (setting.Hotkey)
            {
                var text = (string)setting.DefaultValue;
                entry = MeadworksConfig.Bind(file, setting.Section, setting.Key, text.Length == 0 ? KeyboardShortcut.Empty : KeyboardShortcut.Deserialize(text),
                    setting.Description, ConfigScope.Local);
            }
            else
            {
                switch (setting)
                {
                    case BeaconSetting<bool> b: entry = Bind(b, null); break;
                    case BeaconSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                    case BeaconSetting<string> s: entry = Bind(s, null); break;
                    case BeaconSetting<LinuxFallback> f: entry = Bind(f, null); break;
                    default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
                }
            }
            entries.Add(setting, entry);
        }

        private ConfigDescription Describe(BeaconSetting setting, AcceptableValueBase? acceptable, string? text = null)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, BeaconSettings.AutoJoin.Server) ||
                ReferenceEquals(setting, BeaconSettings.AutoJoin.Character) ||
                ReferenceEquals(setting, BeaconSettings.AutoJoin.CountdownSeconds))
                dependency = new ConfigDependency(() => Get(BeaconSettings.AutoJoin.Enabled), () => "Requires " + BeaconSettings.AutoJoin.Enabled.Section + ": " + BeaconSettings.AutoJoin.Enabled.Key + ".");
            if (ReferenceEquals(setting, BeaconSettings.Reconnect.Attempts))
                dependency = new ConfigDependency(() => Get(BeaconSettings.Reconnect.Enabled), () => "Requires " + BeaconSettings.Reconnect.Enabled.Section + ": " + BeaconSettings.Reconnect.Enabled.Key + ".");
            return new ConfigDescription(text ?? setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(BeaconSetting<T> setting, AcceptableValueBase? acceptable) =>
            MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable),
                setting.Scope == SettingScope.Local ? ConfigScope.Local : ConfigScope.ServerAuthoritative);
    }
}
