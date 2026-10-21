// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: carry rule and cheat (design C1, C2, Q3) and optional fuel (approved PotalMap idea, behavior
// reference). The portal walked into decides for ores by default (game rule, U4); the Ignore Teleport
// Restrictions cheat also waives fuel (U10).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Config;

namespace ExtraPotions.Threshold.Routing
{
    public static class CarryRules
    {
        /// <summary>Whether a trip may start. <paramref name="inventoryTeleportable"/> is the game's own inventory check;
        /// <paramref name="enteredAllowsAllItems"/> is whether the portal walked into lets every item through (the game's
        /// stone portal). This overload preserves the default source-portal rule.</summary>
        public static bool Allowed(bool inventoryTeleportable, bool enteredAllowsAllItems, bool ignoreRestrictionsCheat) =>
            Allowed(inventoryTeleportable, enteredAllowsAllItems, false, OreTransportMode.FromStoneToWood, ignoreRestrictionsCheat);

        public static bool Allowed(bool inventoryTeleportable, bool enteredAllowsAllItems, bool destinationAllowsAllItems,
            OreTransportMode policy, bool ignoreRestrictionsCheat)
        {
            if (inventoryTeleportable || ignoreRestrictionsCheat) return true;
            switch (ThresholdSettings.General.OreTransport.Normalize(policy))
            {
                case OreTransportMode.Blocked: return false;
                case OreTransportMode.StoneToStone: return enteredAllowsAllItems && destinationAllowsAllItems;
                case OreTransportMode.All: return true;
                default: return enteredAllowsAllItems;
            }
        }

        /// <summary>The game's inventory answer with Teleport Listed Items (T17): when the game says no, the trip may still
        /// go if the cheat is in effect and every restricted item carried (<paramref name="restrictedCarried"/>, prefab
        /// names) is named in <paramref name="listedItems"/>. Names match exactly; no names keeps the game's answer.</summary>
        public static bool Teleportable(bool gameTeleportable, IEnumerable<string> restrictedCarried, bool listCheat, string listedItems)
        {
            if (gameTeleportable) return true;
            if (!listCheat || restrictedCarried == null) return false;
            var names = restrictedCarried.ToList();
            return names.Count > 0 && Unlisted(names, listedItems).Count == 0;
        }

        /// <summary>The restricted names not in the list, each once, in carried order (blank names count as unlisted).</summary>
        public static IReadOnlyList<string> Unlisted(IEnumerable<string> restrictedCarried, string listedItems)
        {
            var listed = new HashSet<string>(ThresholdSettings.NameList(listedItems ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var unlisted = new List<string>();
            foreach (var raw in restrictedCarried ?? Enumerable.Empty<string>())
            {
                var name = (raw ?? "").Trim();
                if (!listed.Contains(name) && seen.Add(name)) unlisted.Add(name);
            }
            return unlisted;
        }
    }

    public enum FuelOutcome { NotNeeded, Consume, NotEnough }

    public readonly struct FuelCheck
    {
        public FuelCheck(FuelOutcome outcome, int amount, int missing)
        {
            Outcome = outcome;
            Amount = amount;
            Missing = missing;
        }

        public FuelOutcome Outcome { get; }

        /// <summary>Items a trip takes.</summary>
        public int Amount { get; }

        /// <summary>Items short when <see cref="Outcome"/> is NotEnough.</summary>
        public int Missing { get; }
    }

    public static class FuelRules
    {
        /// <summary>Fuel for one Threshold-routed trip. <paramref name="ignoreRestrictionsCheat"/> is the cheat in effect
        /// (<see cref="ThresholdCheats.IgnoreTeleportRestrictions"/>); it waives fuel (U10).</summary>
        public static FuelCheck Check(bool enabled, int amount, int carried, bool ignoreRestrictionsCheat)
        {
            if (!enabled || ignoreRestrictionsCheat) return new FuelCheck(FuelOutcome.NotNeeded, 0, 0);
            int need = ThresholdSettings.Fuel.FuelAmount.Normalize(amount);
            int have = Math.Max(0, carried);
            return have >= need ? new FuelCheck(FuelOutcome.Consume, need, 0) : new FuelCheck(FuelOutcome.NotEnough, need, need - have);
        }
    }
}
