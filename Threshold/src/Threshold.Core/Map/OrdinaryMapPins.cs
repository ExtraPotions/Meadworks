// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: portal pins and name tags on the game's ordinary large map (parity #13, Show Portal Pins). Behavior
// as Wayfare's map icons (GPL-3.0-or-later; Threshold's own layer, never the game's pin list, so nothing is saved or
// crossed out) and PotalMap's pins (behaviour only): every portal the player may use, its tag beside it, colored by its
// access mode (#72) and spread when portals stand close together (#70).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Picker;

namespace ExtraPotions.Threshold.Map
{
    /// <summary>The large map's view: its UV rectangle over the whole map (0 to 1) and its size in GUI pixels.</summary>
    public readonly struct MapView
    {
        public MapView(float uvX, float uvY, float uvWidth, float uvHeight, float width, float height)
        {
            UvX = uvX;
            UvY = uvY;
            UvWidth = uvWidth;
            UvHeight = uvHeight;
            Width = width;
            Height = height;
        }

        public float UvX { get; }
        public float UvY { get; }
        public float UvWidth { get; }
        public float UvHeight { get; }
        public float Width { get; }
        public float Height { get; }
    }

    public static class OrdinaryMapPins
    {
        /// <summary>Icon side in GUI pixels, also the spread gap.</summary>
        public const float IconSize = 24f;

        /// <summary>Pins draw on the open large map while Threshold routes and Show Portal Pins is on; the travel session draws its
        /// targets on this same map during a session, and a headless process draws nothing.</summary>
        public static bool Shows(bool allowsUi, bool active, bool showPortalPins, bool largeMap, bool inSession) =>
            allowsUi && active && (showPortalPins || inSession) && largeMap;

        /// <summary>One pin per portal the player may use, in list order (the list query with no current portal). Favorites
        /// and hubs included; never a Here pin or the player arrow.</summary>
        public static IReadOnlyList<MapPinSpec> Build(IReadOnlyList<PickerGroup> groups, bool accessModesEnabled, bool highlightStone = false)
        {
            var plan = MapPinPlan.Build(groups, null, null, null, accessModesEnabled, highlightStone);
            return plan.Pins.Take(plan.DestinationCount).ToList().AsReadOnly();
        }

        /// <summary>The name tag beside a pin: the list's label, the untagged word for an untagged portal, nothing with Show
        /// Map Labels off.</summary>
        public static string Label(MapPinSpec pin, bool showLabels, string untagged)
        {
            if (pin == null) throw new ArgumentNullException(nameof(pin));
            if (!showLabels) return "";
            return pin.Label.Length > 0 ? pin.Label : untagged ?? "";
        }

        /// <summary>A map point (0 to 1 over the whole map) as a position from the large map's lower-left corner, in GUI
        /// pixels (the game's own conversion for its large-map pins).</summary>
        public static PinPoint ToGui(float mx, float my, MapView view) =>
            new PinPoint((mx - view.UvX) / view.UvWidth * view.Width, (my - view.UvY) / view.UvHeight * view.Height);

        /// <summary>Whether a map point lies inside the large map's current view (the game's own visibility test).</summary>
        public static bool Visible(float mx, float my, MapView view) =>
            mx > view.UvX && mx < view.UvX + view.UvWidth && my > view.UvY && my < view.UvY + view.UvHeight;
    }
}
