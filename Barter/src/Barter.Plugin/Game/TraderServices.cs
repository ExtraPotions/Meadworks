// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Barter.Config;
using ExtraPotions.Barter.Trading;

namespace ExtraPotions.Barter.Runtime
{
    internal static class BuybackService
    {
        internal static IReadOnlyList<BuybackOffer> Offers(StoreGui store)
        {
            try
            {
                var trader = store?.m_trader;
                if (!Plugin.Active || !ExtraPotions.Meadworks.Crucible.Plugin.Session.IsAuthority || trader == null || Player.m_localPlayer == null ||
                    !Plugin.Settings.GetFor(BarterSettings.Services.BuybackEnabled, TradeWorld.TraderName(trader))) return Array.Empty<BuybackOffer>();
                var view = trader.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) return Array.Empty<BuybackOffer>();
                string actor = Player.m_localPlayer.GetPlayerID().ToString(CultureInfo.InvariantCulture);
                var rows = TradeFeatures.Read(view.GetZDO()).Where(r => r.Actor == actor && r.Expires > TradeFeatures.Day && r.ReservedBy.Length == 0).ToArray();
                foreach (var row in rows)
                {
                    var items = TradeItemSnapshot.Decode(row.Snapshot);
                    row.Label = Localization.instance.Localize(items[0].Item.m_shared.m_name);
                }
                return rows;
            }
            catch (Exception error) { Plugin.Log?.Warning("Buyback offers unavailable: " + error.GetType().Name); return Array.Empty<BuybackOffer>(); }
        }

        internal static void Redeem(StoreGui store, BuybackOffer offer)
        {
            if (offer == null) return;
            var current = Offers(store).FirstOrDefault(o => o.Id == offer.Id);
            if (current == null) return;
            var items = TradeItemSnapshot.Decode(current.Snapshot);
            var request = new TradeRequest { Buy = true, Service = 1, Receipt = current.Id, Snapshot = current.Snapshot,
                Prefab = TradeWorld.PrefabName(items[0].Item), Quality = items[0].Item.m_quality, Amount = current.Amount,
                LotSize = current.Amount, Price = current.Price, Currency = current.Currency };
            TraderServiceExchange.Run(store, request, inventory =>
                SafeTradeInventory.ExchangeMany(inventory, TraderServiceExchange.Payment(inventory, request), items), "buyback_redeemed");
        }
    }

    internal sealed class RepairOffer
    {
        internal ItemDrop.ItemData Item;
        internal int Price;
        internal string Currency = "", Snapshot = "";
    }

    internal static class RepairService
    {
        internal static IReadOnlyList<RepairOffer> Offers(StoreGui store)
        {
            var rows = new List<RepairOffer>();
            var trader = store?.m_trader;
            if (!Plugin.Active || !ExtraPotions.Meadworks.Crucible.Plugin.Session.IsAuthority || trader == null || Player.m_localPlayer == null ||
                !Plugin.Settings.GetFor(BarterSettings.Services.RepairEnabled, TradeWorld.TraderName(trader))) return rows;
            string name = TradeWorld.TraderName(trader);
            ItemDrop currency = Currencies.OfTrader(name);
            if (currency == null) return rows;
            foreach (var item in Player.m_localPlayer.GetInventory().GetAllItems())
            {
                if (item.m_dropPrefab == null || item.m_stack != 1 || !item.m_shared.m_useDurability || !item.m_shared.m_canBeReparied ||
                    item.m_worldLevel < Game.m_worldLevel || !ServiceRules.RepairPrice(Plugin.Settings.GetFor(BarterSettings.Services.RepairBasePrice, name),
                        item.m_quality, item.m_durability, item.GetMaxDurability(), Purses.BuyFactor(trader), out int price)) continue;
                try { rows.Add(new RepairOffer { Item = item, Price = price, Currency = Utils.GetPrefabName(currency.gameObject),
                    Snapshot = TradeItemSnapshot.Encode(new[] { new SafeTradeInventory.Take(item, 1) }) }); }
                catch (Exception error) { Plugin.Log?.Warning("Repair item refused: " + error.GetType().Name); }
            }
            return rows;
        }

        internal static void Repair(StoreGui store, RepairOffer offer)
        {
            if (offer == null) return;
            var current = Offers(store).FirstOrDefault(o => ReferenceEquals(o.Item, offer.Item));
            if (current == null || current.Price != offer.Price || current.Snapshot != offer.Snapshot) return;
            var request = new TradeRequest { Buy = true, Service = 2, Snapshot = current.Snapshot,
                Prefab = TradeWorld.PrefabName(current.Item), Quality = current.Item.m_quality, Amount = 1,
                LotSize = 1, Price = current.Price, Currency = current.Currency };
            TraderServiceExchange.Run(store, request, inventory =>
            {
                if (!inventory.ContainsItem(current.Item) || TradeItemSnapshot.Encode(new[] { new SafeTradeInventory.Take(current.Item, 1) }) != request.Snapshot)
                    return ExchangeResult.RolledBack;
                return SafeTradeInventory.Repair(inventory, TraderServiceExchange.Payment(inventory, request), current.Item);
            }, "repair_repaired");
        }
    }

    internal static class TraderServiceExchange
    {
        private static bool busy;
        private static int generation;
        internal static void ResetPending() { busy = false; generation++; }
        internal static IReadOnlyList<SafeTradeInventory.Take> Payment(Inventory inventory, TradeRequest request)
            => SafeTradeInventory.Currency(inventory, TradeWorld.Item(request.Currency), request.Price);

        internal static void Run(StoreGui store, TradeRequest request, Func<Inventory, ExchangeResult> exchange, string message)
        {
            var player = Player.m_localPlayer;
            var trader = store?.m_trader;
            var inventory = player?.GetInventory();
            if (busy || trader == null || inventory == null || !StoreGui.IsVisible()) return;
            if (TradeAuthority.RecoveryBlocked || SafeTradeInventory.IsBlocked(inventory))
            { player.Message(MessageHud.MessageType.Center, TradeAuthority.UnavailableReason); return; }
            if (OptionalTradeIntegrations.InventoryHeld(inventory))
            { player.Message(MessageHud.MessageType.Center, BarterText.Text("trade_inventory_held")); return; }
            if (Payment(inventory, request).Sum(p => (long)p.Amount) != request.Price)
            { player.Message(MessageHud.MessageType.Center, BarterText.Format("buy_need", request.Price, Currencies.Name(TradeWorld.Item(request.Currency)))); return; }
            busy = true; int attempt = ++generation;
            if (!TradeAuthority.TryBegin(store, request, permit =>
            {
                try
                {
                    if (permit == null) { player.Message(MessageHud.MessageType.Center, TradeAuthority.UnavailableReason); return; }
                    if (attempt != generation || !permit.IsCurrent || player != Player.m_localPlayer || player.GetInventory() != inventory ||
                        store == null || store.m_trader != trader || !StoreGui.IsVisible() || Payment(inventory, request).Sum(p => (long)p.Amount) != request.Price)
                    { permit.Finish(false); return; }
                    var result = exchange(inventory);
                    if (result == ExchangeResult.Unresolved) { player.Message(MessageHud.MessageType.Center, TradeAuthority.UnavailableReason); return; }
                    permit.Finish(result == ExchangeResult.Completed);
                    player.Message(MessageHud.MessageType.Center, BarterText.Text(result == ExchangeResult.Completed ? message :
                        result == ExchangeResult.InventoryHeld ? "trade_inventory_held" : "buy_no_room"));
                }
                finally { if (attempt == generation) busy = false; if (store != null && store.m_trader == trader && StoreGui.IsVisible()) store.FillList(); }
            })) { busy = false; player.Message(MessageHud.MessageType.Center, TradeAuthority.UnavailableReason); }
        }
    }
}
