// Keel - ExtraPotions Meadworks ships and sailing: ship tuning, the Sailing skill, new ship types and deck pens.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// Keel (Waystone). Required on all peers (design section 5): Keel ship prefabs must exist on every peer, tuning must
    /// be identical on whichever peer owns a ship, and pens and Wind Call need handlers everywhere; major.minor must
    /// match. On a vanilla server or a Crucible server without Keel the product is off for the session. Crucible and the
    /// Crucible.UI kit are hard dependencies. A headless process (dedicated server, C10) never builds UI.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = KeelCore.PluginGuid;
        public const string Name = KeelCore.ProductName;
        public const string Version = KeelCore.ProductVersion;
        public const string Family = KeelCore.Family;

        private ProductRegistration? registration;
        internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info) ? (info.Instance as Plugin)?.registration : null;
        private MeadworksLog? log;
        private KeelImport? import;
        private Harmony? harmony;
        private bool started;
        private readonly bool[] displayFailed = new bool[6];

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Keel UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Keel's settings, read at use time; null until Awake succeeded.</summary>
        public static KeelConfig? Settings { get; private set; }

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
                Settings = KeelConfig.Bind(Config, log);
                KeelText.Initialize(Info.Metadata, Logger);
                KeelExplore.Register(registration);
                Ready = true;
                log.Info(AllowsUi ? "Keel registered; modules start after plugin discovery."
                    : "Keel registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                log.Error("Keel initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || registration == null || log == null) return;
            try
            {
                KeelModules.Decide(log);
                import = new KeelImport(Config, Settings!, log);
                KeelSailing.Register(log);
                KeelShips.Initialize(log, import);
                KeelCatalog.Initialize(log);
                KeelPenCommands.Initialize(registration);
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                started = true;
            }
            catch (Exception error)
            {
                log.Error("Keel modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            KeelText.Tick();
            Settings?.Tick();
            if (!started || log == null) return;
            KeelModules.Tick(log);
            KeelShips.Tick();
            KeelCatalog.Tick();
            KeelPenCommands.Tick();
            SailingPerks.Tick(UnityEngine.Time.deltaTime);
            if (!AllowsUi) return;
            // One guard per display part: a failure in one no longer skips the others, and the first failure of each is logged with its stack.
            Guarded(0, "Keel actives", KeelActives.Tick, null);
            Guarded(1, "Keel ship panel", KeelShipPanel.Update, KeelShipPanel.Dispose);
            Guarded(4, "Keel wind square", KeelWindSquare.Update, KeelWindSquare.Dispose);
            Guarded(5, "Keel dial", KeelDial.Update, KeelDial.Dispose);
            Guarded(2, "Keel boat camera", KeelBoatCamera.Update, KeelBoatCamera.Restore);
            Guarded(3, "Keel sail opacity", () => KeelSailOpacity.Update(log), KeelSailOpacity.Restore);
        }

        private void Guarded(int part, string name, Action tick, Action? undo)
        {
            try { tick(); }
            catch (Exception error)
            {
                if (!displayFailed[part]) log?.Warning(name + ": " + error);
                displayFailed[part] = true;
                try { undo?.Invoke(); } catch (Exception cleanup) { log?.Warning(name + " cleanup: " + cleanup.GetType().Name); }
            }
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            try { harmony?.UnpatchSelf(); } catch (Exception error) { log?.Warning("Keel unpatch: " + error.GetType().Name); }
            harmony = null;
            foreach (Action stop in new Action[] { KeelLookout.Reset, KeelShipPanel.Dispose, KeelDial.Dispose, KeelWindSquare.Dispose, KeelBoatCamera.Restore, KeelSailOpacity.Restore, KeelHoldPanel.Restore, KeelPenCommands.Shutdown, KeelPen.Shutdown, KeelCatalog.Shutdown, KeelShips.Shutdown, () => import?.Dispose(), KeelSailing.Dispose, KeelModules.Reset, KeelText.Shutdown })
                try { stop(); } catch (Exception error) { log?.Warning("Keel teardown: " + error.GetType().Name); }
            import = null;
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
