// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behaviour adapted from TradersExtended 2.0.5 (Unlicense, pin 2d5dd879) Trading/TraderCurrency.cs: the store's coin prefab is
// swapped to the trader's currency while its window is open, and an entry can name its own. The code is Barter's.
using System;
using System.Collections.Generic;
using ExtraPotions.Barter.Config;
using ExtraPotions.Barter.Currency;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Barter.Runtime
{
    internal static class Currencies
    {
        private static ItemDrop vanilla;
        private static readonly HashSet<string> warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The game's own coin.</summary>
        internal static ItemDrop Vanilla(StoreGui store = null)
        {
            if (vanilla != null)
                return vanilla;
            vanilla = store != null && store.m_coinPrefab != null && store.m_coinPrefab.name == BarterCore.VanillaCurrency
                ? store.m_coinPrefab
                : TradeWorld.Item(BarterCore.VanillaCurrency);
            return vanilla;
        }

        /// <summary>The currency prefab name the Trader Currencies setting gives this trader, empty for the coin.</summary>
        internal static string NameFor(string trader)
        {
            string text = CurrencyRules.ForTrader(Plugin.Settings.Get(BarterSettings.Lists.TraderCurrencies), trader, out string warning);
            if (warning != null && warned.Add(warning))
                Plugin.Log?.Warning(warning);
            return TraderPolicies.Currency(trader, text);
        }

        /// <summary>The currency item for a prefab name; empty means the coin; an unknown prefab falls back to <paramref name="fallback"/>.</summary>
        internal static ItemDrop Resolve(string prefab, ItemDrop fallback)
        {
            if (CurrencyRules.IsVanilla(prefab))
                return Vanilla();
            ItemDrop found = TradeWorld.Item(prefab);
            if (found != null)
                return found;
            if (warned.Add("missing:" + prefab))
                Plugin.Log?.Warning($"Currency item prefab '{prefab}' was not found. The trader's default currency will be used.");
            return fallback ?? Vanilla();
        }

        /// <summary>The trader's own currency item.</summary>
        internal static ItemDrop OfTrader(string trader) => Resolve(NameFor(trader), Vanilla());

        /// <summary>Makes the store's window trade in the trader's currency; returns the currency.</summary>
        internal static ItemDrop Apply(StoreGui store, Trader trader)
        {
            if (store == null)
                return null;
            Vanilla(store);
            ItemDrop currency = trader == null ? Vanilla() : OfTrader(TradeWorld.TraderName(trader));
            if (currency != null)
            {
                store.m_coinPrefab = currency;
                SetIcons(store.m_rootPanel, currency);
            }
            return currency;
        }

        /// <summary>Puts the coin back when the window closes, so nothing else sees the swapped prefab.</summary>
        internal static void Restore(StoreGui store)
        {
            if (store != null && vanilla != null)
            {
                store.m_coinPrefab = vanilla;
                SetIcons(store.m_rootPanel, vanilla);
            }
        }

        internal static int Count(Inventory inventory, ItemDrop currency) =>
            inventory == null || currency == null ? 0 : inventory.CountItems(currency.m_itemData.m_shared.m_name);

        /// <summary>Sets every coin image under <paramref name="root"/> (the game's price and balance icons) to the currency's icon.</summary>
        internal static void SetIcons(GameObject root, ItemDrop currency)
        {
            if (root == null || currency == null)
                return;
            Sprite icon = currency.m_itemData.GetIcon();
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                string name = image.gameObject.name;
                if (name.IndexOf("coin", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("bkg", StringComparison.OrdinalIgnoreCase) < 0)
                    image.sprite = icon;
            }
        }

        internal static string Name(ItemDrop currency) =>
            currency == null ? string.Empty : Localization.instance.Localize(currency.m_itemData.m_shared.m_name);
    }
}
