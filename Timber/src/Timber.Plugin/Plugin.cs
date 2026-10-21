// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Foundation reuses established Meadworks registry/C01/C02/C03/C04/C05/C06/C10/UI dependencies.
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Timber.Camera;
using Timber.Flooring;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace ExtraPotions.Timber.Runtime
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = TimberCore.PluginGuid;
        public const string Name = TimberCore.ProductName;
        public const string Version = TimberCore.ProductVersion;
        public const string Family = TimberCore.Family;
        ProductRegistration? registration;
        internal static ProductRegistration? Source => BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Guid, out var info) ? (info.Instance as Plugin)?.registration : null;
        Harmony? harmony;
        MeadworksLog? log;
        internal static MeadworksLog? Log { get; private set; }
        public static ReferenceImport? Importer { get; private set; }
        public static PlayerSettingsLocks? PlayerLocks { get; private set; }
        long noticedSession = -1;
        public static bool Ready { get; private set; }
        public static bool AllowsUi { get; private set; }
        public static ConfigEntry<bool>? IgnoreSupportSetting { get; private set; }
        public static ConfigEntry<bool>? FreeBuildSetting { get; private set; }
        public static bool FreeBuild => Active && FreeBuildSetting != null &&
            MeadworksConfig.Effective(FreeBuildSetting, Guid, ConfigScope.Cheat, false);
        public static ConfigEntry<bool>? BuildEverywhereSetting { get; private set; }
        public static bool BuildEverywhere => Active && BuildEverywhereSetting != null &&
            MeadworksConfig.Effective(BuildEverywhereSetting, Guid, ConfigScope.Cheat, false);
        public static bool Active => Ready && MeadworksAuthority.ProductActive(Guid, out _);
        public static bool IgnoreSupport => Active && IgnoreSupportSetting != null &&
            MeadworksConfig.Effective(IgnoreSupportSetting, Guid, ConfigScope.Cheat, false);

        void Awake()
        {
            Log = log = MeadworksLog.For(Logger, Name);
            try
            {
                if (!CruciblePlugin.IsReady) throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.RequiredOnAllPeers, Family));
                AllowsUi = MeadworksHeadless.AllowsUi;
                MeadworksConfig.EnsureSchema(Config, 1);
                IgnoreSupportSetting = MeadworksConfig.BindCheat(Config, "Cheats", "Ignore Support", false,
                    "Pieces placed while enabled keep standing without support after it is disabled. Existing pieces are unchanged. Does not prevent rain, fire or attack damage.");
                MeadworksBlockedOptions.Track(Guid, IgnoreSupportSetting, ConfigScope.Cheat);
                FreeBuildSetting = MeadworksConfig.BindCheat(Config, "Cheats", "Free Build", false,
                    "Hammer pieces (including ships) cost no materials. Pieces must still be learned. Station, DLC and placement checks remain. Does not enable the game no-cost debug mode.");
                MeadworksBlockedOptions.Track(Guid, FreeBuildSetting, ConfigScope.Cheat);
                BuildEverywhereSetting = MeadworksConfig.BindCheat(Config, "Cheats", "Build Everywhere", false,
                    "Place and remove pieces inside no-build zones (dungeons, villages, boss altars). Wards, stations, support and the other placement checks still apply.");
                MeadworksBlockedOptions.Track(Guid, BuildEverywhereSetting, ConfigScope.Cheat);
                TimberText.Initialize(Info.Metadata, Logger);
                CameraModule.Initialize(Config);
                global::Timber.Repair.AreaRepairModule.Initialize(Config);
                FiresRuntime.Initialize(Config);
                PlayerLocks = new PlayerSettingsLocks(Config, log);
                Importer = new ReferenceImport(Config);
                Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new TimberCommand());
                registration.AddCapability<Func<IReadOnlyList<string>>>("meadworks.diagnostics." + Guid, new System.Version(1, 0),
                    () => new[] { "selective support-wear kernel + adopted OpenKeep camera core", "support cheat: " + MeadworksAuthority.Decide(Guid, ConfigScope.Cheat).Reason,
                        "camera core: " + CameraModule.Allowed, "Free Build: " + FreeBuild + "; Build Everywhere: " + BuildEverywhere + "; reference import registered", "UI allowed: " + AllowsUi, PlayerLocks?.Describe() ?? "Local locks unavailable", BindingReport.Summary() });
                // Timber owns debug build mode (read by Hex); presence semantics: true whenever Timber is loaded.
                registration.AddCapability<Func<bool>>(ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.Id(ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.DebugBuild),
                    ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.ContractVersion, () => true);
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                Ready = true;
                log.Info("Timber registered: support-wear kernel and build camera core; full backlog remains pending.");
            }
            catch (Exception error) { log.Error("Timber initialization rejected: " + error); Shutdown(); }
        }

        void Start()
        {
            if (!Ready) return;
            try { CameraModule.ResolveSources(); FiresRuntime.ResolveSources(); }
            catch (Exception error) { log?.Error("Timber source detection rejected: " + error); Shutdown(); }
            if (!Ready) return;
            try { foreach (string conflict in BindingReport.Lines(startup: true)) log?.Warning(conflict); }
            catch (Exception error) { log?.Warning("Binding report unavailable: " + error.GetType().Name); }
        }

        void Update()
        {
            if (!Ready) return;
            PlayerLocks?.Tick();
            TimberText.Tick();
            CameraModule.Tick();
            FiresRuntime.Tick();
            if (AllowsUi) CameraPickupHud.Tick();
            var session = CruciblePlugin.Session;
            if (session.AuthorityReady && noticedSession != session.Generation &&
                !MeadworksAuthority.ProductActive(Guid, out var reason) &&
                (reason == ConfigBlockReason.VanillaServer || reason == ConfigBlockReason.ProductDisabled))
            {
                noticedSession = session.Generation;
                string text = TimberText.Text("off_no_server");
                if (AllowsUi && MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
                log?.Info(text);
            }
        }

        void OnDestroy() => Shutdown();
        void Shutdown()
        {
            Ready = false;
            Importer?.Dispose(); Importer = null;
            PlayerLocks?.Dispose(); PlayerLocks = null;
            harmony?.UnpatchSelf(); harmony = null;
            SupportRuntime.Reset();
            global::Timber.Repair.AreaRepairModule.Reset();
            CameraModule.Reset();
            FiresRuntime.Reset();
            if (IgnoreSupportSetting != null) MeadworksBlockedOptions.Untrack(IgnoreSupportSetting);
            IgnoreSupportSetting = null;
            if (FreeBuildSetting != null) MeadworksBlockedOptions.Untrack(FreeBuildSetting);
            FreeBuildSetting = null;
            if (BuildEverywhereSetting != null) MeadworksBlockedOptions.Untrack(BuildEverywhereSetting);
            BuildEverywhereSetting = null;
            TimberText.Shutdown();
            registration?.Dispose(); registration = null;
        }
    }
}
