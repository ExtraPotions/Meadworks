// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behaviour adapted from TradersExtended 2.0.5 (Unlicense, pin 2d5dd879) Trading/StorePanel.Trading.cs (PurchaseSelectedItem): payment
// first, then delivery, and a failed delivery returns the payment. The code is Barter's and buys one lot per press.
using System;
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Barter.Runtime
{
    internal static class BuyService
    {
        private static bool busy;
        private static int pendingGeneration;

        internal static void ResetPending() { busy = false; pendingGeneration++; }

        internal static void Buy(StoreGui store, Trader.TradeItem item, OfferInfo info)
        {
            if (busy || store == null || store.m_trader == null || item == null || Player.m_localPlayer == null)
                return;
            Player player = Player.m_localPlayer;
            if (item.m_prefab == null || !string.IsNullOrEmpty(item.m_buyKey) || !string.IsNullOrEmpty(item.m_incrementKey))
            {
                Plugin.Log?.Warning("Barter refused a native key or inventory-upgrade reward; atomic reward rollback is not qualified.");
                player.Message(MessageHud.MessageType.Center, TradeAuthority.UnavailableReason);
                return;
            }
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
            ItemDrop currency = TradeOffers.CurrencyOf(info, store);
            int price = item.m_price;
            bool fromList = info != null && info.FromList;
            int stack = fromList ? item.m_stack : Math.Min(item.m_stack, item.m_prefab.m_itemData.m_shared.m_maxStackSize);
            int quality = info != null && info.Quality > 0 ? info.Quality : item.m_prefab.m_itemData.m_quality;
            if (currency == null || price < 0 || stack <= 0) return;
            if (Currencies.Count(inventory, currency) < price)
            {
                player.Message(MessageHud.MessageType.Center, BarterText.Format("buy_need", price, Currencies.Name(currency)));
                return;
            }
            var request = new TradeRequest { Buy = true, Vanilla = !fromList, Prefab = Utils.GetPrefabName(item.m_prefab.gameObject),
                Currency = Utils.GetPrefabName(currency.gameObject), Amount = stack, Quality = quality, LotSize = stack, Price = price };
            busy = true;
            int attempt = ++pendingGeneration;
            if (!TradeAuthority.TryBegin(store, request, permit =>
            {
                try
                {
                    if (attempt != pendingGeneration) { permit?.Finish(false); return; }
                    if (permit == null) { player.Message(MessageHud.MessageType.Center, TradeAuthority.UnavailableReason); return; }
                    if (!permit.IsCurrent || Player.m_localPlayer != player || store == null || store.m_trader != trader ||
                        player.GetInventory() != inventory || !StoreGui.IsVisible())
                    { permit.Finish(false); return; }
                    var payment = SafeTradeInventory.Currency(inventory, currency, price);
                    if (payment.Sum(take => (long)take.Amount) != price) { permit.Finish(false); return; }
                    bool cheated = payment.Any(take => take.Item.m_cheated);
                    var output = SafeTradeInventory.Template(item.m_prefab, quality, cheated);
                    ExchangeResult result = SafeTradeInventory.Exchange(inventory, payment, output, stack);
                    if (result == ExchangeResult.Unresolved)
                    {
                        Plugin.Log?.Warning("Trade inventory rollback could not be proven; the server reservation remains locked for recovery.");
                        return;
                    }
                    permit.Finish(result == ExchangeResult.Completed);
                    if (result != ExchangeResult.Completed)
                    { player.Message(MessageHud.MessageType.Center, BarterText.Text(result == ExchangeResult.InventoryHeld ? "trade_inventory_held" : "buy_no_room")); return; }
                    trader.OnBought(item);
                    store.m_buyEffects.Create(store.transform.position, Quaternion.identity);
                    item.m_buyPlayerEffects?.Create(player.transform.position, Quaternion.identity);
                    if (item.m_levelUpEffect) player.OnSkillLevelup(Skills.SkillType.None, 0f);
                    player.ShowPickupMessage(output, stack);
                    Gogan.LogEvent("Game", "BoughtItem", item.m_prefab.name, 0L);
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
    }
}
