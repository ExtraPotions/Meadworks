// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ExtraPotions.Barter.Config;
using HarmonyLib;

namespace ExtraPotions.Barter.Runtime
{
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    internal static class PriceTips
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last), HarmonyAfter("randyknapp.mods.epicloot")]
        private static void Postfix(ItemDrop.ItemData item, bool crafting, bool appending, ref string __result)
        {
            if (crafting || appending || item == null || !Plugin.Active || !TradeLists.Ready ||
                Plugin.Settings == null || !Plugin.Settings.Get(BarterSettings.Interface.PriceTips) || !StoreGui.IsVisible()) return;
            StoreGui store = StoreGui.instance;
            if (store == null || store.m_trader == null) return;
            string prefab = TradeWorld.PrefabName(item);
            var sale = SellService.Offers.FirstOrDefault(o => o.Sources.Any(source => ReferenceEquals(source, item)));
            if (sale != null)
                __result += "\n\n" + BarterText.Format("price_sell", sale.Price, Currencies.Name(TradeWorld.Item(sale.Currency)), SellService.ItemsPerSale(sale));
            var selected = store.m_selectedItem;
            if (selected?.m_prefab != null && Utils.GetPrefabName(selected.m_prefab.gameObject) == prefab)
                __result += "\n\n" + BarterText.Format("price_buy", selected.m_price,
                    Currencies.Name(TradeOffers.CurrencyOf(TradeOffers.InfoOf(selected), store)), selected.m_stack);
        }
    }
}
