// Adapted from TradersExtended 2.0.5 (shudnal), ItemLists/StorePanel.Lists.cs (AddItemToSellList, TryGetPriceKey, GetPriceKey,
// GetStackPrices), pin 2d5dd879ead70f809f53025d6b28feb3dee7b605. Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: built from entries and an ITradeContext instead of from ObjectDB, returned read-only.
using System;
using System.Collections.Generic;
using ExtraPotions.Barter.Availability;
using ExtraPotions.Barter.Lists;

namespace ExtraPotions.Barter.Trading
{
    /// <summary>What a trader pays for one lot of an item, and in which currency (empty: the trader's currency).</summary>
    public sealed class SellPrice
    {
        public SellPrice(int price, string currency)
        {
            Price = price;
            Currency = currency ?? string.Empty;
        }

        public int Price { get; }

        public string Currency { get; }
    }

    /// <summary>
    /// The prices a trader pays, by item, quality and lot size, from the common and the trader's own Sell list. An entry for
    /// the same item, quality and lot size that comes later replaces the earlier one, so a trader's list overrides the common list.
    /// </summary>
    public sealed class SellPriceTable
    {
        private readonly Dictionary<string, Dictionary<int, SellPrice>> prices;

        private SellPriceTable(Dictionary<string, Dictionary<int, SellPrice>> prices) => this.prices = prices;

        public static SellPriceTable Empty { get; } = new SellPriceTable(new Dictionary<string, Dictionary<int, SellPrice>>());

        public static SellPriceTable Build(IEnumerable<TradeEntry> entries, ITradeContext context)
        {
            var built = new Dictionary<string, Dictionary<int, SellPrice>>(StringComparer.Ordinal);
            foreach (TradeEntry entry in entries)
            {
                if (!EntryAvailability.CanPrice(entry, context))
                    continue;

                string key = Key(entry.Prefab, entry.Quality);
                if (!built.TryGetValue(key, out Dictionary<int, SellPrice>? stacks))
                {
                    stacks = new Dictionary<int, SellPrice>();
                    built.Add(key, stacks);
                }
                stacks[entry.Stack] = new SellPrice(entry.Price, entry.Currency);
            }
            return new SellPriceTable(built);
        }

        /// <summary>
        /// The lot prices for an item of the given quality: those written for exactly that quality, else those written for any
        /// quality. <paramref name="configuredQuality"/> is the quality the prices were written for (0: any).
        /// </summary>
        public IReadOnlyDictionary<int, SellPrice>? Lookup(string prefab, int itemQuality, out int configuredQuality)
        {
            configuredQuality = itemQuality;
            if (prices.TryGetValue(Key(prefab, itemQuality), out Dictionary<int, SellPrice>? exact))
                return exact;

            configuredQuality = 0;
            return prices.TryGetValue(Key(prefab, 0), out Dictionary<int, SellPrice>? any) ? any : null;
        }

        public int Count => prices.Count;

        private static string Key(string prefab, int quality) => prefab + "|" + quality;
    }
}
