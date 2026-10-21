#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CameraPrefs.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup presentation delegated to Crucible.UI.
// Cartur portions: GPL-3.0-only; f81977b2553d48a2a79eea62969f86bb9d9fa32e. See docs/source-provenance.json.
using BepInEx.Configuration;

using UnityEngine;

namespace Timber.Camera
{
    /// <summary>
    /// Each player's own keys of section "Build Camera": the toggle, how fast the camera flies and the head light it
    /// carries. Bound through Crucible as Local; read at use time. Pickup notice presentation uses KX07.
    /// </summary>
    public static class CameraPrefs
    {
        public const string Section = CameraModule.Section;

        public static CameraValue<KeyboardShortcut> ToggleKey { get; private set; }
        public static CameraValue<string> GamepadToggle { get; private set; }
        public static CameraValue<float> Speed { get; private set; }
        public static CameraValue<float> RunMultiplier { get; private set; }
        public static CameraValue<bool> WorldMotion { get; private set; }
        public static CameraValue<bool> InvertMouseHorizontal { get; private set; }
        public static CameraValue<bool> InvertMouseVertical { get; private set; }
        public static CameraValue<bool> InvertPadHorizontal { get; private set; }
        public static CameraValue<bool> InvertPadVertical { get; private set; }
        public static CameraValue<Vector3> CircletOffset { get; private set; }
        public static CameraValue<string> CircletColour { get; private set; }
        public static CameraValue<CameraLightShadows> CircletShadows { get; private set; }
        public static CameraValue<bool> CircletLight { get; private set; }
        public static CameraValue<float> CircletIntensity { get; private set; }
        public static CameraValue<float> CircletRange { get; private set; }
        public static CameraValue<float> CircletSpotAngle { get; private set; }
        public static CameraValue<bool> PickupPanel { get; private set; }
        public static CameraValue<Vector2> PickupPanelPosition { get; private set; }
        public static void Bind(CameraBindings synced)
        {
            ToggleKey = synced.Bind(Section, "Toggle Key", new KeyboardShortcut(KeyCode.B, KeyCode.LeftShift),
                "Brings the build camera out and back while a hammer, hoe or cultivator is in hand.", false);
            GamepadToggle = synced.Bind(Section, "Gamepad Toggle", "JoyAltKeys + JoyRStick",
                "The same on a gamepad: the game's button names joined with +, the last pressed while the others are held (by default hold the left trigger and click the right stick). Empty: none.", false);
            Speed = synced.Bind(Section, "Speed", 10f, "Meters per second the camera flies.", false,
                new AcceptableValueRange<float>(1f, 100f));
            RunMultiplier = synced.Bind(Section, "Run Multiplier", 3f, "How much faster the camera flies while Run (Shift) is held.", false,
                new AcceptableValueRange<float>(1f, 10f));
            WorldMotion = synced.Bind(Section, "Move With Respect To World", false, "Movement follows world axes instead of camera heading.", false);
            InvertMouseHorizontal = synced.Bind(Section, "Invert Mouse Horizontal", false, "Reverse horizontal mouse look while the build camera is detached.", false);
            InvertMouseVertical = synced.Bind(Section, "Invert Mouse Vertical", false, "Reverse vertical mouse look while the build camera is detached.", false);
            InvertPadHorizontal = synced.Bind(Section, "Invert Pad Horizontal", false, "Reverse horizontal gamepad look while the build camera is detached.", false);
            InvertPadVertical = synced.Bind(Section, "Invert Pad Vertical", false, "Reverse vertical gamepad look while the build camera is detached.", false);
            BindLight(synced);
            PickupPanel = synced.Bind(Section, "Pickup Panel", true,
                "Show unmet camera pickup Resting and comfort needs in the shared Meadworks HUD panel.", false);
            PickupPanelPosition = synced.Bind(Section, "Pickup Panel Position", new Vector2(0f, -120f),
                "Pickup panel offset from the top center of the HUD in UI units: x right, y up.", false);
        }

        private static void BindLight(CameraBindings synced)
        {
            CircletLight = synced.Bind(Section, "Circlet Light", true,
                "A light worn on the head (the Dvergr circlet, also in another mod's extra slot) shines from the camera too while it is out, with the circlet's own settings unless the three below change them.", false);
            CircletOffset = synced.Bind(Section, "Circlet Offset", new Vector3(0f, -0.08f, 0.65f), "Camera light offset: right, up, forward; each clamped to -5 to 5 meters.", false);
            CircletColour = synced.Bind(Section, "Circlet Color", "Original", "Original or a hexadecimal RRGGBB[AA] color.", false);
            CircletShadows = synced.Bind(Section, "Circlet Shadows", CameraLightShadows.Original, "Original, Off, Hard or Soft shadows.", false);
            CircletIntensity = synced.Bind(Section, "Circlet Intensity", 0f, "Brightness of the camera's copy. 0: the circlet's own.", false,
                new AcceptableValueRange<float>(0f, 10f));
            CircletRange = synced.Bind(Section, "Circlet Range", 0f, "Meters the camera's copy reaches. 0: the circlet's own.", false,
                new AcceptableValueRange<float>(0f, 100f));
            CircletSpotAngle = synced.Bind(Section, "Circlet Spot Angle", 0f, "Width of the camera's copy's beam in degrees. 0: the circlet's own.", false,
                new AcceptableValueRange<float>(0f, 179f));
        }
    }
}
