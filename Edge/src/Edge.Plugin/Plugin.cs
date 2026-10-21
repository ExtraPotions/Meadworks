// Edge - ExtraPotions Meadworks combat: secondary attacks, quickstep, slope aim assist and weapons back in hand.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;
using ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Runtime.Quickstep;
using ExtraPotions.Edge.Runtime.Equipment;

namespace ExtraPotions.Edge.Runtime
{
    /// <summary>
    /// Edge (Fang). Required on all peers (design section 6): secondaries spawn projectiles, sentinels, area objects and
    /// summons every peer must simulate, and preset values must be the server's; major.minor must match. On a vanilla
    /// server or a Crucible server without Edge the product is off for the session (user decision Q1: swim re-equip and
    /// slope assist turn off too). Each module steps aside for its own source mod (section 10). A headless process
    /// (dedicated server, C10) never builds UI.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [BepInDependency("randyknapp.mods.epicloot", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = EdgeCore.PluginGuid;
        public const string Name = EdgeCore.ProductName;
        public const string Version = EdgeCore.ProductVersion;
        public const string Family = EdgeCore.Family;

        private ProductRegistration? registration;
        internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info) ? (info.Instance as Plugin)?.registration : null;
        private MeadworksLog? log;
        private Harmony? harmony;
        private bool started;
        private EdgeRuleFiles? rules;
        private EdgeSourceConversion? conversion;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Edge UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Edge's settings, read at use time; null until Awake succeeded.</summary>
        public static EdgeConfig? Settings { get; private set; }

        /// <summary>Edge's Meadworks log (quiet by default); null before Awake.</summary>
        internal static MeadworksLog? Log { get; private set; }

        private void Awake()
        {
            log = MeadworksLog.For(Logger, Name);
            Log = log;
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.RequiredOnAllPeers, Family));
                AllowsUi = MeadworksHeadless.AllowsUi;
                EdgeText.Initialize(Info.Metadata, Logger);
                Settings = EdgeConfig.Bind(Config, log);
                rules = new EdgeRuleFiles(Settings, log);
                Ready = true;
                log.Info(AllowsUi ? "Edge registered; modules start after plugin discovery."
                    : "Edge registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                log.Error("Edge initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || registration == null || log == null) return;
            try
            {
                EdgeModules.Decide(log);
                conversion = new EdgeSourceConversion(Config, Settings!, rules!, log);
                QuickstepSystem.Initialize();
                LastEquippedShieldSystem.Initialize();
                EdgeNativeDefinitions.Initialize();
                EdgeSkillPages.Initialize();
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                EpicLootCompat.Initialize();
                started = true;
            }
            catch (Exception error)
            {
                log.Error("Edge modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            EdgeText.Tick();
            rules?.Tick();
            if (started) EdgeNativeDefinitions.Tick();
            if (started) EdgeSwimReequip.Tick();
            if (started) EdgeCooldownHud.Update(Player.m_localPlayer);
            if (started) EdgeAnimationReferences.Tick();
            if (started) Bows.BowViewSystem.UpdateIndicator(Player.m_localPlayer);
            if (started && log != null) EdgeModules.Tick(log);
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            EpicLootCompat.Dispose();
            try { harmony?.UnpatchSelf(); } catch (Exception error) { log?.Warning("Edge unpatch: " + error.GetType().Name); }
            harmony = null;
            QuickstepSystem.Dispose();
            conversion?.Dispose(); conversion = null;
            rules?.Shutdown();
            rules = null;
            EdgeAnimationReferences.Reset();
            EdgeSkillPages.Dispose();
            EdgeCooldownHud.Dispose();
            EdgeCompendium.Release();
            SecondaryAttackKeyHintSystem.Dispose();
            EdgeNativeDefinitions.Dispose();
            foreach (Action stop in new Action[] { Bows.BowViewSystem.Reset, Bows.ArrowRecoverySystem.Reset, EdgeSwimReequip.Reset, LastEquippedShieldSystem.Dispose, EdgeModules.Reset, EdgeText.Shutdown })
                try { stop(); } catch (Exception error) { log?.Warning("Edge teardown: " + error.GetType().Name); }
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
