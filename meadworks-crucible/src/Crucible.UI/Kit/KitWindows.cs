// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Meadworks.Crucible.UI.WindowInput;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// User decision 2026-10-12 19:15: while any MeadWorks window is open, HUD parts hide, as the game's do for the Compendium.
    /// </summary>
    public static class KitWindows
    {
        /// <summary>
        /// True while any MeadWorks window is open: every <see cref="KitWindow"/> (KitAlmanacDialog editors included) and every
        /// window a product registered with <see cref="GameWindow.Add"/>. It is read live from the windows themselves, so a window
        /// destroyed with its scene reads as closed and the flag can never stay on.
        /// </summary>
        public static bool AnyOpen => GameWindow.AnyOpen;

        /// <summary>Ordinary windows hide the HUD; a layout editor keeps it visible while retaining input ownership.</summary>
        public static bool HudBlocked => GameWindow.AnyHudBlocking;
    }
}
