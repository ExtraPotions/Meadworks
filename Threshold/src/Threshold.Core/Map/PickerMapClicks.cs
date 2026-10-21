// Threshold - ExtraPotions Meadworks portals.
// Portions from Wayfare 0.3.2 and ValheimModLibs (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: ValheimModLibs/MapClicks/IconClick.cs, Wayfare/Wayfare/src/Targeting/MapClickPatch.cs.
// Changed by expDARE, 2026: the held click and the click rules as Unity-free values for Threshold.Core (no Harmony, no
// Minimap), the you-are-here point, the game's handlers named for the picker's map patch, and Threshold's names.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Map
{
    /// <summary>What a click on one of Threshold's portal icons does while the picker holds the map.</summary>
    public enum IconClickAction
    {
        /// <summary>Nothing (the picker is closed, or a right click with no map pin under it).</summary>
        None,
        /// <summary>Travel once the game's double-click window has passed without a second click.</summary>
        HoldTravel,
        /// <summary>The second click of a double click: the game places its own map pin there; no travel.</summary>
        PlacePin,
        /// <summary>A right click on one of the player's map pins: the game removes it.</summary>
        RemovePin,
    }

    /// <summary>The picker's map clicks (4C-11): a double click places one of the game's own map pins and a right click
    /// removes one, while a single click on a portal icon still travels. Pins are the game's own (its pin list and its
    /// save, per character and world), so Threshold never adds to the pin list and never collides with other pins.</summary>
    public static class PickerMapClicks
    {
        /// <summary>The game's window between the two clicks of a double click (<c>Minimap.OnMapLeftUp</c>).</summary>
        public const float DoubleClickSeconds = 0.3f;

        /// <summary>The game's map handlers the picker patches by name.</summary>
        public const string LeftClick = "OnMapLeftClick", RightClick = "OnMapRightClick", DoubleClick = "OnMapDblClick",
            RemovePinUnderPointer = "RemovePinUnderPointer";

        public static readonly string[] PatchedHandlers = { LeftClick, RightClick, DoubleClick, RemovePinUnderPointer };

        /// <summary>Whether the game's handler runs. With the picker closed every handler is the game's. While it is open a
        /// single click on the map stays blocked (it would cross out or claim a pin while the player chooses a
        /// destination); the double click (place a pin) and the right-click removal run as on the ordinary map.</summary>
        public static bool NativeRuns(string handler, bool pickerOpen) =>
            !pickerOpen || handler == DoubleClick || handler == RemovePinUnderPointer;

        /// <summary>A click on a portal icon. <paramref name="secondClick"/>: a held travel to this same icon is still
        /// inside the double-click window. <paramref name="pinUnderPointer"/>: one of the game's map pins lies under the
        /// pointer, as the game's own removal finds it.</summary>
        public static IconClickAction OnIcon(bool pickerOpen, bool rightButton, bool secondClick, bool pinUnderPointer)
        {
            if (!pickerOpen) return IconClickAction.None;
            if (rightButton) return pinUnderPointer ? IconClickAction.RemovePin : IconClickAction.None;
            return secondClick ? IconClickAction.PlacePin : IconClickAction.HoldTravel;
        }

        /// <summary>Where the you-are-here marker stands: the portal walked into while the picker is open, else the
        /// player. Null without either (no player yet).</summary>
        public static MapPoint? HereAt(bool pickerOpen, PortalEntry? source, MapPoint? player)
        {
            if (pickerOpen && source != null) return source.Position;
            return player;
        }
    }

    /// <summary>A travel held through the double-click window: it runs once the window has passed, unless a second click
    /// on the same icon drops it first. A later hold on another icon replaces it.</summary>
    public sealed class HeldClick<T> where T : class
    {
        private T? held;
        private float due;

        public T? Held => held;

        /// <summary>Whether a held action for <paramref name="target"/> is still inside its window at <paramref name="now"/>.</summary>
        public bool IsSecondClick(T target, float now, Func<T, T, bool> same) =>
            held != null && now < due && same(held, target);

        public void Hold(T target, float now)
        {
            held = target ?? throw new ArgumentNullException(nameof(target));
            due = now + PickerMapClicks.DoubleClickSeconds;
        }

        public void Drop() => held = null;

        /// <summary>The held target once its window has passed (and forgets it), else null.</summary>
        public T? TakeDue(float now)
        {
            if (held == null || now < due) return null;
            var target = held;
            held = null;
            return target;
        }
    }
}
