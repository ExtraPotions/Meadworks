// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Reuses Meadworks' own Ember/Kindle lock-list, hidden carrier and authenticated receipt pattern.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Extensions;
using Jotunn.Managers;
using Authority = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Magpie.Runtime.Settings
{
    internal sealed partial class MagpieConfig
    {
        internal const string LockedValuesKey = "Locked Player Values";
        readonly ConfigFile file;
        readonly MeadworksLog log;
        readonly ConfigEntry<string> lockedValues;
        readonly MagpieSyncReceipt receipt = new MagpieSyncReceipt();
        readonly HashSet<SettingSpec> trackedLocks = new HashSet<SettingSpec>();
        LockList lockList = LockList.Empty;
        IReadOnlyDictionary<string, string> values = new Dictionary<string, string>();
        string? lockText, valuesText;
        float nextRefresh;
        bool stopped;

        internal bool StartReceiptReady => Authority.IsReady &&
            (Authority.Session.Role != SessionRole.RemoteClient || ServerBinds());
        internal bool IsLocked(SettingSpec spec) => spec.Lockable && Lock(spec).Locked;
        PlayerLock Lock(SettingSpec spec) => PlayerLocks.Evaluate(spec.Id, ServerBinds(), lockList, values);
        bool ServerBinds() => Authority.IsReady && Authority.Session.Role == SessionRole.RemoteClient &&
            receipt.CurrentGeneration == Authority.Session.Generation &&
            MeadworksAuthority.Decide(Plugin.Guid, ConfigScope.ServerAuthoritative).Source == ConfigValueSource.Server;
        internal void Tick(float now)
        {
            if (stopped || now < nextRefresh) return;
            nextRefresh = now + .5f; Refresh(force: false);
        }
        void Refresh(bool force)
        {
            if (stopped) return;
            try
            {
                if (Authority.IsReady && Authority.Session.Role != SessionRole.RemoteClient)
                {
                    var list = LockList.Parse(Entry<string>(MagpieSettings.LockedPlayerSettings).Value);
                    string payload = LockedValues.Compose(list, id => MagpieSettings.Find(id) is SettingSpec spec ? Entry(spec).GetSerializedValue() : null);
                    if (payload != lockedValues.Value)
                        MeadworksConfig.Amend(lockedValues, Plugin.Guid, ConfigScope.ServerAuthoritative, payload);
                }
                string text = Get<string>(MagpieSettings.LockedPlayerSettings);
                if (force || text != lockText)
                {
                    lockText = text; lockList = LockList.Parse(text);
                    if (lockList.Unknown.Count != 0 || lockList.NotLockable.Count != 0)
                        log.Warning("Locked Player Settings: " + lockList.Describe());
                }
                string carried = MeadworksConfig.Effective(lockedValues, Plugin.Guid, ConfigScope.ServerAuthoritative, "");
                if (force || carried != valuesText) { valuesText = carried; values = LockedValues.Decode(carried); }
                foreach (var spec in MagpieSettings.All.Where(setting => setting.Lockable))
                {
                    if (IsLocked(spec))
                    {
                        if (trackedLocks.Add(spec)) MeadworksBlockedOptions.Track(Plugin.Guid, Entry(spec), ConfigScope.ServerAuthoritative);
                    }
                    else if (trackedLocks.Remove(spec)) MeadworksBlockedOptions.Untrack(Entry(spec));
                }
            }
            catch (Exception error) { log.Warning("Configuration refresh failed: " + error.GetType().Name + ": " + error.Message); }
        }
        void OnSynchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs args)
        {
            if (stopped || args.UpdatedPluginGUIDs == null || !args.UpdatedPluginGUIDs.Contains(Plugin.Guid) || !Authority.IsReady) return;
            receipt.Observe(); Refresh(force: true);
        }
        internal void Shutdown()
        {
            if (stopped) return; stopped = true;
            SynchronizationManager.OnConfigurationSynchronized -= OnSynchronized;
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            trackedLocks.Clear(); lockList = LockList.Empty; values = new Dictionary<string, string>();
        }
    }
}
