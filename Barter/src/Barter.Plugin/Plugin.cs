// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Trader mechanics adopted from TradersExtended 2.0.5 (shudnal, Unlicense, pin 2d5dd879ead70f809f53025d6b28feb3dee7b605); see SOURCE_NOTICE.md.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using HarmonyLib;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>
    /// Barter (Hoard): traders you can shape. Per-trader buy and sell lists (YAML, JSON or CSV, synchronized from the server and hot
    /// reloaded), custom currencies, quality and key conditions, a purse that moves prices, and a sell list on the Crucible.UI kit.
    /// Required on all peers (roster peer classes): trader lists and currencies change shared rules, so the server's lists and settings
    /// win and a server without Barter turns it off for the session.
    /// Registers with Crucible, then adopts the trader hooks after all plugins load. A headless process never builds UI but keeps the
    /// list synchronization and the morning purse refill. TradersExtended, when installed, keeps the traders and Barter steps aside.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    // BepInEx minimums for the published 8.0.1 kit. A later 0.3.x still loads.
    [BepInDependency(CruciblePlugin.Guid, "0.3.38")]
    [BepInDependency(CrucibleUiPlugin.Guid, "0.3.20")]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = BarterCore.ProductId;
        public const string Name = "Barter";
        public const string Version = "0.1.9";
        public const string Family = "Hoard";

        internal const string SourceModGuid = "shudnal.TradersExtended";

        private ProductRegistration registration;
        private Harmony serverPatches, clientPatches;
        private bool started;
        private long noticedSession = -1;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        public static bool SteppedAside { get; private set; }

        /// <summary>Barter acts now: registered, no source mod owns the traders, and Crucible says the product is on for this session.</summary>
        private static bool SessionEnabled => Ready && !SteppedAside && MeadworksAuthority.ProductActive(Guid, out _);
        public static bool Active => SessionEnabled && TradeLists.Ready && TraderPolicies.Ready;
        internal static bool AwaitingRules => SessionEnabled && (!TradeLists.Ready || !TraderPolicies.Ready);

        /// <summary>This process may build Barter UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        internal static BarterConfig Settings { get; private set; }

        internal static MeadworksLog Log { get; private set; }

        private void Awake()
        {
            Log = MeadworksLog.For(Logger, Name);
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                CarryOverLegacySettings();
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.RequiredOnAllPeers, Family));
                AllowsUi = MeadworksHeadless.AllowsUi;
                Settings = BarterConfig.Bind(Config, Log);
                BarterText.Initialize(Info.Metadata, Logger);
                Ready = true;
                Log.Info(AllowsUi ? "Barter registered; trader modules start after plugin discovery."
                    : "Barter registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                Shutdown();
                Log.Error("Barter initialization rejected: " + error);
            }
        }

        /// <summary>
        /// Barter's GUID was com.extrapotions.barter before 0.1.2, so BepInEx named its settings file after it. Before the settings
        /// bind, copy that file to the new name (never over an existing one), keep the old one as a .bak and reload.
        /// </summary>
        private void CarryOverLegacySettings()
        {
            string directory = System.IO.Path.GetDirectoryName(Config.ConfigFilePath) ?? BepInEx.Paths.ConfigPath;
            string legacyName = BarterCore.LegacyProductId + ".cfg";
            var result = LegacyFileMigration.PrepareSettings(directory, legacyName, System.IO.Path.GetFileName(Config.ConfigFilePath));
            switch (result.Outcome)
            {
                case LegacyFileOutcome.Migrated:
                    Config.Reload();
                    Log.Info("Settings carried over from " + legacyName + (result.Backup != null
                        ? "; the old file is kept as " + System.IO.Path.GetFileName(result.Backup) + "."
                        : ". " + result.Problem));
                    break;
                case LegacyFileOutcome.KeptNew:
                    Log.Info(legacyName + " was left untouched because " + System.IO.Path.GetFileName(Config.ConfigFilePath) + " already exists.");
                    break;
            }
        }

        private void Start()
        {
            if (!Ready || Settings == null || registration == null || Log == null) return;
            try
            {
                SteppedAside = DeclareAndYield();
                if (!SteppedAside)
                {
                    TradeLists.Initialize(Config, Log);
                    TraderPolicies.Initialize(Config);
                    OptionalTradeIntegrations.Initialize();
                    TradeAuthority.Initialize(registration);
                    ListEditor.Initialize(registration);
                    TradeLists.Changed += RulesChanged;
                    TraderPolicies.Changed += RulesChanged;
                    Settings.Changed += SettingsChanged;
                    serverPatches = new Harmony(Guid + ".server");
                    serverPatches.CreateClassProcessor(typeof(EnvMan_UpdateTriggers_Refill)).Patch();
                    foreach (Type patch in new[] { typeof(ZDO_Deserialize_BarterTrader), typeof(ZNetScene_RPC_SpawnObject_BarterTrader), typeof(ZNet_ServerLoadWorld_BarterTraders), typeof(ZNetView_Awake_BarterTrader),
                        typeof(ZDO_SetOwner_BarterTrader), typeof(ZDO_SetOwnerInternal_BarterTrader) })
                        serverPatches.CreateClassProcessor(patch).Patch();
                    if (AllowsUi)
                    {
                        clientPatches = new Harmony(Guid + ".client");
                        foreach (Type patch in ClientPatches)
                            clientPatches.CreateClassProcessor(patch).Patch();
                    }
                }
                started = true;
            }
            catch (Exception error)
            {
                Log.Error("Barter modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private static readonly Type[] ClientPatches =
        {
            typeof(Trader_GetAvailableItems_Lists), typeof(StoreGui_Show_Barter),
            typeof(StoreGui_Hide_Barter), typeof(StoreGui_Update_Barter), typeof(StoreGui_FillList_Barter),
            typeof(StoreGui_CanAfford_Barter), typeof(StoreGui_BuySelectedItem_Barter), typeof(StoreGui_GetSelectedItemIndex_Barter),
            typeof(StoreGui_UpdateSellButton_Barter), typeof(StoreGui_SellItem_Barter), typeof(Inventory_Changed_Barter),
            typeof(PriceTips), typeof(Player_Load_BarterRecovery),
        };

        private void Update()
        {
            if (!Ready) return;
            BarterText.Tick();
            if (!started) return;
            if (SteppedAside)
            {
                NoticeStepAside();
                return;
            }
            TradeLists.Tick();
            TraderPolicies.Tick();
            TradeAuthority.Tick();
            ListEditor.Tick();
            if (AllowsUi) ListEditorWindow.HandleInput();
        }

        private static void SettingsChanged(object sender, EventArgs args) => RulesChanged();

        private static void RulesChanged()
        {
            TradeOffers.Reset();
            if (AllowsUi) StoreUi.RulesChanged();
        }

        private void NoticeStepAside()
        {
            if (!AllowsUi || MessageHud.instance == null || noticedSession == CruciblePlugin.Session.Generation) return;
            noticedSession = CruciblePlugin.Session.Generation;
            var sources = MeadworksStepAside.Decide(Guid).Present;
            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                BarterText.Format("step_aside", string.Join(", ", sources.Select(source => source.Name))));
        }

        /// <summary>After all plugins load: TradersExtended, identified by its GUID, owns the traders; Barter stays off and says so once.</summary>
        private static bool DeclareAndYield()
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Guid).Any())
            {
                var sources = new List<SourceMod>();
                if (Chainloader.PluginInfos.TryGetValue(SourceModGuid, out var tradersExtended))
                    sources.Add(new SourceMod(tradersExtended.Metadata.GUID, "Traders Extended"));
                MeadworksStepAside.Declare(Guid, Name, sources);
            }
            return MeadworksStepAside.ShouldYield(Guid);
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void Shutdown()
        {
            Ready = false;
            started = false;
            noticedSession = -1;
            try { clientPatches?.UnpatchSelf(); } catch (Exception error) { Log?.Warning("Barter teardown (client patches): " + error.GetType().Name); }
            try { serverPatches?.UnpatchSelf(); } catch (Exception error) { Log?.Warning("Barter teardown (server patches): " + error.GetType().Name); }
            clientPatches = serverPatches = null;
            if (Settings != null) Settings.Changed -= SettingsChanged;
            TradeLists.Changed -= RulesChanged;
            TraderPolicies.Changed -= RulesChanged;
            foreach (Action stop in new Action[] { TradeAuthority.Shutdown, BuyService.ResetPending, SellService.ResetPending, TraderServiceExchange.ResetPending,
                StoreUi.Dispose, ListEditor.Shutdown, OptionalTradeIntegrations.Shutdown, TraderPolicies.Shutdown,
                TradeLists.Shutdown, TradeOffers.Reset, BarterText.Shutdown })
                try { stop(); } catch (Exception error) { Log?.Warning("Barter teardown: " + error.GetType().Name); }
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
            SteppedAside = false;
        }
    }
}
