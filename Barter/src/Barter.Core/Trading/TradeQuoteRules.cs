// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Barter.Availability;
using ExtraPotions.Barter.Lists;

namespace ExtraPotions.Barter.Trading
{
    /// <summary>Checks the complete quoted exchange against the authority's current lists and prices.</summary>
    public static class TradeQuoteRules
    {
        public static bool BuyMatches(IEnumerable<TradeEntry> entries, ITradeContext context, DiscoveryRule discovery,
            bool personalStateAvailable, string prefab, int amount, int quality, int price, string currency,
            int defaultQuality, float factor, Func<string, string?> resolveCurrency)
        {
            if (amount <= 0 || quality <= 0 || price <= 0 || string.IsNullOrEmpty(currency)) return false;
            return entries.Any(entry => entry.Prefab == prefab && entry.Stack == amount &&
                (entry.Quality > 0 ? entry.Quality : defaultQuality) == quality &&
                (personalStateAvailable || !NeedsPersonalKeys(entry)) && EntryAvailability.CanOffer(entry, context, discovery) &&
                resolveCurrency(entry.Currency) == currency &&
                TradeAmounts.TryGetPrice(entry.Price, 1, factor, out int current, roundUp: true) && current == price);
        }

        public static bool SellMatches(IEnumerable<TradeEntry> entries, ITradeContext context, bool personalStateAvailable,
            string prefab, int amount, int quality, int lotSize, int maxStack, int price, string currency,
            float factor, float qualityMultiplier, Func<string, string?> resolveCurrency)
        {
            if (amount <= 0 || quality <= 0 || lotSize <= 0 || maxStack <= 0 || price <= 0 || string.IsNullOrEmpty(currency)) return false;
            var table = SellPriceTable.Build(entries.Where(entry => personalStateAvailable || !NeedsPersonalKeys(entry)), context);
            // Reuse the displayed offer arithmetic, including per-lot rounding and quality fallback.
            var held = new[] { new HeldItem(new object(), prefab, amount, quality, maxStack) };
            return SellPlanner.Plan(held, table, new SellSettings(factor, qualityMultiplier), resolveCurrency).Any(offer =>
                offer.Stack == lotSize && offer.Price == price && offer.Currency == currency &&
                (offer.Kind == SellKind.Single ? amount == 1 : offer.Kind == SellKind.Stack ? amount == offer.Stack : amount == offer.Amount));
        }

        private static bool NeedsPersonalKeys(TradeEntry entry) =>
            TradeEntry.SplitKeys(entry.RequiredPlayerKey).Count != 0 || TradeEntry.SplitKeys(entry.NotRequiredPlayerKey).Count != 0;
    }
}
