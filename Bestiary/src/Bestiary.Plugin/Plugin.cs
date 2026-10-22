// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Integration;
using ExtraPotions.Bestiary.Runtime.Prefabs;
using ExtraPotions.Bestiary.Runtime.Settings;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Bestiary.Runtime
{
    /// <summary>
    /// Bestiary (Fang): EliteCreaturesPack's creatures and bone arsenal, and the creature workshop (design Q1).
    /// Required on all peers (design section 6): prefab hashes in ZDOs must resolve to the same objects everywhere;
    /// major.minor must match. A headless process (dedicated server, C10) builds and registers prefabs and runs AI and
    /// spawns, but no player holds, overlays or Compendium. macOS is unsupported (bundles are per platform): Bestiary
    /// turns itself off there with one notice.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    // 0.1.17 requires Crucible 0.3.28. Its UI is 0.3.20, shipped in that package and idle on a headless server.
    [BepInDependency(CruciblePlugin.Guid, "0.3.28")]
    [BepInDependency(ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Guid, "0.3.20")]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = BestiaryCore.ProductId;
        public const string Name = BestiaryCore.ProductName;
        public const string Version = BestiaryCore.Version;

        private ProductRegistration? registration;
        private MeadworksLog? log;
        private Harmony? harmony;
        private BestiaryImport? import;
        private Workshop.WorkshopConversion? workshopImport;

        /// <summary>Bound settings (section 4), or null before registration.</summary>
        internal static BestiaryConfig? Settings { get; private set; }

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        public static int NativeFailures { get; private set; }

        /// <summary>This process may draw overlays, holds and pages (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        private void Awake()
        {
            log = MeadworksLog.For(Logger, Name);
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                if (Application.platform == RuntimePlatform.OSXPlayer)
                {
                    log.Warning("Bestiary is not supported on macOS (its asset bundles are built for Windows and Linux only); it stays off.");
                    return;
                }
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.RequiredOnAllPeers, BestiaryCore.Family));
                AllowsUi = MeadworksHeadless.AllowsUi;
                BestiaryText.Initialize(Info.Metadata, Logger);
                Settings = new BestiaryConfig(Config);
                BestiaryIntegration.Initialize(registration, log);
                import = new BestiaryImport(Config, Settings, log);
                Workshop.WorkshopHost.Log = new Workshop.WorkshopLog(Logger);
                workshopImport = new Workshop.WorkshopConversion();
                harmony = new Harmony(Guid);
                Ready = true;
                log.Info(AllowsUi ? "Bestiary registered." : "Bestiary registered on a headless process; no overlays, holds or pages are created.");
            }
            catch (Exception error)
            {
                NativeFailures++;
                Shutdown();
                log.Error("Bestiary initialization rejected: " + error.GetType().Name + ": " + error.Message);
            }
        }

        private void Start()
        {
            if (!Ready) return;
            try
            {
                BestiaryIntegration.DecideStepAside();
                Native.Adapters.NativeRuntime.Start(Config, Logger, log!);
                Workshop.WorkshopRuntime.Start(Config, Logger);
                harmony!.CreateClassProcessor(typeof(Compendium.BestiaryCompendium)).Patch();
            }
            catch (Exception error)
            {
                NativeFailures++;
                log?.Error("Bestiary native startup failed: " + error);
                Shutdown();
            }
        }

        private void Update()
        {
            if (Ready) { BestiaryText.Tick(); Workshop.WorkshopRuntime.BeforeNativeTick(); Native.Adapters.NativeRuntime.Tick(); Workshop.WorkshopRuntime.Tick(); Compendium.BestiaryCompendium.Tick(); }
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            Compendium.BestiaryCompendium.Clear();
            Workshop.WorkshopRuntime.Shutdown();
            Native.Adapters.NativeRuntime.Shutdown();
            harmony?.UnpatchSelf();
            harmony = null;
            workshopImport?.Dispose(); workshopImport = null;
            import?.Dispose();
            import = null;
            PrefabClones.Reset();
            BestiaryIntegration.Shutdown();
            BestiaryText.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
