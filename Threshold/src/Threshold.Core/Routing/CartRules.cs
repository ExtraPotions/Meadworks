// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Bring Carts. The idea was seen in OdinPlus TeleportEverything / TeleportCarts (no license found;
// clean-room, built from its one-line feature description only). The cart's load follows the same carry rule as the
// traveler's own inventory (CarryRules, Ore Transport): by default restricted items pass only from a stone portal.
using System;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Routing
{
    /// <summary>What happens to the cart the traveler pulls when a routed trip starts.</summary>
    public enum CartOutcome
    {
        /// <summary>No cart attached.</summary>
        None,
        /// <summary>The cart comes along.</summary>
        Bring,
        /// <summary>Bring Carts is off (or left to TeleportEverything): the cart stays, as in the game.</summary>
        StayOff,
        /// <summary>This client does not own the cart or its box is open: the cart stays, nothing in it is touched.</summary>
        StayBusy,
        /// <summary>The cart's load may not pass this portal: the trip is refused.</summary>
        Refuse,
    }

    /// <summary>Everything the cart check reads. <paramref name="loadHardBlocked"/>: an item in the cart the game never lets
    /// through any portal; <paramref name="loadTeleportable"/>: the game's inventory check on the cart's load with only the
    /// world's rules (and Teleport Listed Items) applied.</summary>
    public readonly struct CartFacts
    {
        public CartFacts(bool attached, bool enabled, bool ownedAndClosed, bool loadHardBlocked, bool loadTeleportable,
            bool enteredAllowsAllItems, bool destinationAllowsAllItems, OreTransportMode oreTransport, bool ignoreRestrictionsCheat)
        {
            Attached = attached;
            Enabled = enabled;
            OwnedAndClosed = ownedAndClosed;
            LoadHardBlocked = loadHardBlocked;
            LoadTeleportable = loadTeleportable;
            EnteredAllowsAllItems = enteredAllowsAllItems;
            DestinationAllowsAllItems = destinationAllowsAllItems;
            OreTransport = oreTransport;
            IgnoreRestrictionsCheat = ignoreRestrictionsCheat;
        }

        public bool Attached { get; }
        public bool Enabled { get; }
        public bool OwnedAndClosed { get; }
        public bool LoadHardBlocked { get; }
        public bool LoadTeleportable { get; }
        public bool EnteredAllowsAllItems { get; }
        public bool DestinationAllowsAllItems { get; }
        public OreTransportMode OreTransport { get; }
        public bool IgnoreRestrictionsCheat { get; }
    }

    public static class CartRules
    {
        /// <summary>Meters in front of the exit point (the destination's facing) where the cart is set down.</summary>
        public const float Ahead = 5f;

        /// <summary>Meters above the floor found under the cart's spot it is set down at, so its wheels do not start in the ground.</summary>
        public const float Lift = 0.3f;

        public static CartOutcome Decide(CartFacts facts)
        {
            if (!facts.Attached) return CartOutcome.None;
            if (!facts.Enabled) return CartOutcome.StayOff;
            if (!facts.OwnedAndClosed) return CartOutcome.StayBusy;
            if (facts.LoadHardBlocked || !CarryRules.Allowed(facts.LoadTeleportable, facts.EnteredAllowsAllItems,
                facts.DestinationAllowsAllItems, facts.OreTransport, facts.IgnoreRestrictionsCheat))
                return CartOutcome.Refuse;
            return CartOutcome.Bring;
        }

        /// <summary>Where the cart is set down: <see cref="Ahead"/> in front of <paramref name="anchor"/> along the
        /// destination's facing <paramref name="yaw"/> (degrees), at the anchor's height.</summary>
        public static MapPoint Place(MapPoint anchor, float yaw)
        {
            double radians = yaw * Math.PI / 180.0;
            return new MapPoint((float)(anchor.X + Math.Sin(radians) * Ahead), anchor.Y, (float)(anchor.Z + Math.Cos(radians) * Ahead));
        }

        /// <summary>The cart's final place: on <paramref name="floor"/> under its spot (plus <see cref="Lift"/>) when that floor
        /// is within a step of where the traveler landed; otherwise its spot a little above the traveler's height, from where
        /// it settles by itself. Never on the traveler.</summary>
        public static MapPoint Settle(MapPoint spot, MapPoint traveller, float? floor)
        {
            if (floor is float y && !float.IsNaN(y) && !float.IsInfinity(y)
                && y <= traveller.Y + TameRules.MaxStepUp && y >= traveller.Y - TameRules.MaxStepDown)
                return new MapPoint(spot.X, y + Lift, spot.Z);
            return new MapPoint(spot.X, traveller.Y + 0.5f, spot.Z);
        }
    }
}
