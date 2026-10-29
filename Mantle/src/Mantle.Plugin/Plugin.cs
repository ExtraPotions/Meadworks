using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mantle.Core;
using Mantle.Backpacks;
using ExtraPotions.Mantle.Embedded.SyncedConfig;
using ExtraPotions.Mantle.Embedded.PatchGuard;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using CrucibleUiPlugin = ExtraPotions.Meadworks.Crucible.UI.CrucibleUiPlugin;

namespace Mantle
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Pin the published Crucible dependency while retaining Crucible.UI's compatible native floor.
    [BepInDependency(CruciblePlugin.Guid, "0.3.37")]
    // The Crucible.UI kit is the standard Meadworks UI (mandatory; idle on a headless server). Mantle's adopted PackPanel
    // inventory UI is the kit's own baseline and stays as adopted. A later 0.3.x still loads.
    [BepInDependency(CrucibleUiPlugin.Guid, "0.3.20")]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    // Inscribe (Meadworks' EliteCrafting) is optional: when it is there, its Awake runs first so the backpack class and Deep
    // Pockets register through its API (Mantle.Elite.EliteSetup, parity choice #94); without it nothing of that runs. Load order
    // only: the API itself is reached through the Crucible capability meadworks.crafting.api, never by plugin lookup.
    [BepInDependency("com.meadworks.inscribe", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("milkyteam.packpanel")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.meadworks.mantle";
        public const string PluginName = "Mantle";
        public const string PluginVersion = "0.1.35";
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static SyncedConfiguration Synced { get; private set; }
        public static ConfigEntry<bool> LockConfiguration { get; private set; }
        public static bool IsReady { get; private set; }
        public static bool WorldReady => IsReady && ZNet.instance && CruciblePlugin.IsReady
            && CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.WorldId != 0;

        private ProductRegistration registration;
        internal static ProductRegistration Source => Instance?.registration;
        private Harmony harmony;

        private void Awake()
        {
            try
            {
                Initialize();
                IsReady = true;
            }
            catch
            {
                OnDestroy();
                throw;
            }
        }

        private void Initialize()
        {
            if (!CruciblePlugin.IsReady)
                throw new InvalidOperationException("Crucible is required.");
            Instance = this;
            Log = Logger;
            registration = CruciblePlugin.Services.Register(new ProductMetadata(PluginGuid, PluginName,
                new System.Version(PluginVersion), new System.Version(0, 1), new System.Version(0, 1),
                MultiplayerPolicy.RequiredOnAllPeers, "Hoard"));
            Synced = new SyncedConfiguration(this, Logger, PluginName, PluginVersion, Mantle.Compatibility.VersionFloor.OldestAccepted(PluginVersion));
            Mantle.Accessories.AccessoryText.Initialize(Info.Metadata, Logger);
            LockConfiguration = Synced.BindLocking("General", "Lock Configuration", true,
                "Server administrators control inventory layout and equipment settings.");
            InventoryModule.Initialize(Synced);
            Mantle.Trinkets.TrinketSettings.Initialize(Synced);
            Mantle.Trinkets.TrinketRuntime.Initialize();
            Mantle.Elite.EliteSetup.Register();
            Mantle.Accessories.AccessoryInscriptions.Register();
            Mantle.Settings.KeepOnDeathUpgrade.Run(Synced.Config, InventorySettings.KeepOnDeath, Logger);
            Mantle.Protection.ProtectedSlots.Initialize();
            registration.AddCapability<Func<Tuple<bool, IReadOnlyList<Tuple<int, int>>>>>("meadworks.inventory.protected_slots", new System.Version(1, 0), Mantle.Protection.ProtectedSlots.Read);
            registration.AddCapability<Func<IReadOnlyList<Tuple<int, int>>>>("meadworks.mantle.foodslots", new System.Version(1, 0), FoodSlotCells);
            registration.AddCapability<Func<object, bool>>("meadworks.mantle.itemrouting", new System.Version(1, 0), value => value is Inventory inventory && InventoryState.Manages(inventory));
            Mantle.Interop.MantleLinks.Register(registration);
            registration.AddCapability<Func<string, bool>>("meadworks.inventory.plates", new System.Version(1, 0), Mantle.Presentation.WildbloodPlate.Showing);
            InventorySettings.BrownStyle.SettingChanged += OnLookChanged;
            InventorySettings.Enabled.SettingChanged += OnLookChanged;
            ExtraPotions.Meadworks.Crucible.UI.KitLook.Changed += OnSharedLookChanged;
            Mantle.Look.LookRequest.Refresh();
            harmony = new Harmony(PluginGuid);
            foreach (Type type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
            {
                if (Admitted(type) && type.GetCustomAttributes(typeof(HarmonyPatch), true).Length != 0)
                    harmony.CreateClassProcessor(type).Patch();
            }
            Mantle.Presentation.LanguageRefresh.Install();
            BackpackPrefab.Install(harmony);
            Mantle.Tackle.TackleboxPrefab.Install(harmony);
            Mantle.Accessories.AccessoryPrefab.Install(harmony);
            Synced.Finish(harmony);
            Guard.Install(harmony, Logger, Assembly.GetExecutingAssembly());
            Logger.LogInfo("Mantle " + PluginVersion + " | Hoard | expDARE | adopted inventory/equipment; original model-rendered item art");
        }

        // Cross-module source supports compilation; the crafting panel, the Timber look and in-place upgrades stay excluded.
        internal static bool Admitted(Type type) => PatchAdmission.Allows(type.FullName);

        private static void OnLookChanged(object sender, EventArgs args)
        {
            Mantle.Look.LookRequest.Refresh();
            Mantle.Ring.KeyRingPopup.Restyle();
        }

        private static void OnSharedLookChanged()
        {
            Mantle.Look.LookRequest.Refresh();
            Mantle.Look.GridSkin.RefreshLook();
            // After the shared repaint, which dresses the ring's Brown disc as a square panel.
            Mantle.Ring.KeyRingPopup.Restyle();
        }

        private static IReadOnlyList<Tuple<int, int>> FoodSlotCells()
        {
            if (!InventoryState.Active || !InventoryState.IsLocal(Player.m_localPlayer))
                return Array.Empty<Tuple<int, int>>();
            var cells = new List<Tuple<int, int>>();
            foreach (Vector2i cell in InventoryState.CellsOf(Mantle.Slots.SlotKind.Food))
                cells.Add(Tuple.Create(cell.x, cell.y));
            return cells.AsReadOnly();
        }

        private void LateUpdate() { Mantle.Trinkets.TrinketKillCredit.Tick(); Mantle.Trinkets.TrinketRuntime.Tick(); Mantle.Panels.InventoryWarmup.Tick(); Mantle.Accessories.AccessoryText.Tick(); Mantle.Accessories.AccessoryWords.Tick(); Mantle.Presentation.WildbloodPlate.Tick(); }

        private void OnDestroy()
        {
            IsReady = false;
            Mantle.Trinkets.TrinketKillCredit.Dispose();
            Mantle.Trinkets.TrinketRuntime.Dispose();
            Mantle.Trinkets.TrinketSettings.Dispose();
            Mantle.Panels.StatusEffectIcons.Restore();
            Mantle.Panels.HudRoom.Reset();
            Mantle.Protection.ProtectedSlots.Reset();
            Mantle.Presentation.WildbloodPlate.Reset();
            Mantle.Accessories.AccessoryText.Shutdown();
            ExtraPotions.Meadworks.OriginalArt.OriginalItemArt.Shutdown();
            InventorySettings.BrownStyle.SettingChanged -= OnLookChanged;
            InventorySettings.Enabled.SettingChanged -= OnLookChanged;
            ExtraPotions.Meadworks.Crucible.UI.KitLook.Changed -= OnSharedLookChanged;
            Mantle.Look.GridSkin.ClearTrackedPanels();
            Mantle.Look.LookRequest.Release();
            Mantle.Panels.CraftingPanelPatch.Unhook();
            InventoryModule.Shutdown();
            Mantle.Elite.EliteSetup.Shutdown();
            Mantle.Accessories.AccessoryInscriptions.Shutdown();
            Mantle.Presentation.LanguageRefresh.Uninstall();
            harmony?.UnpatchSelf();
            Synced?.ConfigWatcher?.Dispose();
            Synced = null;
            registration?.Dispose();
            Instance = null;
        }
    }
}
