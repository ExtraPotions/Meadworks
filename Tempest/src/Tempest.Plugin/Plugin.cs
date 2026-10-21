// Tempest - ExtraPotions Meadworks world weather events: rare blizzards and severe thunderstorms with a warning phase.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Commands;
using ExtraPotions.Tempest.Integration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// Tempest (Runestone). Required on all peers (design section 6): weather must be identical for everyone, so a modded
    /// server refuses clients without Tempest and major.minor must match. On a vanilla server or a Crucible server without
    /// Tempest the product is off for the session. Crucible and the Crucible.UI kit are hard dependencies. A headless
    /// process (dedicated server, C10) never builds UI.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = TempestCore.PluginGuid;
        public const string Name = TempestCore.ProductName;
        public const string Version = TempestCore.ProductVersion;
        public const string Family = TempestCore.Family;

        private ProductRegistration? registration;
        internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info) ? (info.Instance as Plugin)?.registration : null;
        private MeadworksLog? log;
        private static TempestImport? import;

        /// <summary>The NoFogBruh import behind <c>tempest import [apply]</c>; null until Start succeeded.</summary>
        internal static TempestImport? Import => import;
        private Harmony? harmony;
        private bool started;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Tempest UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Tempest's settings, read at use time; null until Awake succeeded.</summary>
        public static TempestConfig? Settings { get; private set; }

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
                Settings = TempestConfig.Bind(Config, log);
                Settings.Changed += OnSettingsChanged;
                TempestText.Initialize(Info.Metadata, Logger);
                Ready = true;
                log.Info(AllowsUi ? "Tempest registered; modules start after plugin discovery."
                    : "Tempest registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                log.Error("Tempest initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || registration == null || log == null) return;
            try
            {
                TempestModules.Decide(log);
                import = new TempestImport(Config, Settings!, log);
                TempestWorld.Initialize(log, Config);
                TempestSky.Initialize(log);
                TempestStrikes.Initialize(log);
                TempestBadge.Initialize(log);
                WeatherCapability.Register(registration);
                TempestRemoteCommands.Initialize(registration, log); // Temporary until Crucible's CS-D1 relay exists.
                CommandManager.Instance.AddConsoleCommand(new TempestCommand(CommandParser.Name));
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                started = true;
            }
            catch (Exception error)
            {
                log.Error("Tempest modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            TempestText.Tick();
            if (!started || log == null) return;
            TempestModules.Tick(log);
            TempestWorld.Tick(UnityEngine.Time.deltaTime);
            TempestSky.Tick();
            TempestStrikes.Tick();
            TempestBadge.Tick();
            TempestRemoteCommands.Tick();
        }

        private static void OnSettingsChanged()
        {
            TempestFilter.Invalidate();
            TempestSky.UpdateFlags();
            TempestFilter.Reapply();
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            try { harmony?.UnpatchSelf(); } catch (Exception error) { log?.Warning("Tempest unpatch: " + error.GetType().Name); }
            harmony = null;
            foreach (Action stop in new Action[] { TempestRemoteCommands.Shutdown, TempestStrikes.Shutdown, TempestBadge.Shutdown, TempestSky.Shutdown, TempestFilter.Shutdown, TempestWorld.Shutdown, () => import?.Dispose(), TempestModules.Reset, TempestText.Shutdown })
                try { stop(); } catch (Exception error) { log?.Warning("Tempest teardown: " + error.GetType().Name); }
            import = null;
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
