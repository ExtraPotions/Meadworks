// Adapted from TradersExtended 2.0.5 (shudnal), ItemLists/StorePanel.Lists.cs (FillSellableList, AddToSellList, CalculateSellPrice),
// pin 2d5dd879ead70f809f53025d6b28feb3dee7b605. Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: the offer grouping is a pure function of the held items and the price table (no inventory, UI or ObjectDB);
// an offer whose price cannot be computed is dropped instead of being shown with the largest integer as its price.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Barter.Trading
{
    /// <summary>One stack the player holds, as the planner sees it.</summary>
    public sealed class HeldItem
    {
        public HeldItem(object id, string prefab, int stack, int quality, int maxStack)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
            Stack = stack;
            Quality = quality;
            MaxStack = maxStack;
        }

        /// <summary>The plugin's handle for the item (its inventory entry); the planner never looks inside.</summary>
        public object Id { get; }

        public string Prefab { get; }

        public int Stack { get; }

        public int Quality { get; }

        public int MaxStack { get; }
    }

    /// <summary>Single: one item that does not stack. Stack: whole lots of a set size. Combined: every held item of a kind, sold by count.</summary>
    public enum SellKind
    {
        Single,
        Stack,
        Combined
    }

    public sealed class SellSettings
    {
        public SellSettings(float priceFactor, float qualityMultiplier)
        {
            PriceFactor = priceFactor;
            QualityMultiplier = qualityMultiplier;
        }

        /// <summary>The trader's sell factor for its current balance (see PriceCurve.SellFactor).</summary>
        public float PriceFactor { get; }

        /// <summary>Extra price per quality level above one for items without a quality of their own entry (0: none).</summary>
        public float QualityMultiplier { get; }
    }

    public sealed class SellOffer
    {
        private readonly List<object> sources = new List<object>();

        internal SellOffer(SellKind kind, string prefab, int stack, int amount, int quality, int pricePerItem, int price, string currency)
        {
            Kind = kind;
            Prefab = prefab;
            Stack = stack;
            Amount = amount;
            Quality = quality;
            PricePerItem = pricePerItem;
            Price = price;
            Currency = currency;
        }

        public SellKind Kind { get; }

        public string Prefab { get; }

        /// <summary>The lot size (1 for a single item and for a combined offer).</summary>
        public int Stack { get; internal set; }

        /// <summary>How many items the offer covers: 0 for a single item, else the total across the held stacks.</summary>
        public int Amount { get; internal set; }

        public int Quality { get; }

        /// <summary>The listed price per item or lot after the quality multiplier, before the trader's factor.</summary>
        public int PricePerItem { get; }

        /// <summary>The price of one lot (Single, Stack) or of everything (Combined) after the trader's factor.</summary>
        public int Price { get; internal set; }

        /// <summary>The currency item prefab the offer pays in.</summary>
        public string Currency { get; }

        /// <summary>The held items that make up the offer.</summary>
        public IReadOnlyList<object> Sources => sources;

        /// <summary>Whole lots available: 1 for a single item, amount over lot size otherwise.</summary>
        public int Lots => Kind == SellKind.Single ? 1 : Kind == SellKind.Combined ? Amount : Amount / Math.Max(Stack, 1);

        internal void AddSource(object source) => sources.Add(source);
    }

    public static class SellPlanner
    {
        /// <summary>
        /// Groups the held items into the offers the sell list shows. <paramref name="resolveCurrency"/> maps an entry's written
        /// currency (empty: the trader's) to the currency item prefab, or null when it cannot be resolved, which drops the offer.
        /// </summary>
        public static List<SellOffer> Plan(IEnumerable<HeldItem> held, SellPriceTable table, SellSettings settings, Func<string, string?> resolveCurrency)
        {
            var offers = new List<SellOffer>();
            foreach (HeldItem item in held)
            {
                IReadOnlyDictionary<int, SellPrice>? stackPrices = table.Lookup(item.Prefab, item.Quality, out int configuredQuality);
                if (stackPrices == null)
                    continue;

                foreach (int stack in stackPrices.Keys.OrderBy(x => x))
                {
                    SellPrice sellPrice = stackPrices[stack];
                    string? currency = resolveCurrency(sellPrice.Currency);
                    if (currency == null || string.Equals(item.Prefab, currency, StringComparison.Ordinal))
                        continue;

                    if (stack == 1)
                        Add(offers, item, item.MaxStack == 1 ? 1 : item.Stack, sellPrice.Price, settings, configuredQuality,
                            item.MaxStack == 1 ? SellKind.Single : SellKind.Combined, currency);
                    else
                        Add(offers, item, stack, sellPrice.Price, settings, configuredQuality, SellKind.Stack, currency);
                }
            }

            offers.RemoveAll(offer => offer.Amount != 0 && offer.Amount < offer.Stack);
            return offers;
        }

        private static void Add(List<SellOffer> offers, HeldItem item, int itemStack, int itemPrice, SellSettings settings,
            int configuredQuality, SellKind kind, string currency)
        {
            int effectiveQuality = configuredQuality > 0 ? configuredQuality : item.Quality;
            int adjustedPrice = itemPrice;
            if (kind != SellKind.Stack && settings.QualityMultiplier != 0 && configuredQuality == 0 && item.Quality > 1)
            {
                if (!TradeAmounts.TryGetQualityPrice(itemPrice, item.Quality, settings.QualityMultiplier, out adjustedPrice))
                    return;
            }

            if (!TradeAmounts.TryGetPrice(adjustedPrice, kind == SellKind.Combined ? itemStack : 1, settings.PriceFactor, out int price, roundUp: false))
                return;

            if (kind == SellKind.Single)
            {
                var single = new SellOffer(kind, item.Prefab, itemStack, 0, effectiveQuality, adjustedPrice, price, currency);
                single.AddSource(item.Id);
                offers.Add(single);
            }
            else if (kind == SellKind.Stack)
            {
                SellOffer? current = offers.Find(o => o.Kind == kind && o.Stack == itemStack && o.Prefab == item.Prefab &&
                                                      o.Quality == effectiveQuality && o.Price == price && o.Currency == currency);
                if (current != null)
                {
                    current.Amount = TradeAmounts.ClampBalance((long)current.Amount + item.Stack);
                    current.AddSource(item.Id);
                }
                else
                {
                    var created = new SellOffer(kind, item.Prefab, itemStack, item.Stack, effectiveQuality, adjustedPrice, price, currency);
                    created.AddSource(item.Id);
                    offers.Add(created);
                }
            }
            else
            {
                SellOffer? current = offers.Find(o => o.Kind == kind && o.Prefab == item.Prefab && o.Quality == effectiveQuality &&
                                                      o.Stack == 1 && o.PricePerItem == adjustedPrice && o.Currency == currency);
                if (current != null)
                {
                    int amount = TradeAmounts.ClampBalance((long)current.Amount + itemStack);
                    if (!TradeAmounts.TryGetPrice(current.PricePerItem, amount, settings.PriceFactor, out int combinedPrice, roundUp: false))
                    {
                        offers.Remove(current);
                        return;
                    }
                    current.Amount = amount;
                    current.Price = combinedPrice;
                    current.AddSource(item.Id);
                }
                else
                {
                    var created = new SellOffer(kind, item.Prefab, 1, itemStack, effectiveQuality, adjustedPrice, price, currency);
                    created.AddSource(item.Id);
                    offers.Add(created);
                }
            }
        }
    }
}
