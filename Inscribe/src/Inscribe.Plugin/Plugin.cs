// Inscribe - ExtraPotions Meadworks gear inscriptions: rarity, runes and gems.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// Inscribe (Fang). Required on all peers (design section 6): rune and scroll prefabs must exist on every peer and
    /// rolls on client-owned creatures must use the server's rules; major.minor must match. On a vanilla server or a
    /// Crucible server without Inscribe the product is off for the session; items keep and show their state. Each module
    /// steps aside for its own source mod (design 10.1). A headless process (dedicated server, C10) never builds UI.
    /// The pinned native rarity engine shares Meadworks rule, display and lifecycle services.
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
        public const string Guid = InscribeCore.PluginGuid;
        public const string Name = InscribeCore.ProductName;
        public const string Version = InscribeCore.ProductVersion;
        public const string Family = InscribeCore.Family;

        private ProductRegistration? registration;
        internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info)
            ? (info.Instance as Plugin)?.registration : null;
        private MeadworksLog? log;
        private bool started;
        private InscribeYamlConversion? yamlConversion;
        private InscribeSourceConversion? sourceConversion;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        public static int NativeFailures { get; private set; }

        /// <summary>This process may build Inscribe UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Inscribe's settings, read at use time; null until Awake succeeded.</summary>
        public static InscribeConfig? Settings { get; private set; }

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
                Settings = InscribeConfig.Bind(Config, log);
                InscribeText.Initialize(Info.Metadata, Logger);
                InscribeCapabilities.Publish(registration, log);
                Ready = true;
                log.Info(AllowsUi ? "Inscribe registered; modules start after plugin discovery."
                    : "Inscribe registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                NativeFailures++;
                log.Error("Inscribe initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || registration == null || log == null) return;
            try
            {
                InscribeModules.Decide(log);
                HarmonyPoints.Verify(log);
                NativeGameplay.Start(Config, Logger, log);
                LegacyEnhancement.Start(log);
                ItemBadgeHooks.InstallDefaults();
                yamlConversion = new InscribeYamlConversion(Config, log);
                sourceConversion = new InscribeSourceConversion(Config);
                started = true;
            }
            catch (Exception error)
            {
                NativeFailures++;
                log.Error("Inscribe modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            InscribeText.Tick();
            Adopted.Rules.RuleReload.Tick();
            NativeGameplay.Tick();
            LegacyEnhancement.Tick();
            if (started && log != null) InscribeModules.Tick(log);
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            sourceConversion?.Dispose(); sourceConversion = null;
            yamlConversion?.Dispose(); yamlConversion = null;
            foreach (Action stop in new Action[] { LegacyEnhancement.Shutdown, ItemBadgeHooks.Reset, NativeGameplay.Shutdown, Adopted.Rules.RuleReload.Reset, InscribeModules.Reset, InscribeText.Shutdown, InscribeCapabilities.Reset })
                try { stop(); } catch (Exception error) { log?.Warning("Inscribe teardown: " + error.GetType().Name); }
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
