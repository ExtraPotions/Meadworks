// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Barter.Runtime
{
    internal static class TraderServicesWindow
    {
        private static KitWindow window;
        private static BarterModalInput input;
        private static TMP_Text buyback, repair, recovery;
        private static Button redeem, mend, edit, resolve;
        private static StoreGui store;
        private static int buyIndex, repairIndex;
        internal static bool IsOpen => window != null && window.IsOpen;

        internal static void Open(StoreGui owner)
        {
            if (IsOpen || ListEditorWindow.IsOpen) return;
            store = owner; buyIndex = repairIndex = 0;
            if (window == null) Create();
            StoreUi.ModalOpened();
            input?.Reset(); window?.Open(); Refresh();
        }

        internal static void HandleInput() => input?.Tick();

        private static void Create()
        {
            window = KitWindow.Create("barter_services", new Vector2(600, 450));
            if (window == null) return;
            input = new BarterModalInput(window);
            Label("services_title", 18, 16, 560, 30, 22);
            buyback = Label("buyback_help", 18, 60, 560, 65, 16);
            Add("previous", 18, 136, 110, () => { buyIndex--; Refresh(); }, 0, 0);
            Add("next", 140, 136, 110, () => { buyIndex++; Refresh(); }, 1, 0);
            redeem = Add("buyback_button", 264, 136, 314, () =>
            {
                var offers = BuybackService.Offers(store);
                if (offers.Count > 0) BuybackService.Redeem(store, offers[Mathf.Clamp(buyIndex, 0, offers.Count - 1)]);
                Refresh();
            }, 2, 0);
            repair = Label("repair_help", 18, 188, 560, 65, 16);
            Add("previous", 18, 264, 110, () => { repairIndex--; Refresh(); }, 0, 1);
            Add("next", 140, 264, 110, () => { repairIndex++; Refresh(); }, 1, 1);
            mend = Add("repair_button", 264, 264, 314, () =>
            {
                var offers = RepairService.Offers(store);
                if (offers.Count > 0) RepairService.Repair(store, offers[Mathf.Clamp(repairIndex, 0, offers.Count - 1)]);
                Refresh();
            }, 2, 1);
            recovery = Label("services_title", 18, 310, 560, 38, 14);
            resolve = Add("trade_recovery_button", 18, 356, 560, () => { TradeAuthority.RequestRecovery(); Refresh(); }, 0, 2);
            KitTooltip.Set(resolve.gameObject, BarterText.Text("trade_recovery_button"), BarterText.Text("trade_recovery_help"));
            edit = Add("editor_title", 18, 400, 270, () => { window.Close(); ListEditorWindow.Open(); }, 0, 3);
            Add("editor_close", 308, 400, 270, () => window.Close(), 2, 3);
        }

        internal static void Refresh()
        {
            if (window == null) return;
            if (store == null || store.m_trader == null || !StoreGui.IsVisible() || !Plugin.Active) { window.Close(); return; }
            if (!window.IsOpen) return;
            var receipts = BuybackService.Offers(store);
            buyIndex = Mathf.Clamp(buyIndex, 0, Math.Max(0, receipts.Count - 1));
            redeem.interactable = receipts.Count > 0;
            buyback.text = BarterText.Text("buyback_help") + "\n" + (receipts.Count == 0 ? BarterText.Text("buyback_empty") :
                receipts[buyIndex].Label + " ×" + receipts[buyIndex].Amount + ": " + receipts[buyIndex].Price + " " + Currencies.Name(TradeWorld.Item(receipts[buyIndex].Currency)));
            var repairs = RepairService.Offers(store);
            repairIndex = Mathf.Clamp(repairIndex, 0, Math.Max(0, repairs.Count - 1));
            mend.interactable = repairs.Count > 0;
            repair.text = BarterText.Text("repair_help") + "\n" + (repairs.Count == 0 ? BarterText.Text("repair_empty") :
                Localization.instance.Localize(repairs[repairIndex].Item.m_shared.m_name) + ": " + repairs[repairIndex].Price + " " + Currencies.Name(TradeWorld.Item(repairs[repairIndex].Currency)));
            edit.interactable = ListEditor.CanEdit;
            resolve.interactable = !TradeAuthority.RecoveryInFlight;
            recovery.text = TradeAuthority.RecoveryStatus;
            if (recovery.text.Length == 0 && !ExtraPotions.Meadworks.Crucible.Plugin.Session.IsAuthority)
                recovery.text = BarterText.Text("service_authority_only");
            KitTooltip.Set(recovery.gameObject, BarterText.Text("services_title"), recovery.text);
            recovery.raycastTarget = recovery.text.Length > 0;
            if (TradeAuthority.RecoveryBlocked) { redeem.interactable = false; mend.interactable = false; }
        }

        private static TMP_Text Label(string key, float x, float y, float width, float height, float size)
        {
            var label = KitCanvas.Text(window.Panel, key, BarterText.Text(key), size, Color.white, TextAlignmentOptions.TopLeft, true);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            label.raycastTarget = false; return label;
        }
        private static Button Add(string key, float x, float y, float width, Action action, int column, int row)
        {
            var button = KitButton.Create(window.Panel, key + row + column, BarterText.Text(key), action, new Vector2(x, y), new Vector2(width, 34));
            input.Add(button, column, row); return button;
        }
        internal static void Close() { window?.Close(); }
        internal static void Dispose() { window?.Destroy(); window = null; input = null; store = null; }
    }
}
