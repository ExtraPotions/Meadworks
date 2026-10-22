// Solstice - ExtraPotions Meadworks seasons: spring, summer, fall and winter by the world calendar, with seasonal effects.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Config;
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
    // BepInEx minimums for the published 8.0.1 kit. A later 0.3.x still loads.
    [BepInDependency(CruciblePlugin.Guid, "0.3.28")]
    [BepInDependency(CrucibleUiPlugin.Guid, "0.3.20")]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = SolsticeCore.PluginGuid;
        public const string Name = SolsticeCore.ProductName;
        public const string Version = SolsticeCore.ProductVersion;
        public const string Family = SolsticeCore.Family;

        const int TickCount = 10;
        static readonly string[] TickNames = { "text", "modules", "seasons", "visuals", "winter", "indicator", "status", "heat", "variants", "weather" };

        private ProductRegistration? registration;
        private MeadworksLog? log;
        private Harmony? harmony;
        private bool started;
        private bool generalEnabled;
        private bool traceEnabled;
        private bool timingsReady;
        private long updateTicks;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly Stopwatch uptime = Stopwatch.StartNew();
        private readonly double[] nextFault = new double[TickCount];
        private readonly long[] elapsed = new long[TickCount];
        private Action[]? ticks;
        private Action<Exception>[]? reporters;

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
                generalEnabled = Settings.Get(SolsticeSettings.General.Enabled);
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
            EnsureTicks();
            clock.Restart();
            elapsed[0] = Time(0, ticks![0]);
            if (!started || log == null) return;
            if (SeasonFrame.EnabledToggled)
            {
                SeasonFrame.EnabledToggled = false;
                traceEnabled = true;
            }
            for (int i = 1; i < TickCount; i++) elapsed[i] = Time(i, ticks[i]);
            updateTicks = clock.ElapsedTicks;
            timingsReady = true;
        }

        private void LateUpdate()
        {
            if (!Ready)
            {
                SeasonFrame.DayMoved = false;
                SeasonFrame.EnabledToggled = false;
                SeasonFrame.ClearWork();
                traceEnabled = false;
                timingsReady = false;
                return;
            }
            bool day = SeasonFrame.DayMoved;
            bool enabled = traceEnabled;
            bool hold = SeasonFrame.EnabledToggled;
            var ran = SeasonFrame.Ran;
            long weather = SeasonFrame.WeatherSwapTicks;
            long music = SeasonFrame.MusicTicks;
            bool logged = timingsReady && (day || enabled) && log != null;
            if (logged)
                log!.Debug(FrameText(day, enabled, updateTicks, elapsed, weather, music, ran));
            SeasonFrame.DayMoved = false;
            traceEnabled = false;
            timingsReady = false;
            if (logged || !hold) SeasonFrame.ClearWork();
        }

        private void EnsureTicks()
        {
            if (ticks != null) return;
            reporters = new Action<Exception>[TickCount];
            for (int i = 0; i < TickCount; i++)
            {
                int index = i;
                reporters[i] = error => NoteFault(index, error);
            }
            ticks = new Action[]
            {
                SolsticeText.Tick,
                () => SolsticeModules.Tick(log!),
                SolsticeSeasons.Tick,
                SeasonVisuals.Tick,
                SeasonWinterLook.Tick,
                SeasonIndicator.Tick,
                SeasonStatus.Tick,
                SeasonHeatHud.Tick,
                () => SeasonEnvironmentVariants.Ensure(EnvMan.instance, true),
                SolsticeWeather.Tick
            };
        }

        private long Time(int index, Action tick)
        {
            long start = clock.ElapsedTicks;
            TickGuard.Run(tick, ref nextFault[index], uptime.Elapsed.TotalSeconds, reporters![index]);
            return clock.ElapsedTicks - start;
        }

        private void NoteFault(int index, Exception error) =>
            log?.Warning("Solstice " + TickNames[index] + " tick: " + error.GetType().Name + ": " + error.Message);

        static string FrameText(bool day, bool enabled, long updateTicks, long[] parts, long weatherTicks, long musicTicks, SeasonFrameSystem ran)
        {
            string why = day && enabled ? "day change and Enabled toggle" : day ? "day change" : "Enabled toggle";
            return "Solstice " + why + " frame: update " + SeasonFrame.Milliseconds(updateTicks)
                + " ms (text " + SeasonFrame.Milliseconds(parts[0])
                + ", modules " + SeasonFrame.Milliseconds(parts[1])
                + ", seasons " + SeasonFrame.Milliseconds(parts[2])
                + ", visuals " + SeasonFrame.Milliseconds(parts[3])
                + ", winter " + SeasonFrame.Milliseconds(parts[4])
                + ", indicator " + SeasonFrame.Milliseconds(parts[5])
                + ", status " + SeasonFrame.Milliseconds(parts[6])
                + ", heat " + SeasonFrame.Milliseconds(parts[7])
                + ", variants " + SeasonFrame.Milliseconds(parts[8])
                + ", weather " + SeasonFrame.Milliseconds(parts[9])
                + "; weather list swap " + SeasonFrame.Milliseconds(weatherTicks)
                + ", music invalidate " + SeasonFrame.Milliseconds(musicTicks)
                + "). Ran: " + SeasonFrame.Describe(ran) + ".";
        }

        private void OnSettingsChanged()
        {
            if (Settings != null)
            {
                bool enabled = Settings.Get(SolsticeSettings.General.Enabled);
                if (enabled != generalEnabled)
                {
                    generalEnabled = enabled;
                    SeasonFrame.EnabledToggled = true;
                }
            }
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
            traceEnabled = false;
            timingsReady = false;
            SeasonFrame.DayMoved = false;
            SeasonFrame.EnabledToggled = false;
            SeasonFrame.ClearWork();
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
            SeasonFrame.ClearWork();
        }
    }
}
