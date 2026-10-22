// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Barter.Config;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Extensions;
using Jotunn.Managers;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Barter.Runtime
{
    internal static class TraderPolicies
    {
        internal const string FileName = "Barter.Traders.json";
        private const string Empty = "{\"version\":1,\"traders\":{}}";
        private static ConfigEntry<string> carrier;
        private static SyncReceipt receipt = new SyncReceipt();
        private static TraderPolicyBook current = TraderPolicyBook.Empty;
        private static long generation = -1;
        private static float nextCheck;
        private static string applied;
        private static bool initialized;
        internal static bool Ready { get; private set; }
        internal static event Action Changed;

        internal static void Initialize(ConfigFile config)
        {
            bool save = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                carrier = config.BindConfig("Trade Lists", "Managed Trader Policies", "",
                    "Managed by Barter: server trader policies. Edit Barter.Traders.json on the host.", synced: true,
                    configAttributes: new ConfigurationManagerAttributes { Browsable = false, ReadOnly = true });
            }
            finally { config.SaveOnConfigSet = save; }
            config.Save();
            SynchronizationManager.OnConfigurationSynchronized += Synchronized;
            initialized = true;
            Tick();
        }

        private static void Synchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs args)
        {
            if (CruciblePlugin.IsReady && args.UpdatedPluginGUIDs != null && args.UpdatedPluginGUIDs.Contains(Plugin.Guid))
            { receipt.Observe(); nextCheck = 0; }
        }

        internal static T Get<T>(BarterSetting<T> setting, string trader, T fallback) => current.Get(trader, setting, fallback);
        internal static string Currency(string trader, string fallback) => current.Currency(trader, fallback);
        internal static bool HasTrader(string trader) => current.Traders.ContainsKey(ExtraPotions.Barter.Lists.TraderNames.Normalize(trader));

        internal static void Tick()
        {
            if (!initialized || !CruciblePlugin.IsReady) return;
            if (generation != CruciblePlugin.Session.Generation)
            {
                generation = CruciblePlugin.Session.Generation;
                applied = null;
                current = TraderPolicyBook.Empty;
                Ready = false;
                nextCheck = 0;
            }
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2f;
            try
            {
                bool remote = CruciblePlugin.Session.Role == SessionRole.RemoteClient;
                var decision = MeadworksAuthority.Decide(Plugin.Guid, ConfigScope.ServerAuthoritative);
                if (decision.Source == ConfigValueSource.Locked || (remote &&
                    (decision.Source != ConfigValueSource.Server || receipt.CurrentGeneration != generation)))
                { current = TraderPolicyBook.Empty; Ready = false; return; }
                string text;
                if (remote) text = MeadworksConfig.Effective(carrier, Plugin.Guid, ConfigScope.ServerAuthoritative, "");
                else
                {
                    string path = Path.Combine(Paths.ConfigPath, FileName);
                    if (!File.Exists(path)) text = Empty;
                    else
                    {
                        if (new FileInfo(path).Length > TraderPolicyJson.MaximumBytes) throw new InvalidDataException("Trader policies exceed 256 KiB.");
                        text = new System.Text.UTF8Encoding(false, true).GetString(File.ReadAllBytes(path)).TrimStart('\uFEFF');
                    }
                }
                if (text == applied && Ready) return;
                TraderPolicyBook next = TraderPolicyJson.Read(text);
                current = next;
                applied = text;
                Ready = true;
                if (!remote && carrier.Value != text) MeadworksConfig.Amend(carrier, Plugin.Guid, ConfigScope.ServerAuthoritative, text);
                Changed?.Invoke();
            }
            catch (Exception error)
            {
                // A bad replacement keeps the last validated policy for this session.
                Plugin.Log?.Warning("Trader policy reload refused: " + error.GetType().Name + ": " + error.Message);
            }
        }

        internal static void Shutdown()
        {
            SynchronizationManager.OnConfigurationSynchronized -= Synchronized;
            initialized = false;
            carrier = null;
            current = TraderPolicyBook.Empty;
            applied = null;
            Ready = false;
            generation = -1;
            receipt = new SyncReceipt();
            Changed = null;
        }
    }
}
