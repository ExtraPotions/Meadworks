// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the player's home hub (user decision, Threshold follow-up 2 of 2026-10-08). The map's Return To
// Hub button leads to a hub each player chooses, stored per world in the character's custom data (as Cairn stores its
// per-world marker choices). Follow-up 3: without a usable home hub there is no button (no nearest-hub fallback); the
// saved choice is kept, so the button returns once that hub is usable again.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Routing
{
    /// <summary>Why Return To Hub has (Home) or has no target: no home hub chosen (None), or the chosen one is gone, no
    /// longer a hub or not usable by this player (Unusable; the saved choice stays).</summary>
    public enum HomeHubSource { None, Home, Unusable }

    public readonly struct HomeHubPick
    {
        public HomeHubPick(PortalEntry? hub, HomeHubSource source)
        {
            Hub = hub;
            Source = source;
        }

        public PortalEntry? Hub { get; }
        public HomeHubSource Source { get; }
    }

    public static class HomeHub
    {
        /// <summary><c>Player.m_customData</c> key of the home hub in the world <paramref name="worldId"/>
        /// (<c>ZNet.GetWorldUID</c>).</summary>
        public static string DataKey(long worldId) =>
            ThresholdCore.CharacterDataPrefix + "homeHub." + worldId.ToString(CultureInfo.InvariantCulture);

        /// <summary>Only a hub can be chosen as home.</summary>
        public static bool CanSet(PortalEntry portal) => portal != null && portal.IsHub;

        /// <summary>The stored value: the hub's position key.</summary>
        public static string Format(PortalEntry hub)
        {
            if (!CanSet(hub)) throw new ArgumentException("Only a hub can be a home hub.", nameof(hub));
            return PortalKey.Of(hub.Position);
        }

        /// <summary>The stored position key, or null for nothing or anything that is not a key.</summary>
        public static string? Parse(string? raw)
        {
            var key = raw?.Trim();
            return PortalKey.IsKey(key) ? key : null;
        }

        /// <summary>The Return To Hub target from <paramref name="portal"/>: the home hub while it still exists, is a hub
        /// and the player may use it; otherwise none (no button).</summary>
        public static HomeHubPick Resolve(string? homeKey, PortalEntry portal, IEnumerable<PortalEntry> world, AccessViewer viewer, bool accessModesEnabled)
        {
            if (portal == null) throw new ArgumentNullException(nameof(portal));
            var usable = (world ?? Enumerable.Empty<PortalEntry>())
                .Where(p => p.IsHub && !p.Id.Equals(portal.Id) && PortalAccessRules.MayTarget(p, viewer, accessModesEnabled))
                .ToList();
            var key = Parse(homeKey);
            if (key == null) return new HomeHubPick(null, HomeHubSource.None);
            var home = usable.FirstOrDefault(p => PortalKey.Of(p.Position) == key);
            return home != null ? new HomeHubPick(home, HomeHubSource.Home) : new HomeHubPick(null, HomeHubSource.Unusable);
        }
    }
}
