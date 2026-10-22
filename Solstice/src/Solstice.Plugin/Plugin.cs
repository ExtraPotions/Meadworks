// Solstice - ExtraPotions Meadworks seasons: spring, summer, fall and winter by the world calendar, with seasonal effects.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Integration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Solstice (Runestone). Required on all peers: seasons change world state (growth, bees, fuel, weather), so a modded server refuses
    /// clients without Solstice and major.minor must match. On a vanilla server or a Crucible server without Solstice the product is off for
    /// the session. Crucible and the Crucible.UI kit are hard dependencies. The season plate, its mark and the heat meter are displays, not controls.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = SolsticeCore.PluginGuid;
        public const string Name = SolsticeCore.ProductName;
        public const string Version = SolsticeCore.ProductVersion;
        public const string Family = SolsticeCore.Family;

        private ProductRegistration? registration;
        private MeadworksLog? log;
        private Harmony? harmony;
        private bool started;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>Solstice's settings, read at use time; null until Awake succeeded.</summary>
        public static SolsticeConfig? Settings { get; private set; }

        private void Awake()
        {
            log = MeadworksLog.For(Logger, Name);
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.RequiredOnAllPeers, Family));
                Settings = SolsticeConfig.Bind(Config, log);
                Settings.Changed += OnSettingsChanged;
                SolsticeText.Initialize(Info.Metadata, Logger);
                SeasonClockSync.Initialize(log, Config);
                Ready = true;
                log.Info("Solstice registered; modules start after plugin discovery.");
            }
            catch (Exception error)
            {
                log.Error("Solstice initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || registration == null || log == null) return;
            try
            {
                SolsticeModules.Decide(log);
                SeasonImport.Run(Settings, log);
                SolsticeSeasons.Initialize(log);
                SeasonVisuals.Initialize(log);
                SeasonWinterLook.Initialize(log);
                SolsticeWeather.Initialize(log);
                SeasonEnvironmentVariants.Initialize(log);
                SeasonCapability.Register(registration);
                CommandManager.Instance.AddConsoleCommand(new SolsticeCommand());
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                started = true;
            }
            catch (Exception error)
            {
                log.Error("Solstice modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            SolsticeText.Tick();
            if (!started || log == null) return;
            SolsticeModules.Tick(log);
            SolsticeSeasons.Tick();
            SeasonVisuals.Tick();
            SeasonWinterLook.Tick();
            SeasonIndicator.Tick();
            SeasonStatus.Tick();
            SeasonHeatHud.Tick();
            SeasonEnvironmentVariants.Ensure(EnvMan.instance, true);
            SolsticeWeather.Tick();
        }

        private static void OnSettingsChanged()
        {
            SolsticeSeasons.Invalidate();
            SeasonVisuals.Invalidate();
            SeasonWinterLook.Invalidate();
            SeasonMusicRuntime.Invalidate();
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            try { SeasonCrops.Shutdown(); } catch (Exception error) { log?.Warning("Solstice crop watch stop: " + error.GetType().Name); }
            try { SeasonEnvironmentVariants.Shutdown(); } catch (Exception error) { log?.Warning("Solstice sky restore: " + error.GetType().Name); }
            try { SeasonWinterLook.Shutdown(); } catch (Exception error) { log?.Warning("Solstice winter look stop: " + error.GetType().Name); }
            try { harmony?.UnpatchSelf(); } catch (Exception error) { log?.Warning("Solstice unpatch: " + error.GetType().Name); }
            harmony = null;
            try { SeasonHeatHud.Shutdown(); } catch (Exception error) { log?.Warning("Solstice heat meter stop: " + error.GetType().Name); }
            try { SeasonStatus.Shutdown(); } catch (Exception error) { log?.Warning("Solstice status stop: " + error.GetType().Name); }
            try { SeasonMarks.Shutdown(); } catch (Exception error) { log?.Warning("Solstice season marks stop: " + error.GetType().Name); }
            try { SeasonMusicRuntime.Shutdown(); } catch (Exception error) { log?.Warning("Solstice music stop: " + error.GetType().Name); }
            foreach (Action stop in new Action[]
            {
                SeasonIndicator.Shutdown, SeasonVisuals.Shutdown, SeasonStatus.Shutdown, SeasonEnvironmentVariants.Shutdown, SeasonClockSync.Shutdown,
                SolsticeWeather.Shutdown, SolsticeSeasons.Shutdown, SolsticeModules.Reset, SolsticeText.Shutdown
            })
                try { stop(); } catch (Exception error) { log?.Warning("Solstice teardown: " + error.GetType().Name); }
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
