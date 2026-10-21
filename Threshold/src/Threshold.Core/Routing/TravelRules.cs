// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the trip check the player's client makes when a destination is clicked or Return To Hub is
// pressed (T12; U3: the client stores the destination, no server-approved trip). The world's portal blocks and the
// exit point follow the game's own TeleportWorld.Teleport (read with ILSpy, not copied): the no-portals and boss keys
// block, and the exit stands the portal's exit distance in front of the destination and a meter up. Carry follows the
// entry portal by default (U4), or the server's ore policy, with the game's hard block kept; fuel follows FuelRules (U10).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Routing
{
    public enum TravelRefusal { None, NoDestination, SamePortal, NoAccess, WorldBlocked, BossBlocked, Carry, Fuel }

    /// <summary>Everything the trip check reads. <paramref name="inventoryHardBlocked"/>: an item the game never lets
    /// through any portal (its inventory check fails even with every item allowed); <paramref name="inventoryTeleportable"/>:
    /// the game's own inventory check with only the world's rules (the TeleportAll key) applied.</summary>
    public sealed class TravelFacts
    {
        public TravelFacts(Route route, bool noPortalsKey, bool bossBlocked, bool inventoryHardBlocked, bool inventoryTeleportable,
            bool enteredAllowsAllItems, bool ignoreRestrictionsCheat, bool fuelEnabled, int fuelAmount, int fuelCarried)
            : this(route, noPortalsKey, bossBlocked, inventoryHardBlocked, inventoryTeleportable, enteredAllowsAllItems,
                ignoreRestrictionsCheat, fuelEnabled, fuelAmount, fuelCarried, OreTransportMode.FromStoneToWood)
        {
        }

        public TravelFacts(Route route, bool noPortalsKey, bool bossBlocked, bool inventoryHardBlocked, bool inventoryTeleportable,
            bool enteredAllowsAllItems, bool ignoreRestrictionsCheat, bool fuelEnabled, int fuelAmount, int fuelCarried, OreTransportMode oreTransport)
        {
            Route = route;
            NoPortalsKey = noPortalsKey;
            BossBlocked = bossBlocked;
            InventoryHardBlocked = inventoryHardBlocked;
            InventoryTeleportable = inventoryTeleportable;
            EnteredAllowsAllItems = enteredAllowsAllItems;
            IgnoreRestrictionsCheat = ignoreRestrictionsCheat;
            FuelEnabled = fuelEnabled;
            FuelAmount = fuelAmount;
            FuelCarried = fuelCarried;
            OreTransport = oreTransport;
        }

        public Route Route { get; }
        public bool NoPortalsKey { get; }
        public bool BossBlocked { get; }
        public bool InventoryHardBlocked { get; }
        public bool InventoryTeleportable { get; }
        public bool EnteredAllowsAllItems { get; }
        public bool IgnoreRestrictionsCheat { get; }
        public bool FuelEnabled { get; }
        public int FuelAmount { get; }
        public int FuelCarried { get; }
        public OreTransportMode OreTransport { get; }
    }

    public readonly struct TravelPlan
    {
        public TravelPlan(TravelRefusal refusal, PortalEntry? target, FuelCheck fuel)
        {
            Refusal = refusal;
            Target = target;
            Fuel = fuel;
        }

        public TravelRefusal Refusal { get; }

        /// <summary>The destination when the trip goes.</summary>
        public PortalEntry? Target { get; }

        /// <summary>Fuel to take once the game accepted the teleport (<see cref="FuelOutcome.Consume"/>), or why not.</summary>
        public FuelCheck Fuel { get; }

        public bool Go => Refusal == TravelRefusal.None && Target != null;
    }

    public static class TravelRules
    {
        public static PortalEntry? CurrentTarget(PortalEntry selected, IEnumerable<PortalEntry> current) =>
            current.FirstOrDefault(portal => portal.Id.Equals(selected.Id));

        public static TravelPlan Decide(TravelFacts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var none = new FuelCheck(FuelOutcome.NotNeeded, 0, 0);
            var route = facts.Route;
            switch (route.Kind)
            {
                case RouteKind.Refused:
                    return new TravelPlan(route.Refusal == RouteRefusal.SamePortal ? TravelRefusal.SamePortal : TravelRefusal.NoAccess, null, none);
                case RouteKind.TravelToDestination:
                case RouteKind.TravelToHub:
                    if (route.Target == null) return new TravelPlan(TravelRefusal.NoDestination, null, none);
                    break;
                default:
                    return new TravelPlan(TravelRefusal.NoDestination, null, none);
            }
            if (facts.NoPortalsKey) return new TravelPlan(TravelRefusal.WorldBlocked, null, none);
            if (facts.BossBlocked) return new TravelPlan(TravelRefusal.BossBlocked, null, none);
            if (facts.InventoryHardBlocked || !CarryRules.Allowed(facts.InventoryTeleportable, facts.EnteredAllowsAllItems,
                route.Target!.AllowsAllItems, facts.OreTransport, facts.IgnoreRestrictionsCheat))
                return new TravelPlan(TravelRefusal.Carry, null, none);
            var fuel = FuelRules.Check(facts.FuelEnabled, facts.FuelAmount, facts.FuelCarried, facts.IgnoreRestrictionsCheat);
            if (fuel.Outcome == FuelOutcome.NotEnough) return new TravelPlan(TravelRefusal.Fuel, null, fuel);
            return new TravelPlan(TravelRefusal.None, route.Target, fuel);
        }

        /// <summary>The game's boss rule: with the no-boss-portals key, a running boss event or any active boss blocks.</summary>
        public static bool BossBlocked(bool noBossPortalsKey, bool bossEvent, float activeBosses) =>
            noBossPortalsKey && (bossEvent || activeBosses > 0f);

        /// <summary>Where the traveler lands: <paramref name="exitDistance"/> in front of the destination (its facing
        /// <paramref name="yaw"/>, degrees) and a meter up, as the game's own portal does.</summary>
        public static MapPoint ExitPoint(MapPoint destination, float yaw, float exitDistance)
        {
            double radians = yaw * Math.PI / 180.0;
            return new MapPoint((float)(destination.X + Math.Sin(radians) * exitDistance), destination.Y + 1f,
                (float)(destination.Z + Math.Cos(radians) * exitDistance));
        }

        /// <summary>The message for a refusal: a Threshold key, or the game's own token for the world's blocks.</summary>
        public static string TextKey(TravelRefusal refusal)
        {
            switch (refusal)
            {
                case TravelRefusal.NoAccess: return ThresholdTextKeys.Refusal(RouteRefusal.NoAccess);
                case TravelRefusal.SamePortal: return ThresholdTextKeys.Refusal(RouteRefusal.SamePortal);
                case TravelRefusal.WorldBlocked: return "$msg_blocked";
                case TravelRefusal.BossBlocked: return "$msg_blockedbyboss";
                case TravelRefusal.Carry: return ThresholdTextKeys.CarryBlocked;
                case TravelRefusal.Fuel: return ThresholdTextKeys.FuelNotEnough;
                default: return "";
            }
        }
    }
}
