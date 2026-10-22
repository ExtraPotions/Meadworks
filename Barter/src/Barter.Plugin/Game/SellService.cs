// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behaviour adapted from TradersExtended 2.0.5 (Unlicense, pin 2d5dd879) ItemLists/StorePanel.Lists.cs (FillSellableList) and
// Trading/StorePanel.Trading.cs (SellSelectedItem). The offer grouping is Barter.Core's SellPlanner; the code here is Barter's.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Barter.Config;
using ExtraPotions.Barter.Currency;
using ExtraPotions.Barter.Lists;
using ExtraPotions.Barter.Trading;
using UnityEngine;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>The sell list for the open trader: what the player carries that this trader pays for, and the sale of the selected offer.</summary>
    internal static class SellService
    {
        private static readonly List<SellOffer> offers = new List<SellOffer>();
        private static SellOffer selected;
        private static bool busy;
        private static int pendingGeneration;

        internal static void ResetPending() { busy = false; pendingGeneration++; }

        internal static IReadOnlyList<SellOffer> Offers => offers;

        internal static SellOffer Selected => selected;

        /// <summary>Text the player typed to narrow the list.</summary>
        internal static string Filter { get; set; } = string.Empty;

        /// <summary>The list or the selection changed; the panel redraws.</summary>
        internal static long Revision { get; private set; }

        internal static void Clear()
        {
            offers.Clear();
            selected = null;
            Filter = string.Empty;
            Revision++;
        }

        /// <summary>Builds the offers from the inventory and the trader's lists; keeps the selection when the same offer is still there.</summary>
        internal static void Refresh(StoreGui store)
        {
            SellOffer previous = selected;
            offers.Clear();
            selected = null;
            try
            {
                Trader trader = store != null ? store.m_trader : null;
                Player player = Player.m_localPlayer;
                if (trader == null || player == null || !Plugin.Active || !TradeLists.Ready || Plugin.Settings == null)
                    return;

                string name = TradeWorld.TraderName(trader);
                TradeBook book = TradeLists.Effective;
                bool automatic = Plugin.Settings.GetFor(BarterSettings.Lists.CommonValuables, name);
                var table = SellPriceTable.Build(book.Sell(name, automatic), TradeWorld.Instance);
                var settings = new SellSettings(Purses.SellFactor(trader), Plugin.Settings.GetFor(BarterSettings.Lists.QualityPriceMultiplier, name));
                string traderCurrency = Currencies.NameFor(name);
                ItemDrop own = store.m_coinPrefab != null ? store.m_coinPrefab : Currencies.Vanilla();

                bool hideEquipped = Plugin.Settings.Get(BarterSettings.Interface.HideEquipped);
                string filter = Filter ?? string.Empty;
                var held = new List<HeldItem>();
                foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItemsSortedByName())
                {
                    string prefab = TradeWorld.PrefabName(item);
                    if (prefab.Length == 0 || item.m_worldLevel < Game.m_worldLevel || item.m_equipped || OptionalTradeIntegrations.ProtectedItem(item, player))
                        continue;
                    if (hideEquipped && (item.m_equipped || (item.m_gridPos.y == 0 && item.IsEquipable())))
                        continue;
                    if (filter.Length > 0 && Localization.instance.Localize(item.m_shared.m_name).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    held.Add(new HeldItem(item, prefab, item.m_stack, item.m_quality, item.m_shared.m_maxStackSize));
                }

                offers.AddRange(SellPlanner.Plan(held, table, settings, raw =>
                {
                    ItemDrop currency = Currencies.Resolve(CurrencyRules.Effective(raw, traderCurrency), own);
                    return currency != null ? Utils.GetPrefabName(currency.gameObject) : null;
                }));

                int at = previous == null ? -1 : offers.FindIndex(offer => Same(offer, previous));
                selected = at >= 0 ? offers[at] : offers.Count > 0 ? offers[0] : null;
            }
            finally
            {
                Revision++;
            }
        }

        internal static void Select(SellOffer offer)
        {
            if (!ReferenceEquals(selected, offer))
            {
                selected = offer;
                Revision++;
            }
        }

        /// <summary>How many items one press of Sell hands over: everything for a combined offer, one lot otherwise.</summary>
        internal static int ItemsPerSale(SellOffer offer) =>
            offer == null ? 0 : offer.Kind == SellKind.Combined ? offer.Amount : offer.Kind == SellKind.Stack ? offer.Stack : 1;

        /// <summary>Whether Sell can work now, and why not (empty when it can).</summary>
        internal static bool CanSell(StoreGui store, out string reason)
        {
            reason = string.Empty;
            SellOffer offer = selected;
            if (busy || offer == null || store == null || store.m_trader == null || Player.m_localPlayer == null)
                return false;
            if (TradeAuthority.RecoveryBlocked || SafeTradeInventory.IsBlocked(Player.m_localPlayer.GetInventory()))
            {
                reason = TradeAuthority.UnavailableReason;
                return false;
            }
            if (OptionalTradeIntegrations.InventoryHeld(Player.m_localPlayer.GetInventory()))
            {
                reason = BarterText.Text("trade_inventory_held");
                return false;
            }
            if (SameCurrencyAsTrader(offer, store) && !Purses.CanPay(store.m_trader, offer.Price))
            {
                reason = BarterText.Format("sell_trader_poor", store.m_trader.GetHoverName());
                return false;
            }
            if (!CanReceivePayment(Player.m_localPlayer.GetInventory(), offer.Sources.Cast<ItemDrop.ItemData>().ToList(),
                ItemsPerSale(offer), offer.Price, TradeWorld.Item(offer.Currency) ?? Currencies.Vanilla()))
            {
                reason = BarterText.Text("sell_no_room");
                return false;
            }
            return true;
        }

        internal static void Sell(StoreGui store)
        {
            if (busy) return;
            if (!CanSell(store, out string reason))
            {
                if (reason.Length > 0) Player.m_localPlayer?.Message(MessageHud.MessageType.Center, reason);
                return;
            }
            SellOffer offer = selected;
            BeginSale(store, offer.Sources.Cast<ItemDrop.ItemData>().ToList(), ItemsPerSale(offer), offer.Price,
                TradeWorld.Item(offer.Currency) ?? Currencies.Vanilla(), offer.Prefab, offer.Quality, offer.Stack, false);
        }

        /// <summary>Checks the native valuable stack when Barter's sell list is hidden.</summary>
        internal static bool CanSellValuables(StoreGui store, out string reason)
        {
            reason = string.Empty;
            Player player = Player.m_localPlayer;
            if (busy || store == null || store.m_trader == null || player == null) return false;
            Inventory inventory = player.GetInventory();
            if (TradeAuthority.RecoveryBlocked || SafeTradeInventory.IsBlocked(inventory))
            { reason = TradeAuthority.UnavailableReason; return false; }
            if (OptionalTradeIntegrations.InventoryHeld(inventory))
            { reason = BarterText.Text("trade_inventory_held"); return false; }
            ItemDrop.ItemData item = store.GetSellableItem();
            ItemDrop currency = store.m_coinPrefab ?? Currencies.Vanilla();
            if (item == null || currency == null || item.m_stack <= 0 || item.m_shared.m_value <= 0) return false;
            long price = (long)item.m_stack * item.m_shared.m_value;
            if (price > int.MaxValue) { reason = TradeAuthority.UnavailableReason; return false; }
            if (!Purses.CanPay(store.m_trader, (int)price))
            { reason = BarterText.Format("sell_trader_poor", store.m_trader.GetHoverName()); return false; }
            if (!CanReceivePayment(inventory, new[] { item }, item.m_stack, (int)price, currency))
            { reason = BarterText.Text("sell_no_room"); return false; }
            return true;
        }

        /// <summary>Retains the native button's first valuable stack, independent of Barter's hidden selection.</summary>
        internal static void SellValuables(StoreGui store)
        {
            if (busy || store == null || store.m_trader == null || Player.m_localPlayer == null) return;
            ItemDrop.ItemData item = store.GetSellableItem();
            ItemDrop currency = store.m_coinPrefab ?? Currencies.Vanilla();
            if (item == null || currency == null || item.m_stack <= 0 || item.m_shared.m_value <= 0) return;
            long price = (long)item.m_stack * item.m_shared.m_value;
            if (price > int.MaxValue) return;
            BeginSale(store, new List<ItemDrop.ItemData> { item }, item.m_stack, (int)price, currency,
                TradeWorld.PrefabName(item), item.m_quality, 1, true);
        }

        private static void BeginSale(StoreGui store, List<ItemDrop.ItemData> items, int needed, int price,
            ItemDrop currency, string prefab, int quality, int lotSize, bool vanilla)
        {
            Player player = Player.m_localPlayer;
            Inventory inventory = player.GetInventory();
            if (OptionalTradeIntegrations.InventoryHeld(inventory))
            {
                player.Message(MessageHud.MessageType.Center, BarterText.Text("trade_inventory_held"));
                return;
            }
            if (TradeAuthority.RecoveryBlocked || SafeTradeInventory.IsBlocked(inventory))
            {
                player.Message(MessageHud.MessageType.Center, TradeAuthority.UnavailableReason);
                return;
            }
            Trader trader = store.m_trader;
            if (currency == null || items.Count == 0 || needed <= 0 || price < 0) return;
            if (!CanReceivePayment(inventory, items, needed, price, currency))
            {
                player.Message(MessageHud.MessageType.Center, BarterText.Text("sell_no_room"));
                return;
            }
            var request = new TradeRequest { Buy = false, Vanilla = vanilla, Prefab = prefab, Amount = needed,
                Quality = quality, Price = price, Currency = Utils.GetPrefabName(currency.gameObject), LotSize = lotSize };
            bool buyback = Plugin.Settings.GetFor(BarterSettings.Services.BuybackEnabled, TradeWorld.TraderName(trader));
            if (buyback && !ExtraPotions.Meadworks.Crucible.Plugin.Session.IsAuthority)
                player.Message(MessageHud.MessageType.Center, BarterText.Text("service_authority_only"));
            if (buyback && ExtraPotions.Meadworks.Crucible.Plugin.Session.IsAuthority)
            {
                var escrow = new List<SafeTradeInventory.Take>();
                int remaining = needed;
                foreach (var source in items)
                {
                    int count = Math.Min(remaining, source.m_stack);
                    if (count > 0) escrow.Add(new SafeTradeInventory.Take(source, count));
                    remaining -= count;
                }
                if (remaining != 0) return;
                try { request.Snapshot = TradeItemSnapshot.Encode(escrow); }
                catch (Exception error) { Plugin.Log?.Warning("Sale snapshot refused: " + error.GetType().Name); return; }
            }
            if (!TradeCodec.FitsReservation(request, ExtraPotions.Meadworks.Crucible.Plugin.Session.SessionId))
            {
                player.Message(MessageHud.MessageType.Center, BarterText.Text("sell_buyback_too_large"));
                return;
            }
            // Keep the selected source counts while waiting. A moved, consumed, or restacked source cancels the sale.
            var counts = items.Select(item => item.m_stack).ToArray();
            busy = true;
            int attempt = ++pendingGeneration;
            if (!TradeAuthority.TryBegin(store, request, permit =>
            {
                try
                {
                    if (attempt != pendingGeneration) { permit?.Finish(false); return; }
                    if (permit == null) { player.Message(MessageHud.MessageType.Center, TradeAuthority.UnavailableReason); return; }
                    if (!permit.IsCurrent || Player.m_localPlayer != player || store == null || store.m_trader != trader ||
                        player.GetInventory() != inventory || !StoreGui.IsVisible() ||
                        items.Where((item, i) => !inventory.ContainsItem(item) || item.m_stack != counts[i] ||
                            item.m_equipped || OptionalTradeIntegrations.ProtectedItem(item, player) ||
                            item.m_quality != quality || TradeWorld.PrefabName(item) != prefab).Any())
                    { permit.Finish(false); return; }
                    var payment = new List<SafeTradeInventory.Take>();
                    int left = needed;
                    foreach (var item in items)
                    {
                        int take = Math.Min(left, item.m_stack);
                        if (take <= 0) break;
                        payment.Add(new SafeTradeInventory.Take(item, take));
                        left -= take;
                    }
                    if (left != 0) { permit.Finish(false); return; }
                    if (request.Snapshot.Length > 0 && TradeItemSnapshot.Encode(payment) != request.Snapshot)
                    { permit.Finish(false); return; }
                    var output = SafeTradeInventory.Template(currency, currency.m_itemData.m_quality,
                        payment.Any(take => take.Item.m_cheated));
                    ExchangeResult result = SafeTradeInventory.Exchange(inventory, payment, output, price);
                    if (result == ExchangeResult.Unresolved)
                    {
                        Plugin.Log?.Warning("Trade inventory rollback could not be proven; the server reservation remains locked for recovery.");
                        return;
                    }
                    permit.Finish(result == ExchangeResult.Completed);
                    if (result != ExchangeResult.Completed)
                    { player.Message(MessageHud.MessageType.Center, BarterText.Text(result == ExchangeResult.InventoryHeld ? "trade_inventory_held" : "sell_no_room")); return; }
                    string label = (needed > 1 ? needed + "x " : string.Empty) + Localization.instance.Localize(items[0].m_shared.m_name);
                    trader.OnSold();
                    store.m_sellEffects.Create(store.transform.position, Quaternion.identity);
                    player.Message(MessageHud.MessageType.TopLeft,
                        BarterText.Format("sell_sold", label, price, Currencies.Name(currency)), 0, items[0].m_shared.m_icons[0]);
                    Gogan.LogEvent("Game", "SoldItem", label, 0L);
                }
                finally
                {
                    if (attempt == pendingGeneration) busy = false;
                    if (store != null && store.m_trader == trader && StoreGui.IsVisible()) store.FillList();
                }
            }))
            {
                busy = false;
                player.Message(MessageHud.MessageType.Center, TradeAuthority.UnavailableReason);
            }
        }

        private static bool CanReceivePayment(Inventory inventory, IReadOnlyList<ItemDrop.ItemData> items, int needed, int price, ItemDrop currency)
        {
            if (currency == null || needed <= 0) return false;
            var payment = new List<SafeTradeInventory.Take>();
            foreach (var item in items)
            {
                if (item == null) return false;
                int take = Math.Min(needed, item.m_stack);
                if (take > 0) payment.Add(new SafeTradeInventory.Take(item, take));
                needed -= take;
                if (needed == 0) break;
            }
            return needed == 0 && SafeTradeInventory.CanFitExchange(inventory, payment,
                SafeTradeInventory.Template(currency, currency.m_itemData.m_quality, false), price);
        }

        private static bool SameCurrencyAsTrader(SellOffer offer, StoreGui store)
        {
            ItemDrop paid = TradeWorld.Item(offer.Currency);
            ItemDrop own = store.m_coinPrefab != null ? store.m_coinPrefab : Currencies.Vanilla();
            return paid == null || own == null || paid.m_itemData.m_shared.m_name == own.m_itemData.m_shared.m_name;
        }

        private static bool Same(SellOffer a, SellOffer b) =>
            a.Kind == b.Kind && a.Prefab == b.Prefab && a.Stack == b.Stack && a.Quality == b.Quality && a.Currency == b.Currency &&
            (a.Kind != SellKind.Single || ReferenceEquals(a.Sources[0], b.Sources[0]));
    }
}
