// Cairn - ExtraPotions Meadworks exploration: map radius, fog and smoke removal, time and weather HUD, Exploration skill.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace ExtraPotions.Cairn.Runtime
{
    /// <summary>
    /// Cairn (Waystone). Client-optional (roster peer classes, design section 5): works on any server; major.minor must match
    /// when both peers run Cairn. A server running Cairn pushes its radius values and removal allowances (C01); on a server
    /// without Cairn the Q1 hold keeps gate-like mist on and the radius at Pathfinder's defaults. Each module steps aside for
    /// its own source mod (design 9.1). A headless process binds configuration and applies the authoritative world clock, without UI.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.VersionCheckOnly, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = CairnCore.PluginGuid;
        public const string Name = CairnCore.ProductName;
        public const string Version = CairnCore.ProductVersion;
        public const string Family = CairnCore.Family;

        private ProductRegistration? registration;
        internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info) ? (info.Instance as Plugin)?.registration : null;
        private MeadworksLog? log;
        private Harmony? harmony;
        private CairnImport? import;
        private bool started;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Cairn UI and touch render state (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Cairn's log for runtime parts that warn once; null before Awake.</summary>
        internal static MeadworksLog? Log { get; private set; }

        /// <summary>Cairn's settings, read at use time; null until Awake succeeded.</summary>
        public static CairnConfig? Settings { get; private set; }

        private void Awake()
        {
            log = MeadworksLog.For(Logger, Name);
            Log = log;
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.Optional, Family));
                AllowsUi = MeadworksHeadless.AllowsUi;
                CairnText.Initialize(Info.Metadata, Logger);
                Settings = CairnConfig.Bind(Config, log);
                import = new CairnImport(Config, Settings, log);
                CairnContracts.Register(registration);
                Ready = true;
                log.Info(AllowsUi ? "Cairn registered; modules start after plugin discovery."
                    : "Cairn registered on a headless process; authoritative world clock, no UI or render work.");
            }
            catch (Exception error)
            {
                log.Error("Cairn initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || registration == null || log == null) return;
            try
            {
                CairnModules.Decide(log);
                CairnExploration.Register(log);
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(CairnWorldClock));
                if (AllowsUi && !CairnModules.StepAside.SteppedAside(Integration.CairnModule.Radius))
                    harmony.PatchAll(typeof(CairnRadius.Patches));
                if (AllowsUi && !CairnModules.StepAside.SteppedAside(Integration.CairnModule.Visibility))
                    CairnVisibility.Install(harmony);
                if (AllowsUi) harmony.PatchAll(typeof(CairnTidyPins.Patches));
                if (AllowsUi) harmony.PatchAll(typeof(CairnStatusRoom.Patches));
                if (AllowsUi) harmony.PatchAll(typeof(CairnExploration.Patches));
                if (AllowsUi) CairnMenu.Register();
                if (AllowsUi) harmony.PatchAll(typeof(CairnSettingsWindow.Patches));
                started = true;
            }
            catch (Exception error)
            {
                log.Error("Cairn modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            CairnText.Tick();
            if (!started || log == null) return;
            CairnModules.Tick(log);
            if (AllowsUi) import?.Tick();
            if (AllowsUi) CairnVisibility.Tick(UnityEngine.Time.unscaledTime);
            if (AllowsUi) CairnTempest.Tick(log);
            if (AllowsUi) CairnSleep.Tick();
            if (AllowsUi) CairnHud.Tick();
            if (AllowsUi) CairnSquares.Tick();
            if (AllowsUi) CairnTempest.Present(log);
            if (AllowsUi) CairnSettingsWindow.Tick();
            if (AllowsUi) CairnExploration.Tick(UnityEngine.Time.time, UnityEngine.Time.deltaTime);
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            try { CairnVisibility.Uninstall(); } catch (Exception error) { log?.Warning("Cairn visibility restore: " + error.GetType().Name); }
            try { harmony?.UnpatchSelf(); } catch (Exception error) { log?.Warning("Cairn unpatch: " + error.GetType().Name); }
            harmony = null;
            import?.Dispose(); import = null;
            foreach (Action stop in new Action[] { CairnMenu.Reset, CairnSettingsWindow.Reset, CairnStatusRoom.Reset, CairnHud.Reset, CairnSquares.Reset, CairnTidyPins.Reset, CairnExploration.Dispose, CairnRadius.Reset, CairnTempest.Reset, CairnSleep.Reset, CairnModules.Reset, CairnText.Shutdown })
                try { stop(); } catch (Exception error) { log?.Warning("Cairn teardown: " + error.GetType().Name); }
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
