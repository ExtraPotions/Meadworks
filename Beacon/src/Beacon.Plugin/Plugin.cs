// Beacon - ExtraPotions Meadworks server favorites, auto-join and protected password storage.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using ExtraPotions.Beacon.Integration;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Managers;
using Jotunn.Utils;
using HarmonyLib;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Beacon.Runtime
{
    /// <summary>
    /// Beacon (Runestone). Client-optional (design section 7, Crucible <see cref="MultiplayerPolicy.Optional"/>): it acts only
    /// before and while joining on the local client and works on any server, modded or vanilla. Crucible and the Crucible.UI
    /// kit are hard dependencies (floors follow the built Crucible and Crucible.UI versions). A headless process binds only configuration/authority: no
    /// UI, no secret store access, one Debug line.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiGuid, ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = BeaconCore.PluginGuid;
        public const string Name = BeaconCore.ProductName;
        public const string Version = BeaconCore.ProductVersion;
        public const string Family = BeaconCore.Family;
        const string CrucibleUiGuid = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin.Guid;

        private ProductRegistration? registration;
        internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info) ? (info.Instance as Plugin)?.registration : null;
        private MeadworksLog? log;
        private Harmony? harmony;
        private bool started;

        /// <summary>Registered with Crucible and running on a graphical client.</summary>
        public static bool Ready { get; private set; }

        public static BeaconConfig? Settings { get; private set; }

        /// <summary>Beacon's C06 log; every text Beacon writes passes the secret redactor first.</summary>
        public static MeadworksLog? Log { get; private set; }

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
                Settings = BeaconConfig.Bind(Config, log);
                if (!MeadworksHeadless.AllowsUi)
                {
                    log.Debug("Beacon loaded on a headless process: server policy bound; no UI, password storage or joining.");
                    return;
                }
                BeaconText.Initialize(Info.Metadata, Logger);
                Ready = true;
            }
            catch (Exception error)
            {
                log.Error("Beacon initialization rejected: " + error.GetType().Name);
                Shutdown();
            }
        }

        private void Start()
        {
            if (!Ready || log == null) return;
            try
            {
                BeaconModules.Decide(log);
                BeaconSecrets.Initialize(Settings!, log);
                BeaconServers.Load(log);
                if (BeaconModules.Active(BeaconModule.Join))
                {
                    harmony = new Harmony(Guid);
                    harmony.CreateClassProcessor(typeof(BeaconJoinPatches)).Patch();
                    harmony.CreateClassProcessor(typeof(BeaconFavoritePatches)).Patch();
                    BeaconAutoJoin.Initialize();
                }
                // The panel's own server list input guard applies whether or not the join module runs.
                harmony ??= new Harmony(Guid);
                harmony.CreateClassProcessor(typeof(BeaconServerListPatches)).Patch();
                CommandManager.Instance.AddConsoleCommand(new BeaconCommand());
                BeaconConversion.Register();
                started = true;
            }
            catch (Exception error)
            {
                // Never the message: an exception text could quote a value Beacon holds.
                log.Error("Beacon modules rejected: " + error.GetType().Name);
                Shutdown();
            }
        }

        private void Update()
        {
            if (!Ready) return;
            BeaconText.Tick();
            if (!started || log == null) return;
            if (FejdStartup.instance == null && !BeaconJoin.Joining)
            {
                BeaconSecrets.Tick(log);
                BeaconSecretActions.Tick();
                BeaconSaveOffer.Tick();
                return;
            }
            BeaconModules.Tick(log);
            BeaconSecrets.Tick(log);
            BeaconSecretActions.Tick();
            BeaconFavorites.Tick();
            BeaconMenuBack.Tick();
            BeaconPanel.Tick();
            BeaconJoin.Tick(log);
            BeaconAutoJoin.Tick(log);
            BeaconSaveOffer.Tick();
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started = false;
            try { harmony?.UnpatchSelf(); } catch (Exception error) { log?.Warning("Beacon unpatch: " + error.GetType().Name); }
            harmony = null;
            foreach (Action stop in new Action[] { BeaconConversion.Reset, BeaconAutoJoin.Reset, BeaconSaveOffer.Reset, BeaconFavorites.Reset, BeaconPanel.Reset, BeaconImportSecrets.Reset, BeaconJoin.Reset, BeaconSecretActions.Reset, BeaconServers.Unload, BeaconSecrets.Shutdown, BeaconModules.Reset, BeaconText.Shutdown })
                try { stop(); } catch (Exception error) { log?.Warning("Beacon teardown: " + error.GetType().Name); }
            Settings?.Shutdown();
            Settings = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
