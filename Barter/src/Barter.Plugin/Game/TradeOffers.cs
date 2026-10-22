// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behaviour adapted from TradersExtended 2.0.5 (Unlicense, pin 2d5dd879) ItemLists/StorePanel.Lists.cs (the Trader.GetAvailableItems
// finalizer): list entries become trade items, vanilla offers can be removed, prices follow the trader's purse. The code is Barter's;
// a purchase's quality and currency live in a side table instead of being encoded into the vanilla trade item's stack.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ExtraPotions.Barter.Availability;
using ExtraPotions.Barter.Config;
using ExtraPotions.Barter.Currency;
using ExtraPotions.Barter.Lists;
using ExtraPotions.Barter.Trading;
using UnityEngine;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>What Barter knows about a trade item it created or priced.</summary>
    internal sealed class OfferInfo
    {
        /// <summary>The list entry, null for a re-priced copy of a vanilla offer.</summary>
        internal TradeEntry Entry;

        /// <summary>The offer this one stands for: the list entry, or the vanilla trade item it was copied from.</summary>
        internal object Origin;

        internal string Trader;

        /// <summary>The currency prefab the offer is paid in; empty is the trader's own currency.</summary>
        internal string CurrencyName = string.Empty;

        /// <summary>The quality bought; 0 is the item's own quality.</summary>
        internal int Quality;

        internal bool FromList => Entry != null;
    }

    internal static class TradeOffers
    {
        private sealed class Built
        {
            internal int Percent;
            internal Trader.TradeItem Item;
        }

        private sealed class Scaled
        {
            internal int Percent;
            internal Trader.TradeItem Item;
        }

        private static readonly ConditionalWeakTable<Trader.TradeItem, OfferInfo> infos = new ConditionalWeakTable<Trader.TradeItem, OfferInfo>();
        private static readonly ConditionalWeakTable<Trader.TradeItem, Scaled> scaled = new ConditionalWeakTable<Trader.TradeItem, Scaled>();
        private static readonly Dictionary<TradeEntry, Dictionary<string, Built>> built = new Dictionary<TradeEntry, Dictionary<string, Built>>();

        /// <summary>The lists changed: offers are made again from the new entries.</summary>
        internal static void Reset() => built.Clear();

        internal static OfferInfo InfoOf(Trader.TradeItem item) =>
            item != null && infos.TryGetValue(item, out OfferInfo info) ? info : null;

        /// <summary>The offer a trade item stands for, so a selection survives its price changing.</summary>
        internal static object OriginOf(Trader.TradeItem item) => InfoOf(item)?.Origin ?? item;

        /// <summary>The currency item an offer is paid in.</summary>
        internal static ItemDrop CurrencyOf(OfferInfo info, StoreGui store)
        {
            ItemDrop trader = store != null && store.m_coinPrefab != null ? store.m_coinPrefab : Currencies.Vanilla();
            return info == null || string.IsNullOrEmpty(info.CurrencyName) ? trader : Currencies.Resolve(info.CurrencyName, trader);
        }

        /// <summary>True when the offer is paid in the trader's own currency, which is the one its purse is counted in.</summary>
        internal static bool PaysTraderCurrency(OfferInfo info, StoreGui store)
        {
            if (info == null)
                return true;
            ItemDrop paid = CurrencyOf(info, store);
            ItemDrop own = store != null && store.m_coinPrefab != null ? store.m_coinPrefab : Currencies.Vanilla();
            return paid == null || own == null || paid.m_itemData.m_shared.m_name == own.m_itemData.m_shared.m_name;
        }

        /// <summary>Applies Barter's rules to the list the game built for a trader: removals, list entries, then purse pricing.</summary>
        internal static void Apply(Trader trader, ref List<Trader.TradeItem> result)
        {
            if (trader == null || Player.m_localPlayer == null || Plugin.Settings == null)
                return;
            if (result == null)
                result = new List<Trader.TradeItem>();

            string name = TradeWorld.TraderName(trader);
            bool disableVanilla = Plugin.Settings.GetFor(BarterSettings.Lists.DisableVanillaItems, name);
            bool disableOthers = Plugin.Settings.GetFor(BarterSettings.Lists.DisableOtherModsItems, name);
            if (disableOthers)
            {
                result.Clear();
                foreach (Trader.TradeItem item in trader.m_items ?? new List<Trader.TradeItem>())
                    if (item != null && (!disableVanilla || HasKeyReward(item)) && IsAvailable(item))
                        result.Add(item);
            }
            else if (disableVanilla)
            {
                var vanilla = new HashSet<Trader.TradeItem>((trader.m_items ?? new List<Trader.TradeItem>()).Where(item => item != null && !HasKeyReward(item)));
                result.RemoveAll(item => item == null || vanilla.Contains(item));
            }

            float factor = Purses.BuyFactor(trader);
            int percent = (int)Math.Round(factor * 100d);
            AddEntries(trader, name, factor, percent, result);

            if (percent != 100)
                for (int i = result.Count - 1; i >= 0; i--)
                {
                    Trader.TradeItem priced = Rescale(result[i], factor, percent);
                    if (priced == null)
                        result.RemoveAt(i);
                    else
                        result[i] = priced;
                }
        }

        private static void AddEntries(Trader trader, string name, float factor, int percent, List<Trader.TradeItem> result)
        {
            TradeBook book = TradeLists.Effective;
            if (!TradeLists.Ready)
                return;

            var ignored = new HashSet<string>(TraderNames.SplitList(Plugin.Settings.GetFor(BarterSettings.Lists.AlwaysOffered, name)), StringComparer.OrdinalIgnoreCase);
            foreach (Trader.TradeItem item in trader.m_items ?? new List<Trader.TradeItem>())
                if (item != null && item.m_prefab != null)
                    ignored.Add(item.m_prefab.name);
            var discovery = new DiscoveryRule(Plugin.Settings.GetFor(BarterSettings.Lists.SellOnlyDiscovered, name), ignored);
            string traderCurrency = Currencies.NameFor(name);

            foreach (TradeEntry entry in book.Buy(name))
            {
                if (!EntryAvailability.CanOffer(entry, TradeWorld.Instance, discovery))
                    continue;
                Trader.TradeItem offer = Offer(entry, name, traderCurrency, factor, percent);
                if (offer != null)
                    result.Add(offer);
            }
        }

        private static Trader.TradeItem Offer(TradeEntry entry, string trader, string traderCurrency, float factor, int percent)
        {
            if (!built.TryGetValue(entry, out Dictionary<string, Built> byTrader))
                built.Add(entry, byTrader = new Dictionary<string, Built>(StringComparer.Ordinal));
            if (byTrader.TryGetValue(trader, out Built existing) && existing.Percent == percent && existing.Item.m_prefab != null)
                return existing.Item;

            ItemDrop drop = TradeWorld.Item(entry.Prefab);
            if (drop == null || drop.m_itemData?.m_shared == null || !TradeAmounts.TryGetPrice(entry.Price, 1, factor, out int price, roundUp: true))
                return null;

            var item = new Trader.TradeItem
            {
                m_prefab = drop,
                m_stack = entry.Stack,
                m_price = price,
                m_requiredGlobalKey = entry.RequiredGlobalKey ?? string.Empty,
                m_name = drop.m_itemData.m_shared.m_name ?? string.Empty,
                m_icon = drop.m_itemData.m_shared.m_icons?.FirstOrDefault(),
                // Empty selects StoreGui's native item tooltip, including item stats and other mods' details.
                m_tooltip = string.Empty,
                m_buyKey = string.Empty,
                m_incrementKey = string.Empty,
                m_buyPlayerEffects = new EffectList(),
            };
            infos.Remove(item);
            infos.Add(item, new OfferInfo
            {
                Entry = entry,
                Origin = entry,
                Trader = trader,
                Quality = entry.Quality,
                CurrencyName = CurrencyRules.Effective(entry.Currency, traderCurrency),
            });
            byTrader[trader] = new Built { Percent = percent, Item = item };
            return item;
        }

        /// <summary>A copy of a vanilla offer at the purse price; list offers were priced when made. Null drops an offer whose price cannot be computed.</summary>
        private static Trader.TradeItem Rescale(Trader.TradeItem item, float factor, int percent)
        {
            if (item == null)
                return null;
            OfferInfo info = InfoOf(item);
            if (info != null || item.m_price <= 0)
                return item;

            if (scaled.TryGetValue(item, out Scaled existing) && existing.Percent == percent)
                return existing.Item;

            if (!TradeAmounts.TryGetPrice(item.m_price, 1, factor, out int price, roundUp: true))
                return null;

            Trader.TradeItem copy = Clone(item);
            copy.m_price = price;
            infos.Add(copy, new OfferInfo { Origin = item });
            scaled.Remove(item);
            scaled.Add(item, new Scaled { Percent = percent, Item = copy });
            return copy;
        }

        private static Trader.TradeItem Clone(Trader.TradeItem item) => new Trader.TradeItem
        {
            m_prefab = item.m_prefab,
            m_stack = item.m_stack,
            m_price = item.m_price,
            m_requiredGlobalKey = item.m_requiredGlobalKey,
            m_icon = item.m_icon,
            m_name = item.m_name,
            m_tooltip = item.m_tooltip,
            m_buyKey = item.m_buyKey,
            m_incrementKey = item.m_incrementKey,
            m_incrementAmount = item.m_incrementAmount,
            m_buyPlayerEffects = item.m_buyPlayerEffects,
            m_levelUpEffect = item.m_levelUpEffect,
        };

        private static bool HasKeyReward(Trader.TradeItem item) => !string.IsNullOrEmpty(item?.m_buyKey);

        /// <summary>The same test the game's own list applies: the world key is set and a bought key is not owned yet.</summary>
        private static bool IsAvailable(Trader.TradeItem item) =>
            (string.IsNullOrEmpty(item.m_requiredGlobalKey) || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(item.m_requiredGlobalKey))) &&
            (string.IsNullOrEmpty(item.m_buyKey) || !Player.m_localPlayer.HaveUniqueKey(item.m_buyKey));
    }
}
