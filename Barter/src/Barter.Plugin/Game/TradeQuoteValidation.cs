// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.Linq;
using ExtraPotions.Barter.Availability;
using ExtraPotions.Barter.Config;
using ExtraPotions.Barter.Currency;
using ExtraPotions.Barter.Trading;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Barter.Runtime
{
    internal static class TradeQuoteValidation
    {
        internal static bool TryValidate(AuthorityContext context, TradeRequest request, Trader trader, ZDO traderZdo, out long purseDelta, out string reason)
        {
            purseDelta = 0;
            reason = "TradeUnavailable";
            if (!Plugin.Active || !TradeLists.Ready || trader == null || traderZdo == null || Plugin.Settings == null || ZNet.instance == null || !ZNet.instance.IsServer()) return false;
            if (!TraderAuthority.TryValidate(traderZdo, out _)) return false;
            if (!TryActor(context, traderZdo, out Player localPlayer)) return false;
            if (request.Amount <= 0 || request.Quality <= 0 || request.LotSize <= 0 || request.Price <= 0) return false;
            if (request.Service != 0)
            {
                ZDO zdo = ZDOMan.instance?.GetZDO(new ZDOID(request.TraderUser, request.TraderId));
                bool evidence = context.IsLocal && (request.Service == 1
                    ? TradeAuthority.HasCharacterReceipt(request.Receipt)
                    : TradeEvidence.MatchesInventory(request, false));
                bool valid = zdo != null && TradeFeatures.Validate(context.ActorId, request, trader, zdo, out purseDelta, evidence);
                reason = valid ? string.Empty : "ServiceQuoteChanged";
                return valid;
            }

            ItemDrop item = TradeWorld.Item(request.Prefab);
            if (item == null || item.m_itemData.m_shared.m_maxStackSize <= 0 || request.Quality > item.m_itemData.m_shared.m_maxQuality) return false;
            string name = TradeWorld.TraderName(trader);
            ItemDrop ownCurrency = Currencies.OfTrader(name);
            if (ownCurrency == null) return false;
            string own = Utils.GetPrefabName(ownCurrency.gameObject);
            string Resolve(string raw)
            {
                ItemDrop resolved = Currencies.Resolve(CurrencyRules.Effective(raw, Currencies.NameFor(name)), ownCurrency);
                return resolved == null ? null : Utils.GetPrefabName(resolved.gameObject);
            }

            bool matches;
            if (request.Vanilla)
            {
                matches = request.Currency == own && (request.Buy
                    ? request.LotSize == request.Amount && NativeBuyMatches(request, trader, traderZdo, item)
                    : request.LotSize == 1 && request.Amount <= item.m_itemData.m_shared.m_maxStackSize &&
                      item.m_itemData.m_shared.m_value > 0 && request.Prefab != own &&
                      (long)item.m_itemData.m_shared.m_value * request.Amount == request.Price);
            }
            else if (request.Buy)
            {
                var ignored = new System.Collections.Generic.HashSet<string>(ExtraPotions.Barter.Lists.TraderNames.SplitList(
                    Plugin.Settings.GetFor(BarterSettings.Lists.AlwaysOffered, name)), StringComparer.OrdinalIgnoreCase);
                foreach (Trader.TradeItem native in trader.m_items)
                    if (native?.m_prefab != null) ignored.Add(Utils.GetPrefabName(native.m_prefab.gameObject));
                var discovery = new DiscoveryRule(Plugin.Settings.GetFor(BarterSettings.Lists.SellOnlyDiscovered, name), ignored);
                matches = request.LotSize == request.Amount && TradeQuoteRules.BuyMatches(TradeLists.Effective.Buy(name),
                    new Context(localPlayer), discovery, localPlayer != null, request.Prefab, request.Amount, request.Quality,
                    request.Price, request.Currency, item.m_itemData.m_quality, Purses.BuyFactor(trader, traderZdo), Resolve);
            }
            else
                matches = TradeQuoteRules.SellMatches(TradeLists.Effective.Sell(name, Plugin.Settings.GetFor(BarterSettings.Lists.CommonValuables, name)),
                    new Context(localPlayer), localPlayer != null, request.Prefab, request.Amount, request.Quality, request.LotSize,
                    item.m_itemData.m_shared.m_maxStackSize, request.Price, request.Currency, Purses.SellFactor(trader, traderZdo),
                    Plugin.Settings.GetFor(BarterSettings.Lists.QualityPriceMultiplier, name), Resolve);
            if (!matches) { reason = "QuoteChangedOrUnsupported"; return false; }
            if (Purses.Applies(trader) && request.Currency == own) purseDelta = request.Buy ? request.Price : -(long)request.Price;
            reason = string.Empty;
            return true;
        }

        private static bool NativeBuyMatches(TradeRequest request, Trader trader, ZDO traderZdo, ItemDrop item)
        {
            if (Plugin.Settings.GetFor(BarterSettings.Lists.DisableVanillaItems, TradeWorld.TraderName(trader))) return false;
            return trader.m_items.Any(native => native?.m_prefab == item && string.IsNullOrEmpty(native.m_buyKey) &&
                string.IsNullOrEmpty(native.m_incrementKey) &&
                (string.IsNullOrEmpty(native.m_requiredGlobalKey) || ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(native.m_requiredGlobalKey)) &&
                Math.Min(native.m_stack, item.m_itemData.m_shared.m_maxStackSize) == request.Amount && item.m_itemData.m_quality == request.Quality &&
                TradeAmounts.TryGetPrice(native.m_price, 1, Purses.BuyFactor(trader, traderZdo), out int price, roundUp: true) && request.Price == price);
        }

        private static bool TryActor(AuthorityContext context, ZDO traderZdo, out Player localPlayer)
        {
            localPlayer = null;
            if (context == null || !context.Session.IsAuthority || !context.Session.AuthorityReady) return false;
            Vector3 position;
            if (context.IsLocal)
            {
                localPlayer = Player.m_localPlayer;
                if (localPlayer == null || localPlayer.GetPlayerID().ToString(CultureInfo.InvariantCulture) != context.ActorId) return false;
                position = localPlayer.transform.position;
            }
            else
            {
                var peer = ZNet.instance.GetPeers().FirstOrDefault(p => p.m_uid.ToString(CultureInfo.InvariantCulture) == context.SenderId);
                if (peer == null || !peer.IsReady() || !peer.m_rpc.IsConnected()) return false;
                var character = ZDOMan.instance?.GetZDO(peer.m_characterID);
                if (character == null || character.GetOwner() != peer.m_uid ||
                    character.GetLong(ZDOVars.s_playerID, 0).ToString(CultureInfo.InvariantCulture) != context.ActorId) return false;
                position = character.GetPosition();
            }
            return TraderAuthority.TryValidate(traderZdo, out Vector3 anchor) && Vector3.Distance(position, anchor) <= 10f;
        }

        private sealed class Context : ITradeContext
        {
            private readonly Player player;
            internal Context(Player player) => this.player = player;
            public bool HasGlobalKey(string key) => ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key);
            public bool HasPlayerKey(string key) => player != null && player.HaveUniqueKey(key);
            public bool IsKnown(string prefab) => player != null && TradeWorld.Item(prefab) != null && player.IsMaterialKnown(TradeWorld.Item(prefab).m_itemData.m_shared.m_name);
            public int? MaxStack(string prefab) => TradeWorld.Item(prefab)?.m_itemData.m_shared.m_maxStackSize;
        }
    }
}
