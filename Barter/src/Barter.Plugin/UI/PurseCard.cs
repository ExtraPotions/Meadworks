// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behaviour adapted from TradersExtended 2.0.5 (Unlicense, pin 2d5dd879) UI/StorePanel.cs (the balance on the store window and the
// price change shown beside the trader and player names). Built on the Crucible.UI kit; no upstream UI code is used.
using System;
using ExtraPotions.Barter.Pricing;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>The trader's purse beside the store window: balance and currency, and how far the balance moves prices.</summary>
    internal sealed class PurseCard : IDisposable
    {
        internal const string ObjectName = "Barter_purse";
        private static readonly Color Good = new Color(0.5f, 1f, 0.5f);
        private static readonly Color Bad = new Color(1f, 0.4f, 0.4f);
        private static readonly Color Dim = new Color(0.78f, 0.78f, 0.78f);

        private readonly RectTransform panel;
        private readonly TMP_Text title, balance, buying, selling;
        private readonly Image coin;
        private Button services;

        private PurseCard(RectTransform panel, TMP_Text title, TMP_Text balance, TMP_Text buying, TMP_Text selling, Image coin)
        {
            this.panel = panel;
            this.title = title;
            this.balance = balance;
            this.buying = buying;
            this.selling = selling;
            this.coin = coin;
        }

        internal bool Alive => panel != null;

        internal static PurseCard Create(StoreGui store)
        {
            Transform root = store != null && store.m_rootPanel != null ? store.m_rootPanel.transform : null;
            RectTransform panel = KitPanel.Create(root, ObjectName, new Vector2(250f, 154f));
            if (panel == null)
                return null;

            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-6f, 0f);

            TMP_Text title = Label(panel, "title", 18f, Color.white, TextAlignmentOptions.MidlineLeft, 10f, 8f, 230f, 22f);
            TMP_Text balance = Label(panel, "balance", 16f, Color.white, TextAlignmentOptions.MidlineLeft, 10f, 34f, 190f, 20f);
            TMP_Text buying = Label(panel, "buying", 15f, Dim, TextAlignmentOptions.MidlineLeft, 10f, 60f, 230f, 20f);
            TMP_Text selling = Label(panel, "selling", 15f, Dim, TextAlignmentOptions.MidlineLeft, 10f, 82f, 230f, 20f);

            var coinObject = new GameObject("coin", typeof(RectTransform), typeof(Image));
            coinObject.layer = panel.gameObject.layer;
            RectTransform coinRect = (RectTransform)coinObject.transform;
            coinRect.SetParent(panel, false);
            Anchor(coinRect, 204f, 34f, 20f, 20f);
            Image coin = coinObject.GetComponent<Image>();
            coin.preserveAspect = true;
            coin.raycastTarget = false;

            Button services = KitButton.Create(panel, "services", BarterText.Text("services_title"), () => TraderServicesWindow.Open(StoreGui.instance),
                new Vector2(10f, 114f), new Vector2(230f, 30f));
            KitTooltip.Set(panel.gameObject, BarterText.Text("purse_title"), BarterText.Text("purse_tip"));
            return new PurseCard(panel, title, balance, buying, selling, coin) { services = services };
        }

        internal void Show(bool visible)
        {
            if (panel != null && panel.gameObject.activeSelf != visible)
                panel.gameObject.SetActive(visible);
        }

        /// <summary>Refreshes the texts for the trader the store shows.</summary>
        internal void Update(StoreGui store, bool showBalance)
        {
            if (panel == null || store == null || store.m_trader == null)
                return;

            title.gameObject.SetActive(showBalance);
            balance.gameObject.SetActive(showBalance);
            buying.gameObject.SetActive(showBalance);
            selling.gameObject.SetActive(showBalance);
            coin.gameObject.SetActive(showBalance);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, showBalance ? 154f : 50f);
            if (services != null)
                Anchor((RectTransform)services.transform, 10f, showBalance ? 114f : 10f, 230f, 30f);
            if (services != null) KitTooltip.Set(services.gameObject, BarterText.Text("services_title"),
                BarterText.Text(OptionalTradeIntegrations.EpicLootAdventure ? "epicloot_panel" : "controller_help"));
            Trader trader = store.m_trader;
            title.text = trader.GetHoverName();
            ItemDrop currency = store.m_coinPrefab != null ? store.m_coinPrefab : Currencies.Vanilla();
            if (coin != null && currency != null)
                coin.sprite = currency.m_itemData.GetIcon();
            balance.text = BarterText.Text("purse_balance") + ": " + Purses.Balance(trader);
            Describe(buying, "purse_buying", Purses.BuyFactor(trader), reversed: true);
            Describe(selling, "purse_selling", Purses.SellFactor(trader), reversed: false);
            Fit();
        }

        /// <summary>Keeps the card on the HUD canvas as the store or the resolution moves.</summary>
        internal void Fit() => PanelPlacement.Apply(panel);

        private static void Describe(TMP_Text label, string key, float factor, bool reversed)
        {
            string change = PriceCurve.Describe(factor, reversed, out bool favourable);
            label.text = BarterText.Text(key) + ": " + (change.Length == 0
                ? BarterText.Text("purse_standard")
                : BarterText.Format("purse_percent", change));
            label.color = change.Length == 0 ? Dim : favourable ? Good : Bad;
        }

        private static TMP_Text Label(RectTransform parent, string name, float size, Color colour, TextAlignmentOptions alignment, float x, float y, float width, float height)
        {
            TMP_Text text = KitCanvas.Text(parent, name, string.Empty, size, colour, alignment, false);
            Anchor((RectTransform)text.transform, x, y, width, height);
            text.raycastTarget = false;
            return text;
        }

        private static void Anchor(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public void Dispose()
        {
            if (panel != null)
                UnityEngine.Object.Destroy(panel.gameObject);
        }
    }
}
