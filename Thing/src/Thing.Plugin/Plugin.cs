// Thing - ExtraPotions Meadworks server administration and moderation.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Integration;
using Jotunn.Managers;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace ExtraPotions.Thing.Runtime
{
    /// <summary>
    /// Thing (Runestone). Required on all peers (design section 6): the server delivers moderation notices, refusal reasons
    /// and localized messages through every player's Thing, so a modded server refuses clients without Thing and major.minor
    /// must match. On a vanilla server or a Crucible server without Thing the product is off for the session. Crucible and
    /// the Crucible.UI kit are hard dependencies. A headless process (dedicated server, C10) never builds UI.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = ThingCore.PluginGuid;
        public const string Name = ThingCore.ProductName;
        public const string Version = ThingCore.ProductVersion;
        public const string Family = ThingCore.Family;

        private ProductRegistration? registration;
        internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info) ? (info.Instance as Plugin)?.registration : null;
        private MeadworksLog? log;
        private ThingImport? import;
        private bool started;

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Thing UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>Thing's settings, read at use time; null until Awake succeeded.</summary>
        public static ThingConfig? Settings { get; private set; }

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
                Settings = ThingConfig.Bind(Config, log);
                ThingText.Initialize(Info.Metadata, Logger);
                ThingNotices.Initialize(log);
                ThingPlayerFreeze.Initialize(log);
                ThingServer.Initialize(log);
                ThingPickup.Initialize(log);
                Ready = true;
                log.Info(AllowsUi ? "Thing registered; modules start after plugin discovery."
                    : "Thing registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                log.Error("Thing initialization rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || log == null) return;
            try
            {
                ThingModules.Decide(log);
                import = new ThingImport(Config, Settings!, log);
                ThingRemoteCommands.Initialize(registration!, log); // Temporary until Crucible's CS-D1 relay exists
                ThingTerrainCommands.Initialize(registration!);
                ThingDropCommands.Initialize(registration!);
                ThingTeleportCommands.Initialize(registration!);
                ThingWard.Initialize(registration!);
                ThingPoi.Initialize(registration!);
                ThingTime.Initialize();
                ThingChatMute.Initialize();
                ThingHexAudit.Initialize(registration!);
                ThingAccess.Initialize(log);
                ThingInventory.Initialize(registration!, log);
                registration!.AddCapability<Func<string, string, bool>>(AdminPermissions.CapabilityId, AdminPermissions.CapabilityVersion, ThingAccess.Allows);
                // meadworks.admin.command: another product (Hex) runs a thing command through Thing's own admin, module and audit checks.
                registration!.AddCapability<Func<string[], bool>>(ExtraPotions.Meadworks.Contracts.AdminCommandContract.Id,
                    ExtraPotions.Meadworks.Contracts.AdminCommandContract.ContractVersion, args => { new ThingCommand().Run(args); return true; });
                CommandManager.Instance.AddConsoleCommand(new ThingCommand());
                started = true;
            }
            catch (Exception error)
            {
                log.Error("Thing modules rejected: " + error.GetType().Name + ": " + error.Message);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            ThingText.Tick();
            if (!started || log == null) return;
            Settings?.Tick(UnityEngine.Time.unscaledTime);
            ThingModules.Tick(log);
            ThingNotices.Tick();
            ThingServer.Tick();
            ThingRemoteCommands.Tick();
            ThingYamlFiles.Tick();
            ThingTeleportCommands.Tick();
            ThingInventory.Tick();
            ThingWard.Tick();
            if (Player.m_localPlayer != null)
            {
                ThingPlayerFreeze.Tick();
                ThingTime.Tick();
                ThingTerrainCommands.Tick();
                ThingDropCommands.Tick();
                ThingPickup.Tick();
                ThingPanel.Tick();
                if (Settings != null && ThingModules.Active(ThingModule.WorldTools))
                {
                    if (MeadworksInput.Pressed(Settings.ShortcutValue(ThingSettings.Hotkeys.WardOverride))) ThingWard.Run(new[] { "ward" });
                    if (MeadworksInput.Pressed(Settings.ShortcutValue(ThingSettings.Hotkeys.RemoveDrops))) ThingDropCommands.Run(new[] { "drops" });
                }
            }
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            foreach (Action stop in new Action[] { ThingPanel.Shutdown, ThingConfigFiles.Reset, ThingYamlFiles.Reset, ThingTeleportCommands.Shutdown, ThingChatMute.Shutdown, ThingTime.Shutdown, ThingPoi.Shutdown, ThingWard.Shutdown, ThingPickup.Shutdown, ThingDropCommands.Shutdown, ThingTerrainCommands.Shutdown, ThingInventory.Shutdown, ThingRemoteCommands.Shutdown, ThingServer.Unload, ThingNotices.Shutdown, ThingPlayerFreeze.Shutdown, () => import?.Dispose(), ThingModules.Reset, ThingText.Shutdown })
                try { stop(); } catch (Exception error) { log?.Warning("Thing teardown: " + error.GetType().Name); }
            import = null;
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
