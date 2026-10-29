// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>
    /// Kindle (Forge): crafting station search, sort, favorites, views, batch crafting, recipe tracker and salvage.
    /// Client-optional (roster peer classes): works on any server; major.minor must match when both peers run Kindle.
    /// Registers the shared configuration/capabilities, then adopts crafting features after all plugins load.
    /// A headless process never builds UI; installed overlapping source mods retain their own crafting behavior.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, "0.3.37")]
    [BepInDependency(CrucibleUiPlugin.Guid, "0.3.21")]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.VersionCheckOnly, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.meadworks.kindle";
        public const string Name = "Kindle";
        public const string Version = "0.1.29";
        public const string Family = "Forge";

        private ProductRegistration? registration;
        internal static ProductRegistration? Source { get; private set; }
        private MeadworksLog? log;
        private KindleReferenceImport? referenceImport;
        private bool started;
        private long noticedSession = -1;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }
        public static bool SteppedAside { get; private set; }
        public static bool GameplayAvailable => Ready && !SteppedAside && MeadworksAuthority.ProductActive(Guid, out _);

        /// <summary>This process may build Kindle UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Kindle's settings, read at use time; null until Awake succeeded.</summary>
        public static KindleConfig? Settings { get; private set; }

        private void Awake()
        {
            log = MeadworksLog.For(Logger, Name);
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.Optional, Family));
                Source = registration;
                AllowsUi = MeadworksHeadless.AllowsUi;
                Settings = KindleConfig.Bind(Config, log);
                KindleText.Initialize(Info.Metadata, Logger);
                referenceImport = new KindleReferenceImport(Config, Settings, log);
                SalvageTabCapability.Register(registration);
                SalvageStackCapability.Register(registration);
                Ready = true;
                log.Info(AllowsUi ? "Kindle registered; crafting modules start after plugin discovery."
                    : "Kindle registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                Shutdown();
                log.Error("Kindle initialization rejected: " + error);
            }
        }

        private void Start()
        {
            if (!Ready || Settings == null || registration == null || log == null) return;
            try
            {
                SteppedAside = KindleReferenceImport.DeclareAndYield();
                // Server YAML/config still registers while source plugins own the client UI.
                KindleSalvage.Initialize(Config, registration, message => log.Warning(message));
                if (!SteppedAside && AllowsUi)
                {
                    KindleRecipes.Initialize(Settings, log);
                    KindleBatch.Initialize(Settings, log);
                    KindleTracker.Initialize(Settings, log);
                    KindleCarts.Initialize(Settings, log);
                    KindleVirtualStation.Initialize(Settings);
                    KindleStationReach.Initialize(Settings);
                    KindleRepair.Initialize();
                    KindleRecipes.ToggleTracking = KindleTracker.Toggle;
                    KindleRecipes.IsTracked = KindleTracker.IsTracked;
                    Localization.OnLanguageChange += Relabel;
                }
                started = true;
            }
            catch (Exception error)
            {
                log.Error("Kindle modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            Settings?.Tick(UnityEngine.Time.unscaledTime);
            KindleText.Tick();
            if (!started) return;
            referenceImport?.Tick(GameplayAvailable);
            if (SteppedAside && AllowsUi && InventoryGui.IsVisible() && MessageHud.instance != null &&
                noticedSession != CruciblePlugin.Session.Generation)
            {
                noticedSession = CruciblePlugin.Session.Generation;
                var sources = MeadworksStepAside.Decide(Guid).Present;
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                    KindleText.Format("step_aside", string.Join(", ", sources.Select(source => source.Name))));
            }
            KindleSalvage.Tick();
            if (!GameplayAvailable || !AllowsUi) return;
            KindleRecipes.Tick();
            KindleBatch.Tick();
            KindleTracker.Tick();
            KindleCarts.Tick();
            KindleVirtualStation.Tick();
        }

        /// <summary>
        /// The game's language changed (<c>Localization.SetLanguage</c>): every Kindle caption, tooltip and the tracker are
        /// written again in it at once, so no text from the previous language stays on screen.
        /// </summary>
        private void Relabel()
        {
            if (!GameplayAvailable || !AllowsUi) return;
            try { KindleSalvage.Tick(); KindleRecipes.Relabel(); KindleTracker.Relabel(); }
            catch (Exception error) { log?.Warning("Kindle language refresh: " + error.GetType().Name + ": " + error.Message); }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void Shutdown()
        {
            Localization.OnLanguageChange -= Relabel;
            Ready = false;
            started = false;
            noticedSession = -1;
            foreach (Action stop in new Action[] { KindleRepair.Shutdown, KindleRecipes.Shutdown, KindleBatch.Shutdown, KindleTracker.Shutdown, KindleCarts.Shutdown, KindleVirtualStation.Shutdown, KindleStationReach.Shutdown, KindleSalvage.Shutdown, KindleText.Shutdown })
                try { stop(); } catch (Exception error) { log?.Warning("Kindle teardown: " + error.GetType().Name); }
            referenceImport?.Dispose();referenceImport = null;
            Settings?.Shutdown();
            Settings = null;
            Source = null;
            registration?.Dispose();
            registration = null;
            SteppedAside = false;
        }
    }
}
