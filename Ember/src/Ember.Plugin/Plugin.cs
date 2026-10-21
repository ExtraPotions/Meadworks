// Ember - ExtraPotions Meadworks home comforts: multiplayer sleep, torches as fire, automatic doors.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Ember;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Ember.Runtime
{
    /// <summary>
    /// Ember (Hearth): sleep and the partial sleep boost, torches as fire sources, automatic doors.
    /// Required on all peers (roster peer classes): sleep time runs on the server and torch fire areas change world
    /// rules every peer sees; major.minor must match. On a vanilla server or a Crucible server without Ember the whole
    /// product is off for the session (user decision Q2). Each module steps aside for its own source mod (Q3). A headless
    /// process (dedicated server, C10) never builds UI and never runs door logic.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    // The Crucible.UI kit is the standard Meadworks UI: a mandatory dependency (shipped in the Crucible package, idle on a
    // headless server).
    [BepInDependency(ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Guid, ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = EmberCore.PluginGuid;
        public const string Name = EmberCore.ProductName;
        public const string Version = EmberCore.ProductVersion;
        public const string Family = EmberCore.Family;

        private ProductRegistration? registration;
        internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info) ? (info.Instance as Plugin)?.registration : null;
        private MeadworksLog? log;
        private Harmony? harmony;
        private EmberImport? import;
        private bool started;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Ember UI and run door logic (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Ember's settings, read at use time; null until Awake succeeded.</summary>
        public static EmberConfig? Settings { get; private set; }

        private void Awake()
        {
            log = MeadworksLog.For(Logger, Name);
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.RequiredOnAllPeers, Family));
                AllowsUi = MeadworksHeadless.AllowsUi;
                Settings = EmberConfig.Bind(Config, log);
                EmberSeasons.Initialize(Config);
                EmberText.Initialize(Info.Metadata, Logger);
                Ready = true;
                log.Info(AllowsUi ? "Ember registered; modules start after plugin discovery."
                    : "Ember registered on a headless process; no UI and no door logic are created.");
            }
            catch (Exception error)
            {
                log.Error("Ember initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || Settings == null || registration == null || log == null) return;
            try
            {
                EmberModules.Decide(log);
                SleepRuntime.Initialize(registration, log);
                SleepCapability.Register(registration);
                EmberSleepHud.Initialize(log);
                TorchRuntime.Initialize(registration, log);
                DoorManager.Initialize(log);
                import = new EmberImport(Config, Settings, log);
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                started = true;
            }
            catch (Exception error)
            {
                log.Error("Ember modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            var dt = UnityEngine.Time.unscaledDeltaTime;
            Settings?.Tick(UnityEngine.Time.unscaledTime);
            EmberText.Tick();
            if (!started) return;
            EmberModules.Tick(log!);
            EmberSeasons.Tick();
            OwnedBedRespawn.Tick();
            OwnedBedChoice.Tick();
            OwnedBedPins.Tick();
            QuickRespawn.Tick();
            SleepRuntime.Tick(UnityEngine.Time.deltaTime);
            SleepCapability.Tick();
            EmberSleepHud.Tick();
            TorchRuntime.Tick(dt);
            DoorManager.Tick(dt);
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            try { harmony?.UnpatchSelf(); } catch (Exception error) { log?.Warning("Ember unpatch: " + error.GetType().Name); }
            harmony = null;
            foreach (Action stop in new Action[] { OwnedBedChoice.Close, OwnedBedPins.Clear, QuickRespawn.Reset, OwnedBedRespawn.Reset, EmberSleepHud.Shutdown, SleepRuntime.Shutdown, SleepCapability.Reset, EmberSeasons.Shutdown, TorchRuntime.Shutdown, DoorManager.Shutdown, EmberModules.Reset, EmberText.Shutdown })
                try { stop(); } catch (Exception error) { log?.Warning("Ember teardown: " + error.GetType().Name); }
            import?.Dispose();
            import = null;
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
