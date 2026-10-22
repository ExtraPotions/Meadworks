// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behaviour adapted from TradersExtended 2.0.5 (Unlicense, pin 2d5dd879) UI/StorePanel.cs and ItemLists/StorePanel.Lists.cs: a sell list
// beside the buy list with a filter field, the item, its lot and quality, the price and currency, and a Sell button. Built on the
// Crucible.UI kit (panel, virtual list, text field, button, tooltip); no upstream UI code is used. Built, not yet run in the game.
using System;
using System.Collections.Generic;
using ExtraPotions.Barter.Trading;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>The sell list of the open trader beside the store window.</summary>
    internal sealed class SellPanel : IDisposable
    {
        internal const string ObjectName = "Barter_sell";
        private const float Width = 340f;
        private const float RowHeight = 46f;
        private static readonly Color Row = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color RowSelected = new Color(0.8f, 0.55f, 0.1f, 0.55f);

        private readonly RectTransform panel;
        private readonly KitVirtualList list;
        private readonly KitTextField filter;
        private readonly Button sell;
        private readonly TMP_Text title, info;
        private long shown = -1;
        private bool controller;

        private SellPanel(RectTransform panel, KitVirtualList list, KitTextField filter, Button sell, TMP_Text title, TMP_Text info)
        {
            this.panel = panel;
            this.list = list;
            this.filter = filter;
            this.sell = sell;
            this.title = title;
            this.info = info;
        }

        internal bool Alive => panel != null;

        internal static SellPanel Create(StoreGui store)
        {
            Transform root = store != null && store.m_rootPanel != null ? store.m_rootPanel.transform : null;
            float height = store != null && store.m_rootPanel != null ? ((RectTransform)store.m_rootPanel.transform).rect.height : 600f;
            RectTransform panel = KitPanel.Create(root, ObjectName, new Vector2(Width, height));
            if (panel == null)
                return null;

            panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(6f, 0f);

            TMP_Text title = KitCanvas.Text(panel, "title", BarterText.Text("sell_title"), 20f, Color.white, TextAlignmentOptions.MidlineLeft, false);
            Place((RectTransform)title.transform, 12f, 8f, Width - 24f, 26f);
            title.raycastTarget = false;

            KitTextField filter = KitTextField.Create(panel, "filter", KitFieldMode.Search, new Vector2(12f, 40f), new Vector2(Width - 24f, 30f),
                BarterText.Text("sell_filter"), BarterText.Text("sell_filter"));

            float listHeight = height - 78f - 88f;
            KitVirtualList list = KitVirtualList.Create(panel, "list", new Vector2(Width - 24f, listHeight), MakeRow);
            if (list != null)
                Place(list.Root, 12f, 78f, Width - 24f, listHeight);

            TMP_Text info = KitCanvas.Text(panel, "info", string.Empty, 15f, new Color(0.78f, 0.78f, 0.78f), TextAlignmentOptions.TopLeft, true);
            Place((RectTransform)info.transform, 12f, height - 84f, Width - 24f, 40f);
            info.raycastTarget = false;

            Button sell = KitButton.Create(panel, "sell", BarterText.Text("sell_button"), () => SellService.Sell(StoreGui.instance),
                new Vector2(12f, height - 42f), new Vector2(Width - 24f, 34f));

            var made = new SellPanel(panel, list, filter, sell, title, info);
            if (filter != null)
                filter.Changed += text =>
                {
                    SellService.Filter = text ?? string.Empty;
                    SellService.Refresh(StoreGui.instance);
                };
            return made;
        }

        internal void Show(bool visible)
        {
            if (panel != null && panel.gameObject.activeSelf != visible)
                panel.gameObject.SetActive(visible);
        }

        /// <summary>The filter text was cleared because the store opened for another trader.</summary>
        internal void ResetFilter() => filter?.Input?.SetTextWithoutNotify(string.Empty);

        /// <summary>Redraws the rows when the offers changed, and keeps the Sell button and the price line in step.</summary>
        internal void Update(StoreGui store)
        {
            if (panel == null)
                return;

            if (shown != SellService.Revision && list != null)
            {
                shown = SellService.Revision;
                var rows = new List<KitVirtualRow>(SellService.Offers.Count);
                for (int i = 0; i < SellService.Offers.Count; i++)
                {
                    SellOffer offer = SellService.Offers[i];
                    rows.Add(new KitVirtualRow(i + ":" + offer.Prefab, RowHeight, rect => Bind(rect, offer), Unbind));
                }
                list.SetRows(rows, shown);
            }
            list?.Tick();

            bool can = SellService.CanSell(store, out string reason);
            if (sell != null)
                sell.interactable = can;

            SellOffer selected = SellService.Selected;
            if (selected == null)
                info.text = BarterText.Text(SellService.Offers.Count == 0 ? "sell_empty" : controller ? "controller_help" : "sell_tip");
            else
                info.text = reason.Length > 0 ? reason : Describe(selected);
            Fit();
        }

        /// <summary>Keeps the list on the HUD canvas as the store or the resolution moves.</summary>
        internal void Fit() => PanelPlacement.Apply(panel);

        internal void ReleaseController() { controller = false; }

        internal bool BeforeNativeUpdate(StoreGui store)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy || !MeadworksInput.GamepadActive) { controller = false; return false; }
            if (KitTextField.Typing) return true;
            if (!controller)
            {
                if (!MeadworksInput.PadDown(PadButton.DPadRight)) return false;
                MeadworksInput.PadConsume(PadButton.DPadRight); controller = true; FocusSelected(); return true;
            }
            if (MeadworksInput.PadDown(PadButton.B) || MeadworksInput.PadDown(PadButton.DPadLeft))
            {
                MeadworksInput.PadConsume(PadButton.B); MeadworksInput.PadConsume(PadButton.DPadLeft);
                controller = false; EventSystem.current?.SetSelectedGameObject(store.m_buyButton.gameObject); return true;
            }
            int direction = MeadworksInput.PadDown(PadButton.DPadUp) ? -1 : MeadworksInput.PadDown(PadButton.DPadDown) ? 1 : 0;
            if (direction != 0)
            {
                MeadworksInput.PadConsume(direction < 0 ? PadButton.DPadUp : PadButton.DPadDown);
                int at = -1;
                for (int i = 0; i < SellService.Offers.Count; i++) if (ReferenceEquals(SellService.Selected, SellService.Offers[i])) at = i;
                int next = SellCursor.Move(at, SellService.Offers.Count, direction);
                if (next >= 0) SellService.Select(SellService.Offers[next]);
                Update(store); FocusSelected();
            }
            if (MeadworksInput.PadDown(PadButton.X))
            { MeadworksInput.PadConsume(PadButton.X); filter?.Focus(); }
            if (MeadworksInput.PadDown(PadButton.A))
            { MeadworksInput.PadConsume(PadButton.A); if (SellService.CanSell(store, out _)) SellService.Sell(store); }
            return true;
        }

        private void FocusSelected()
        {
            for (int i = 0; i < SellService.Offers.Count; i++)
                if (ReferenceEquals(SellService.Selected, SellService.Offers[i]))
                { list?.Focus(i + ":" + SellService.Offers[i].Prefab); return; }
            EventSystem.current?.SetSelectedGameObject(sell.gameObject);
        }

        private static string Describe(SellOffer offer)
        {
            string text = SellService.ItemsPerSale(offer) > 1 ? SellService.ItemsPerSale(offer) + "x " : string.Empty;
            text += Localization.instance.Localize(TradeWorld.Item(offer.Prefab)?.m_itemData.m_shared.m_name ?? offer.Prefab);
            ItemDrop currency = TradeWorld.Item(offer.Currency);
            return text + ": " + offer.Price + " " + Currencies.Name(currency);
        }

        private static RectTransform MakeRow(RectTransform parent)
        {
            var go = new GameObject("BarterSellRow", typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Image background = go.GetComponent<Image>();
            background.sprite = KitArt.Cell;
            background.type = Image.Type.Sliced;
            background.color = Row;
            go.GetComponent<Button>().targetGraphic = background;

            Image icon = Child(rect, "icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, -20f), new Vector2(44f, 20f)).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            Image coin = Child(rect, "coin", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, -11f), new Vector2(-6f, 11f)).gameObject.AddComponent<Image>();
            coin.preserveAspect = true;
            coin.raycastTarget = false;

            TMP_Text price = KitCanvas.Text(rect, "price", string.Empty, 16f, Color.white, TextAlignmentOptions.MidlineRight, false);
            Stretch((RectTransform)price.transform, new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-32f, 0f));
            price.raycastTarget = false;

            TMP_Text name = KitCanvas.Text(rect, "name", string.Empty, 16f, Color.white, TextAlignmentOptions.MidlineLeft, false);
            Stretch((RectTransform)name.transform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(52f, 0f), new Vector2(0f, 0f));
            name.raycastTarget = false;
            return rect;
        }

        private static void Bind(RectTransform rect, SellOffer offer)
        {
            ItemDrop item = TradeWorld.Item(offer.Prefab);
            ItemDrop.ItemData data = item != null ? item.m_itemData : null;
            rect.Find("icon").GetComponent<Image>().sprite = data?.GetIcon();

            string text = Localization.instance.Localize(data != null ? data.m_shared.m_name : offer.Prefab);
            if (offer.Quality > 1)
                text += " <color=#add8e6ff>(" + offer.Quality + ")</color>";
            if (offer.Kind == SellKind.Stack)
                text += " x" + offer.Stack + (offer.Lots > 1 ? " <color=#c0c0c0ff>(" + BarterText.Format("sell_lots", offer.Lots) + ")</color>" : string.Empty);
            else if (offer.Kind == SellKind.Combined && offer.Amount > 1)
                text += " x" + offer.Amount;
            rect.Find("name").GetComponent<TMP_Text>().text = text;

            ItemDrop currency = TradeWorld.Item(offer.Currency) ?? Currencies.Vanilla();
            rect.Find("coin").GetComponent<Image>().sprite = currency?.m_itemData.GetIcon();
            rect.Find("price").GetComponent<TMP_Text>().text = offer.Price.ToString();

            bool selected = ReferenceEquals(SellService.Selected, offer);
            rect.GetComponent<Image>().color = selected ? RowSelected : Row;
            Button button = rect.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SellService.Select(offer));

            if (data != null)
                KitTooltip.Set(rect.gameObject, data.m_shared.m_name, data.GetTooltip());
        }

        private static void Unbind(RectTransform rect)
        {
            Button button = rect != null ? rect.GetComponent<Button>() : null;
            button?.onClick.RemoveAllListeners();
        }

        private static RectTransform Child(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public void Dispose()
        {
            list?.Dispose();
            if (panel != null)
                UnityEngine.Object.Destroy(panel.gameObject);
        }
    }
}
