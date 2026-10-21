#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/PadToggle.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
using ExtraPotions.Meadworks.Crucible;
using System.Linq;

namespace Timber.Camera
{
    /// <summary>
    /// Gamepad Toggle: the game's own button names (<c>ZInput</c>, so the player's rebinding applies) joined with +; the
    /// last goes down this frame while the others are held. Read only while a gamepad is the active input; an unknown
    /// name never counts as pressed (the game's lookup returns false). The default, hold the left trigger and click the
    /// right stick, is free in the game's default layout: with the trigger held the click hides nothing.
    /// </summary>
    public static class PadToggle
    {
        private static string parsedFrom;
        private static string[] buttons = new string[0];

        public static bool Pressed()
        {
            if (!MeadworksInput.Allows(InputContext.Gameplay) || !MeadworksInput.GamepadActive)
                return false;
            string[] names = Buttons();
            if (names.Length == 0 || !ZInput.GetButtonDown(names[names.Length - 1]))
                return false;
            for (int i = 0; i < names.Length - 1; i++)
            {
                if (!ZInput.GetButton(names[i]))
                    return false;
            }
            return true;
        }

        /// <summary>The setting split into names, parsed again only when its text changes.</summary>
        private static string[] Buttons()
        {
            string value = CameraPrefs.GamepadToggle.Value ?? "";
            if (value == parsedFrom)
                return buttons;
            parsedFrom = value;
            buttons = value.Split('+').Select(name => name.Trim()).Where(name => name.Length > 0).ToArray();
            return buttons;
        }
    }
}
