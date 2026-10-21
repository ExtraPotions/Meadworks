// Threshold - ExtraPotions Meadworks portals.
// Portions from Wayfare 0.3.2 and ValheimModLibs (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: ValheimModLibs/MapClicks/IconClick.cs, Wayfare/Wayfare/src/Targeting/MapClickPatch.cs.
// Changed by expDARE, 2026: clicks arrive on Threshold's own icon buttons (pointer events) instead of a hit test in a
// Minimap.OnMapLeftClick prefix; the held travel ticks from the picker's Update and is checked against the picker's
// current destinations; a double click on an icon asks the game's own double click to place the pin; Threshold's names.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ExtraPotions.Threshold.Map;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Runtime.Travel;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ExtraPotions.Threshold.Runtime.Map
{
    /// <summary>Pointer clicks on one of Threshold's portal icons on the large map.</summary>
    public sealed class ThresholdMapPinClick : MonoBehaviour, IPointerClickHandler
    {
        public PortalEntry? Target;

        public void OnPointerClick(PointerEventData data)
        {
            if (Target == null) return;
            if (data.button == PointerEventData.InputButton.Left) ThresholdMapClicks.OnIcon(Target, false);
            else if (data.button == PointerEventData.InputButton.Right) ThresholdMapClicks.OnIcon(Target, true);
        }
    }

    /// <summary>The picker's map clicks (4C-11). A click on a portal icon travels once the game's double-click window has
    /// passed; a double click there places one of the game's own map pins instead, and a right click on one of the
    /// player's map pins removes it. Pins are the game's (its list, its save), never Threshold's.</summary>
    public static class ThresholdMapClicks
    {
        static readonly HeldClick<PortalEntry> held = new HeldClick<PortalEntry>();

        public static void OnIcon(PortalEntry target, bool rightButton)
        {
            var minimap = Minimap.instance;
            float now = Time.unscaledTime;
            bool second = held.IsSecondClick(target, now, Same);
            switch (PickerMapClicks.OnIcon(ThresholdPickerWindow.IsOpen, rightButton, second, minimap != null && PinUnderPointer(minimap)))
            {
                case IconClickAction.HoldTravel:
                    held.Hold(target, now);
                    break;
                case IconClickAction.PlacePin:
                    held.Drop();
                    minimap!.OnMapDblClick();
                    break;
                case IconClickAction.RemovePin:
                    held.Drop();
                    minimap!.RemovePinUnderPointer();
                    break;
            }
        }

        /// <summary>From the picker's Update: a held travel whose window has passed runs, if the picker is still open and the
        /// portal is still one of its destinations.</summary>
        public static void Tick()
        {
            if (!ThresholdPickerWindow.IsOpen) { held.Drop(); return; }
            var target = held.TakeDue(Time.unscaledTime);
            if (target == null) return;
            if (ThresholdPickerWindow.SessionPins.Any(p => p.Portal != null && p.Portal.Id.Equals(target.Id))) ThresholdTravel.TravelTo(target);
        }

        public static void Drop() => held.Drop();

        /// <summary>Whether one of the player's map pins lies under the pointer, as the game's own click and removal find it.</summary>
        static bool PinUnderPointer(Minimap minimap)
        {
            var point = minimap.ScreenToWorldPoint(ZInput.pointerPosition);
            return minimap.GetClosestPin(point, minimap.PinInteractRadius, true) != null;
        }

        static bool Same(PortalEntry a, PortalEntry b) => a.Id.Equals(b.Id);
    }
}
