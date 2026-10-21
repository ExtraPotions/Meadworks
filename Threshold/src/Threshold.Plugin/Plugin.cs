// Threshold - ExtraPotions Meadworks portals: destination picker, named hubs, access modes, favorites and fuel.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Threshold.Runtime.Conversion;
using ExtraPotions.Threshold.Runtime.Map;
using ExtraPotions.Threshold.Runtime.Portals;
using ExtraPotions.Threshold.Runtime.SeaGates;
using ExtraPotions.Threshold.Runtime.Travel;
using HarmonyLib;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace ExtraPotions.Threshold.Runtime
{
    /// <summary>
    /// Threshold (Waystone). Required on all peers (design section 5): the complete portal list exists only on the server,
    /// hub flags and access modes are portal ZDO data changed on request, permissions are checked on the server; major.minor
    /// must match. On a vanilla server or a Crucible server without Threshold the product is off for the session. Steps
    /// aside as a whole while PortalSelector, PotalMap or Wayfare is installed (design 9.1). A headless process (dedicated
    /// server, C10) never builds UI. Settings, catalog, step-aside, peer-class gate and notices, the C09 PortalSelector import (T07b), the
    /// server portal list with discovery widening and its sync (T11), the travel patch, mode cycle and hub edits (T12)
    /// the picker window (T13b), the quick jumps and the sea gates (T14, default off).
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = ThresholdCore.PluginGuid;
        public const string Name = ThresholdCore.ProductName;
        public const string Version = ThresholdCore.ProductVersion;
        public const string Family = ThresholdCore.Family;

        private ProductRegistration? registration;
        internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info) ? (info.Instance as Plugin)?.registration : null;
        private MeadworksLog? log;
        private ThresholdSourceConversion? conversion, wayfareConversion;
        private Harmony? harmony;
        private bool started;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Threshold UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Threshold's log for code without a plugin instance (the map pane); null before Awake.</summary>
        internal static MeadworksLog? Log { get; private set; }

        /// <summary>Threshold's settings, read at use time; null until Awake succeeded.</summary>
        public static ThresholdConfig? Settings { get; private set; }

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
                Settings = ThresholdConfig.Bind(Config, log);
                ThresholdText.Initialize(Info.Metadata, Logger);
                conversion = new ThresholdSourceConversion(Config, Settings, log);
                wayfareConversion = new ThresholdSourceConversion(Config, Settings, log, wayfare: true);
                ThresholdPortals.Initialize(registration);
                ThresholdPortalEdits.Initialize(registration);
                Ready = true;
                log.Info(AllowsUi ? "Threshold registered; step-aside is decided after plugin discovery."
                    : "Threshold registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                log.Error("Threshold initialization rejected: " + error.GetType().Name + ": " + error.Message + (error.InnerException != null ? " <- " + error.GetBaseException().GetType().Name + ": " + error.GetBaseException().Message + " at " + (error.GetBaseException().StackTrace ?? "").Replace(System.Environment.NewLine, " | ") : ""));
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || registration == null || log == null) return;
            try
            {
                ThresholdModules.Decide(log);
                if (!ThresholdModules.SteppedAside)
                {
                    harmony = new Harmony(Guid);
                    ThresholdPortalDiscovery.Patch(harmony);
                    ThresholdTravel.Patch(harmony);
                    ThresholdPortalEdits.Patch(harmony);
                    SeaGates.SeaGates.Patch(harmony);
                    if (AllowsUi)
                    {
                        ThresholdJumps.Patch(harmony);
                        ThresholdPickerWindow.Patch(harmony);
                        ThresholdPickerWindow.Install();
                    }
                }
                started = true;
            }
            catch (Exception error)
            {
                log.Error("Threshold start rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            ThresholdText.Tick();
            Settings?.Tick();
            if (!started) return;
            if (log != null) ThresholdModules.Tick(log);
            ThresholdPortalDiscovery.Tick(ThresholdModules.DiscoveryEnabled());
            ThresholdPortals.Tick(ThresholdModules.PortalRoutingActive());
            ThresholdPortalEdits.Tick();
            if (!ThresholdModules.SteppedAside) SeaGates.SeaGates.Tick();
            if (AllowsUi)
            {
                ThresholdTravel.OpenPending();
                ThresholdPickerWindow.Tick();
                ThresholdTargetPicker.Tick();
                ThresholdSeaGatePicker.Tick();
                ThresholdTames.Tick();
            }
        }

        private void LateUpdate()
        {
            if (Ready && started && AllowsUi) ThresholdMapPins.LateTick();
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            wayfareConversion?.Dispose();
            wayfareConversion = null;
            conversion?.Dispose();
            conversion = null;
            harmony?.UnpatchSelf();
            harmony = null;
            foreach (Action stop in new Action[] { ThresholdMapPins.Shutdown, ThresholdPickerWindow.Shutdown, ThresholdTargetPicker.Shutdown, ThresholdSeaGatePicker.Shutdown, SeaGates.SeaGates.Reset, ThresholdJumps.Reset, ThresholdTravel.Reset, ThresholdPortalEdits.Shutdown, ThresholdPortals.Shutdown, ThresholdPortalDiscovery.Reset, ThresholdModules.Reset, ThresholdText.Shutdown })
                try { stop(); } catch (Exception error) { log?.Warning("Threshold teardown: " + error.GetType().Name); }
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
