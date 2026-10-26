using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Burrow.Core;
using Burrow.Stow;
using Burrow.Reach;
using Burrow.Capacity;
using Burrow.Shared;
using Burrow.Stacks;
using ExtraPotions.Burrow.Embedded.PatchGuard;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace Burrow
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(CruciblePlugin.Guid, "0.3.32")]
    // The Crucible.UI kit is the standard Meadworks UI (mandatory; idle on a headless server). The button row keeps its own
    // gamepad-preserving copy of the kit look (Look/ButtonLook.cs).
    [BepInDependency(CrucibleUiPlugin.Guid, "0.3.21")]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [BepInIncompatibility("milkyteam.openkeep")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.meadworks.burrow";
        public const string PluginName = "Burrow";
        public const string PluginVersion = "0.3.30";
        public static ManualLogSource Log { get; private set; }
        public static SyncedConfiguration Synced { get; private set; }
        public static ConfigEntry<bool> LockConfiguration { get; private set; }
        public static Plugin Instance { get; private set; }
        public static bool IsReady { get; private set; }
        private ProductRegistration registration;
        internal static ProductRegistration Source => Instance?.registration;
        private Harmony harmony;
        public Resources.NativeResourceProvider Resources { get; private set; }
        internal SharedTransfers.SharedTransferService Transfers { get; private set; }

        private void Awake()
        {
            try { Initialize(); IsReady = true; }
            catch { OnDestroy(); throw; }
        }
        private void Initialize()
        {
            if (!CruciblePlugin.IsReady) throw new InvalidOperationException("Crucible is required.");
            Instance = this;
            Log = Logger;
            registration = CruciblePlugin.Services.Register(new ProductMetadata(PluginGuid, PluginName,
                new System.Version(PluginVersion), new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.RequiredOnAllPeers, "Hoard"));
            Synced = new SyncedConfiguration(this, Logger, PluginName, PluginVersion, ExtraPotions.Burrow.VersionFloor.OldestAccepted(PluginVersion));
            LockConfiguration = Synced.BindLocking("General", "Lock Configuration", true,
                "Server administrators control shared storage settings.");
            CoreModule.Initialize(Synced);
            Integration.MultiUserChestYield.Bind(Synced);
            ReachModule.Initialize(Synced);
            Stations.StationExtensionReach.Bind(Synced);
            StowModule.Initialize(Synced);
            CapacityModule.Initialize(Synced);
            SceneReady.Initialize();
            Burrow.Capacity.PreviewSettings.Bind(Synced);
            Burrow.Capacity.ChestPreview.Initialize();
            StacksModule.Initialize(Synced);
            SharedModule.Initialize(Synced);
            HotkeyDefaultsUpgrade.Run(Synced);
            SettingDefaultsUpgrade.Run(Synced);
            Burrow.Core.PackPanelLink.Registry = () => ExtraPotions.Meadworks.Crucible.Plugin.IsReady ? ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities : null;
            Shared.ChestOps.NativeItemRouting = inventory => {
                var registry = ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities;
                var owners = registry.Providers("meadworks.mantle.itemrouting");
                return owners.Count == 1 && owners[0].Owner == "com.meadworks.mantle"
                    && registry.TryResolve<System.Func<object,bool>>("meadworks.mantle.itemrouting", new System.Version(1,0), out var route) && route(inventory);
            };
            Resources = new Resources.NativeResourceProvider();
            Transfers = new SharedTransfers.SharedTransferService(registration);
            registration.AddCapability<ExtraPotions.Meadworks.Contracts.IAsyncResourceProvider>("meadworks.resources.async",new System.Version(2,0),Resources);
            registration.AddCapability<Func<object,bool>>("meadworks.burrow.inventory_hold",new System.Version(1,0),Burrow.Resources.NativeInventoryGuards.OwnHeld);
            Integration.IntegrationLinks.Register(registration);
            Integration.ChestSalvageLink.Register(registration, () => Resources);
            Integration.StowRowYield.Initialize(Info.Metadata, Logger);
            harmony = new Harmony(PluginGuid);
            StacksModule.HookSpawns(harmony);
            int failed = 0;
            foreach (Type type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
            {
                if (type.Namespace != null && type.Namespace.StartsWith("ExtraPotions.Burrow.Embedded.", StringComparison.Ordinal)) continue;
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                try { harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception error) { failed++; Logger.LogError("Patch " + type.FullName + ": " + error); }
            }
            if (failed > 0) throw new InvalidOperationException(failed + " Burrow patches failed to apply.");
            Synced.Finish(harmony);
            Guard.Install(harmony, Logger, Assembly.GetExecutingAssembly());
            Logger.LogInfo("Burrow " + PluginVersion + " | Hoard | expDARE | storage modules loaded");
        }
        private void Start() => EpicLootLink.Register();
        private void Update(){Integration.StowRowYield.Tick();Transfers?.Tick();Resources?.Tick();Burrow.Resources.NativeInventoryGuards.DrainDeaths();Burrow.Resources.NativeInventoryGuards.DrainRemovals();Burrow.Stacks.PackPanelKeys.Tick(UnityEngine.Time.unscaledTime);Guard.Run("chest preview",Burrow.Capacity.ChestPreview.Tick);}
        private void OnDestroy()
        {
            Shared.ChestOps.NativeItemRouting = null;
            IsReady = false;
            SceneReady.Shutdown();
            Burrow.Capacity.ChestPreview.Shutdown();
            Integration.StowRowYield.Shutdown();
            Integration.MultiUserChestYield.Shutdown();
            Stations.StationExtensionReach.Shutdown();
            Transfers?.Dispose();
            Transfers = null;
            Resources?.Dispose();
            Resources = null;
            harmony?.UnpatchSelf();
            Synced?.ConfigWatcher?.Dispose();
            registration?.Dispose();
            Instance = null;
            Synced = null;
        }
    }
}
