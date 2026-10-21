// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the map pane of the picker (design section 10, TH06) on Crucible.UI KX09 KitMapView. Walking in
// opens the map and a click on a portal travels (U1); the clicked portal is the client's destination (U3). Every portal
// opens the map; the plan carries the hub of the map's Return To Hub button, marks the player's home hub pin, and a hub
// pin offers "Set as home hub".
using System;
using System.Collections.Generic;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Picker;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Routing;

namespace ExtraPotions.Threshold.Map
{
    /// <summary>What a pin stands for: a destination portal, a destination hub, the portal walked into, or the player arrow.</summary>
    public enum MapPinKind { Portal, Hub, Here, Player }

    /// <summary>One pin of the plan. <see cref="Portal"/> is null only for the player arrow.</summary>
    public sealed class MapPinSpec
    {
        internal MapPinSpec(MapPinKind kind, PortalEntry? portal, string label, bool favourite, bool home = false, AccessMode mode = AccessMode.Public,
            bool stone = false)
        {
            IsStone = stone;
            Kind = kind;
            Mode = mode;
            Portal = portal;
            Label = label;
            IsFavourite = favourite;
            IsHome = home;
        }

        public MapPinKind Kind { get; }
        public PortalEntry? Portal { get; }

        /// <summary>Hub name or tag as the list shows it (empty for an untagged portal and the player).</summary>
        public string Label { get; }

        public bool IsFavourite { get; }

        /// <summary>The portal's access mode as it applies now (Public while access modes are off); the pin is colored by it (#72).</summary>
        public AccessMode Mode { get; }

        /// <summary>A portal that lets every item through, drawn gray while Highlight Stone Portals is on.</summary>
        public bool IsStone { get; }

        /// <summary>This destination is the player's home hub in this world.</summary>
        public bool IsHome { get; }

        /// <summary>Shown even where the explored mask hides pins: the portal walked into and the player.</summary>
        public bool AlwaysShow => Kind == MapPinKind.Here || Kind == MapPinKind.Player;
    }

    /// <summary>The pins in the order the map view holds them: every picker row in list order (so list and map share one
    /// selection), then the portal walked into, then the player arrow, which the plugin moves each frame.</summary>
    public sealed class MapPinPlan
    {
        private MapPinPlan(IReadOnlyList<MapPinSpec> pins, int destinations, int here, int player, PortalEntry? returnHub)
        {
            Pins = pins;
            DestinationCount = destinations;
            HereIndex = here;
            PlayerIndex = player;
            ReturnHub = returnHub;
            ReturnHubIndex = returnHub != null ? IndexOf(returnHub.Id) : -1;
        }

        public IReadOnlyList<MapPinSpec> Pins { get; }

        /// <summary>Destination pins come first, indices 0 to DestinationCount - 1.</summary>
        public int DestinationCount { get; }

        /// <summary>Index of the portal walked into, or -1 without one.</summary>
        public int HereIndex { get; }

        /// <summary>Index of the player arrow (always the last pin).</summary>
        public int PlayerIndex { get; }

        /// <summary>The hub the map's Return To Hub button travels to, or null for no button (the route table's
        /// <c>PortalAction.ReturnToHub</c> answer).</summary>
        public PortalEntry? ReturnHub { get; }

        /// <summary>The destination pin of <see cref="ReturnHub"/>, or -1 (no button, or the hub is not listed).</summary>
        public int ReturnHubIndex { get; }

        public static MapPinPlan Build(IReadOnlyList<PickerGroup> groups, PortalEntry? current, PortalEntry? returnHub = null,
            string? homeHubKey = null, bool accessModesEnabled = true, bool highlightStone = false)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            var home = HomeHub.Parse(homeHubKey);
            var pins = new List<MapPinSpec>();
            foreach (var group in groups)
                foreach (var row in group.Rows)
                {
                    bool isHome = home != null && row.Portal.IsHub && PortalKey.Of(row.Portal.Position) == home;
                    pins.Add(new MapPinSpec(row.Portal.IsHub ? MapPinKind.Hub : MapPinKind.Portal, row.Portal, row.Label, row.IsFavourite, isHome,
                        PortalAccessRules.EffectiveMode(row.Portal.Mode, accessModesEnabled), highlightStone && row.Portal.AllowsAllItems));
                }
            int destinations = pins.Count;
            int here = -1;
            if (current != null)
            {
                here = pins.Count;
                pins.Add(new MapPinSpec(MapPinKind.Here, current, PickerList.LabelOf(current), false, false,
                    PortalAccessRules.EffectiveMode(current.Mode, accessModesEnabled), highlightStone && current.AllowsAllItems));
            }
            pins.Add(new MapPinSpec(MapPinKind.Player, null, "", false));
            return new MapPinPlan(pins.AsReadOnly(), destinations, here, pins.Count - 1, returnHub);
        }

        /// <summary>The portal a click on pin <paramref name="index"/> travels to, or null (this portal, the player, out of range).</summary>
        public PortalEntry? DestinationAt(int index) => index >= 0 && index < DestinationCount ? Pins[index].Portal : null;

        /// <summary>The hub "Set as home hub" on pin <paramref name="index"/> would choose, or null (not a hub destination
        /// pin: a plain portal, this portal, the player, out of range).</summary>
        public PortalEntry? HomeCandidateAt(int index)
        {
            var portal = DestinationAt(index);
            return portal != null && HomeHub.CanSet(portal) ? portal : null;
        }

        /// <summary>The destination pin of a portal (a list selection shown on the map), or -1.</summary>
        public int IndexOf(PortalId id)
        {
            for (int i = 0; i < DestinationCount; i++)
                if (Pins[i].Portal!.Id.Equals(id)) return i;
            return -1;
        }
    }

    /// <summary>What the map pane draws under the pins.</summary>
    public enum MapImage
    {
        /// <summary>The game's map image with its fog of war over unexplored land.</summary>
        GameMapWithFog,

        /// <summary>The kit frame only, with the fog over unexplored land.</summary>
        PlainWithFog,

        /// <summary>The kit frame only.</summary>
        Plain,
    }

    public static class MapPaneRules
    {
        /// <summary>The explored mask hides pins only while Only Explored Portals is on; the list already leaves those
        /// portals out, so the map never shows a pin the list does not (A7).</summary>
        public static bool UsesExploredMask(bool onlyExploredPortals) => onlyExploredPortals;

        /// <summary>The game map always keeps its fog (it would reveal the whole world otherwise); Hide Unexplored Map
        /// decides only for the plain fallback map.</summary>
        public static MapImage Image(bool useGameMap, bool hideUnexplored) =>
            useGameMap ? MapImage.GameMapWithFog : hideUnexplored ? MapImage.PlainWithFog : MapImage.Plain;

        /// <summary>The game's map texture is encoded data, not a picture: it is drawn through a copy of the game's own map material
        /// (which also applies the fog), never as a plain texture.</summary>
        public static bool UsesGameMaterial(MapImage image) => image == MapImage.GameMapWithFog;

        /// <summary>Start Zoom normalized to its range (0.25 to 8, the kit's zoom range); non-finite gives the default.</summary>
        public static float StartZoom(float value) => ThresholdSettings.Map.StartZoom.Normalize(value);
    }
}
