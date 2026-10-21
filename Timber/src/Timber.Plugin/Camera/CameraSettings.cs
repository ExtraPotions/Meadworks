#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CameraSettings.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
// Cartur portions: GPL-3.0-only; f81977b2553d48a2a79eea62969f86bb9d9fa32e. See docs/source-provenance.json.
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;


namespace Timber.Camera
{
    /// <summary>
    /// The gameplay keys of section "11. Build Camera": where the camera may go, how far it builds and picks up, and what
    /// it costs in comfort. All synced and lockable; read at use time.
    /// </summary>
    public static class CameraSettings
    {
        public const string Section = CameraModule.Section;

        public static CameraValue<bool> Enabled { get; private set; }
        public static CameraValue<float> RangeMultiplier { get; private set; }
        public static CameraValue<float> ExtraReach { get; private set; }
        public static CameraValue<bool> CameraPickup { get; private set; }
        public static CameraValue<float> TerrainClearance { get; private set; }
        public static CameraValue<float> PickupRange { get; private set; }
        public static CameraValue<bool> DemisterFollow { get; private set; }
        public static CameraValue<float> DemisterRange { get; private set; }
        public static CameraValue<bool> EntryNeedsResting { get; private set; }
        public static CameraValue<int> EntryMinComfort { get; private set; }
        public static CameraValue<bool> PickupNeedsResting { get; private set; }
        public static CameraValue<int> PickupMinComfort { get; private set; }

        public static void Bind(CameraBindings synced)
        {
            Enabled = synced.Bind(Section, "Enabled", true,
                "With a hammer, hoe or cultivator in hand near a crafting station (a workbench, forge, stonecutter...), Toggle Key detaches the camera from you. "
                + "Fly it with the movement keys, Jump up, Crouch down, Run faster; build, remove and repair from it as usual. Toggle Key again, Hide (R) or putting the tool away brings it back.");
            RangeMultiplier = synced.Bind(Section, "Range Multiplier", 1f,
                "The camera comes out and stays within this many times a station's build range around any crafting station (measured flat as the game does, and as far above or below it). "
                + "Only the camera's area: pieces still need the station's real range, and the station's base area (raids, comfort) is unchanged.",
                acceptableValues: new AcceptableValueRange<float>(0.1f, 10f), synced: true, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Build Camera: Enabled.") });
            ExtraReach = synced.Bind(Section, "Extra Reach", ExtraPotions.Timber.ReferenceCameraValues.DefaultExtraReach,
                "Meters the camera reaches beyond your own building reach (the game's 5 m, or what another mod makes it), measured from the camera, for placing, removing and repairing.",
                acceptableValues: new AcceptableValueRange<float>(0f, 95f), synced: true, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Build Camera: Enabled.") });
            CameraPickup = synced.Bind(Section, "Camera Pickup", true,
                "While the camera is out, items lying within the game's auto pickup range of it come into your inventory, as they do around you (only with the game's auto pickup on).", synced: true, acceptableValues: null, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Build Camera: Enabled.") });
            TerrainClearance = synced.Bind(Section, "Terrain Clearance", 1f, "Camera collision probe radius in meters.", acceptableValues: new AcceptableValueRange<float>(0.1f, 2f), synced: true, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Build Camera: Enabled.") });
            PickupRange = synced.Bind(Section, "Pickup Range", ExtraPotions.Timber.ReferenceCameraValues.DefaultPickupRange, "Camera auto pickup range in meters. 0: the player's native range.", acceptableValues: new AcceptableValueRange<float>(0f, 100f), synced: true, tags: new object[] { new ConfigDependency(() => Enabled.Value && CameraPickup.Value, () => "Requires Build Camera: Enabled and Camera Pickup.") });
            DemisterFollow = synced.Bind(Section, "Demister Follow Camera", true, "The equipped Wisplight follows the detached camera.", synced: true, acceptableValues: null, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Build Camera: Enabled.") });
            DemisterRange = synced.Bind(Section, "Demister Range Multiplier", 2.5f, "Wisplight clearing radius while the camera is detached.", acceptableValues: new AcceptableValueRange<float>(0.1f, 10f));
            BindNeeds(synced);
        }

        private static void BindNeeds(CameraBindings synced)
        {
            EntryNeedsResting = synced.Bind(Section, "Entry Needs Resting", false,
                "The camera comes out only while you have the game's Resting effect (by a fire, under a roof).", synced: true, acceptableValues: null, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Build Camera: Enabled.") });
            EntryMinComfort = synced.Bind(Section, "Entry Min Comfort", 0,
                "The comfort level you need where you stand to bring the camera out. 0: none.",
                acceptableValues: new AcceptableValueRange<int>(0, 50), synced: true, tags: new object[] { new ConfigDependency(() => Enabled.Value, () => "Requires Build Camera: Enabled.") });
            PickupNeedsResting = synced.Bind(Section, "Pickup Needs Resting", false,
                "Camera Pickup works only while you have the game's Resting effect.", synced: true, acceptableValues: null, tags: new object[] { new ConfigDependency(() => Enabled.Value && CameraPickup.Value, () => "Requires Build Camera: Enabled and Camera Pickup.") });
            PickupMinComfort = synced.Bind(Section, "Pickup Min Comfort", 0,
                "The comfort level you need where you stand for Camera Pickup. 0: none.",
                acceptableValues: new AcceptableValueRange<int>(0, 50), synced: true, tags: new object[] { new ConfigDependency(() => Enabled.Value && CameraPickup.Value, () => "Requires Build Camera: Enabled and Camera Pickup.") });
        }
    }
}
