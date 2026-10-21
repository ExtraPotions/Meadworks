// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; adapts the Timber.Plugin PlayerSettingsLocks and TimberSyncReceipt pattern (expDARE): a hidden
// ServerAuthoritative carrier, refreshed on SettingChanged and Jotunn sync, never writing the player's own values.
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Threshold.Config;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Threshold.Runtime
{
    /// <summary>
    /// Locked Player Settings: the server names Local settings whose runtime values follow the server's own value on
    /// connected clients. The client's saved values stay untouched and apply again on disconnect.
    /// </summary>
    public sealed class ThresholdPlayerLocks : IDisposable
    {
        private const string Product = Plugin.Guid;
        private const int MaxText = 1024 * 1024;

        private readonly ConfigFile file;
        private readonly MeadworksLog log;
        private readonly ConfigEntry<string> listEntry, carrier;
        private readonly Dictionary<string, ConfigEntryBase> locals;
        private readonly HashSet<ConfigEntryBase> tracked = new HashSet<ConfigEntryBase>();
        private readonly SyncReceipt receipt = new SyncReceipt();
        private IReadOnlyList<string> locks = Array.Empty<string>();
        private IReadOnlyDictionary<string, string> values = new Dictionary<string, string>();
        private string listText = "", valuesText = "";
        private float next;
        private bool disposed, refreshing, dirty = true;

        public ThresholdPlayerLocks(ConfigFile file, ConfigEntry<string> listEntry, Dictionary<string, ConfigEntryBase> locals, MeadworksLog log)
        {
            this.file = file;
            this.listEntry = listEntry;
            this.locals = locals;
            this.log = log;
            carrier = MeadworksConfig.BindHidden(file, ThresholdSettings.General.Section, "Locked Player Values", "",
                "Managed serialized server values for Locked Player Settings; do not edit.", ConfigScope.ServerAuthoritative);
            file.SettingChanged += OnChanged;
            SynchronizationManager.OnConfigurationSynchronized += OnSynchronized;
            Refresh();
        }

        private void OnChanged(object sender, SettingChangedEventArgs args) => dirty = true;

        private void OnSynchronized(object sender, ConfigurationSynchronizationEventArgs args)
        {
            if (args.UpdatedPluginGUIDs == null || !args.UpdatedPluginGUIDs.Contains(Product) || !CruciblePlugin.IsReady) return;
            receipt.Observe();
            dirty = true;
            Refresh();
        }

        private bool ServerBinds() => CruciblePlugin.IsReady && CruciblePlugin.Session.Role == SessionRole.RemoteClient
            && receipt.CurrentGeneration == CruciblePlugin.Session.Generation
            && MeadworksAuthority.Decide(Product, ConfigScope.ServerAuthoritative).Source == ConfigValueSource.Server;

        public void Tick()
        {
            if (disposed) return;
            float now = Time.unscaledTime;
            if (!dirty && now < next) return;
            next = now + 0.5f;
            Refresh();
        }

        private void Refresh()
        {
            if (disposed || refreshing) return;
            refreshing = true;
            try
            {
                dirty = false;
                if (CruciblePlugin.IsReady && CruciblePlugin.Session.Role != SessionRole.RemoteClient)
                {
                    string text = LockedPlayerValues.Compose(listEntry.Value, id => locals.TryGetValue(id, out var entry) ? entry.GetSerializedValue() : null);
                    if (text != carrier.Value) MeadworksConfig.Amend(carrier, Product, ConfigScope.ServerAuthoritative, text);
                }
                string list = MeadworksConfig.Effective(listEntry, Product, ConfigScope.ServerAuthoritative, "");
                if (list != listText)
                {
                    listText = list;
                    locks = list.Length <= MaxText ? ThresholdSettings.ParseLocked(list) : Array.Empty<string>();
                    int named = ThresholdSettings.NameList(list).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    if (named > locks.Count) log.Warning("Locked Player Settings: " + (named - locks.Count) + " unknown or non-player entries ignored.");
                }
                string carried = MeadworksConfig.Effective(carrier, Product, ConfigScope.ServerAuthoritative, "");
                if (carried != valuesText)
                {
                    valuesText = carried;
                    values = carried.Length <= MaxText ? LockedPlayerValues.Decode(carried) : new Dictionary<string, string>();
                }
                bool binds = ServerBinds();
                foreach (var pair in locals)
                {
                    bool locked = LockedPlayerValues.ServerText(pair.Key, binds, locks, values) != null;
                    if (locked && tracked.Add(pair.Value))
                    {
                        MeadworksBlockedOptions.Untrack(pair.Value);
                        MeadworksBlockedOptions.Track(Product, pair.Value, ConfigScope.ServerAuthoritative);
                    }
                    else if (!locked && tracked.Remove(pair.Value)) MeadworksBlockedOptions.Untrack(pair.Value);
                }
            }
            catch (Exception error) { log.Warning("Player setting lock refresh: " + error.GetType().Name + ": " + error.Message); }
            finally { refreshing = false; }
        }

        /// <summary>The server's value while <paramref name="entry"/> is locked, otherwise <paramref name="own"/>.</summary>
        public T Effective<T>(ConfigEntry<T> entry, T own)
        {
            if (disposed) return own;
            if (dirty) Refresh();
            string? text = LockedPlayerValues.ServerText(entry.Definition.Section + "." + entry.Definition.Key, ServerBinds(), locks, values);
            if (text == null) return own;
            try
            {
                object parsed = TomlTypeConverter.ConvertToValue(text, entry.SettingType);
                if (entry.Description.AcceptableValues != null && !entry.Description.AcceptableValues.IsValid(parsed)) return (T)entry.DefaultValue;
                return (T)parsed;
            }
            catch (Exception) { return (T)entry.DefaultValue; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            file.SettingChanged -= OnChanged;
            SynchronizationManager.OnConfigurationSynchronized -= OnSynchronized;
            foreach (var entry in tracked) MeadworksBlockedOptions.Untrack(entry);
            tracked.Clear();
        }

        // Jotunn's receipt precedes Crucible's authenticated epoch; it binds to that exact connection and epoch once.
        private sealed class SyncReceipt
        {
            private ZNet? network;
            private ZRpc? server;
            private string epoch = "";
            private long world;

            internal void Observe()
            {
                network = ZNet.instance;
                server = network != null && !network.IsServer() ? network.GetServerRPC() : null;
                epoch = "";
                world = 0;
                if (server != null && server.IsConnected() && CruciblePlugin.IsReady)
                {
                    var session = CruciblePlugin.Session;
                    if (session.Role == SessionRole.RemoteClient && !string.IsNullOrEmpty(session.SessionId))
                    {
                        epoch = session.SessionId;
                        world = session.WorldId;
                    }
                }
            }

            internal long CurrentGeneration
            {
                get
                {
                    if (!CruciblePlugin.IsReady || network == null || server == null || !server.IsConnected()
                        || !ReferenceEquals(network, ZNet.instance) || network.IsServer()
                        || !ReferenceEquals(server, network.GetServerRPC())) return -1;
                    var session = CruciblePlugin.Session;
                    if (session.Role != SessionRole.RemoteClient || !session.AuthorityReady
                        || session.WorldId == 0 || string.IsNullOrEmpty(session.SessionId)) return -1;
                    if (epoch.Length == 0)
                    {
                        epoch = session.SessionId;
                        world = session.WorldId;
                    }
                    return epoch == session.SessionId && world == session.WorldId ? session.Generation : -1;
                }
            }
        }
    }
}
