// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the routing decision table of design 13.1. PortalSelector's documented hub and ReturnToMain
// behavior and the walk-in map picker of PotalMap and Wayfare are the references (behavior only). Every portal, tagged
// ones included, opens the map when walked into and a click travels (U1); the client holds the destination, there is no
// server-approved trip (U3); several hubs (Q1). Return To Hub is a map button that leads to the player's home hub
// (HomeHub) and is absent when none is usable.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Routing
{
    /// <summary>What the player does at a portal: Use (E), Use with the tag edit key (Shift + E), walk in, click a
    /// destination on the map that walking in opened (or close it without a pick), or press the map's Return To Hub
    /// button. The plugin asks <see cref="RouteTable.Decide"/> with <see cref="ReturnToHub"/> when it opens the map: a
    /// <see cref="RouteKind.TravelToHub"/> answer shows the button (with that hub), anything else hides it.</summary>
    public enum PortalAction { Use, UseTagEdit, Enter, Pick, ReturnToHub }

    public enum RouteKind { Vanilla, OpenPicker, TravelToDestination, TravelToHub, Refused }

    public enum RouteRefusal { None, NoAccess, SamePortal }

    /// <summary>Everything the table reads. <see cref="Destination"/> is the portal this player clicked on the map (held by
    /// the player's client, design section 3 and U3), or null; only <see cref="PortalAction.Pick"/> reads it.
    /// <see cref="HomeHubKey"/> is the player's stored home hub in this world (<see cref="HomeHub"/>), or null; only
    /// <see cref="PortalAction.ReturnToHub"/> reads it.</summary>
    public sealed class RouteFacts
    {
        public RouteFacts(PortalAction action, bool productActive, PortalEntry portal, bool hasVanillaPair, ReturnToHubMode returnToHub,
            PortalEntry? destination, IReadOnlyList<PortalEntry> world, AccessViewer viewer, bool accessModesEnabled,
            string? homeHubKey = null)
        {
            Action = action;
            ProductActive = productActive;
            Portal = portal ?? throw new ArgumentNullException(nameof(portal));
            HasVanillaPair = hasVanillaPair;
            ReturnToHub = returnToHub;
            Destination = destination;
            World = world ?? Array.Empty<PortalEntry>();
            Viewer = viewer;
            AccessModesEnabled = accessModesEnabled;
            HomeHubKey = homeHubKey;
        }

        public PortalAction Action { get; }
        public bool ProductActive { get; }
        public PortalEntry Portal { get; }
        public bool HasVanillaPair { get; }
        public ReturnToHubMode ReturnToHub { get; }
        public PortalEntry? Destination { get; }
        public IReadOnlyList<PortalEntry> World { get; }
        public AccessViewer Viewer { get; }
        public bool AccessModesEnabled { get; }
        public string? HomeHubKey { get; }
    }

    public readonly struct Route
    {
        private Route(RouteKind kind, PortalEntry? target, RouteRefusal refusal)
        {
            Kind = kind;
            Target = target;
            Refusal = refusal;
        }

        public RouteKind Kind { get; }
        public PortalEntry? Target { get; }
        public RouteRefusal Refusal { get; }

        public static Route Vanilla => new Route(RouteKind.Vanilla, null, RouteRefusal.None);
        public static Route Picker => new Route(RouteKind.OpenPicker, null, RouteRefusal.None);
        public static Route To(PortalEntry target) => new Route(RouteKind.TravelToDestination, target, RouteRefusal.None);
        public static Route ToHub(PortalEntry hub) => new Route(RouteKind.TravelToHub, hub, RouteRefusal.None);
        public static Route Refuse(RouteRefusal why) => new Route(RouteKind.Refused, null, why);

        public override string ToString() => Kind + (Target != null ? " " + Target : "") + (Refusal != RouteRefusal.None ? " (" + Refusal + ")" : "");
    }

    public static class RouteTable
    {
        public static Route Decide(RouteFacts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            if (!facts.ProductActive) return Route.Vanilla;
            var portal = facts.Portal;
            switch (facts.Action)
            {
                case PortalAction.Use:
                case PortalAction.UseTagEdit:
                    return Route.Vanilla;
                case PortalAction.Pick:
                    if (facts.Destination == null) return Route.Vanilla;
                    if (facts.Destination.Id.Equals(portal.Id)) return Route.Refuse(RouteRefusal.SamePortal);
                    if (!PortalAccessRules.MayTarget(facts.Destination, facts.Viewer, facts.AccessModesEnabled)) return Route.Refuse(RouteRefusal.NoAccess);
                    return Route.To(facts.Destination);
                case PortalAction.Enter:
                    return Route.Picker;
            }
            if (portal.IsHub) return Route.Vanilla;
            switch (facts.ReturnToHub)
            {
                case ReturnToHubMode.Always:
                    break;
                case ReturnToHubMode.UnpairedOnly:
                    if (facts.HasVanillaPair) return Route.Vanilla;
                    break;
                default:
                    return Route.Vanilla;
            }
            var hub = HomeHub.Resolve(facts.HomeHubKey, portal, facts.World, facts.Viewer, facts.AccessModesEnabled).Hub;
            return hub == null ? Route.Vanilla : Route.ToHub(hub);
        }
    }
}
