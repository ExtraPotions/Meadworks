// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Barter.Config;
using UnityEngine;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>Owns the purse card and the sell panel next to the store window and keeps them in step with it.</summary>
    internal static class StoreUi
    {
        private const float RefreshSeconds = 0.25f;

        private static PurseCard purse;
        private static SellPanel sell;
        private static StoreGui owner;
        private static bool isOpen;
        private static float next;
        private static bool inventoryDirty;

        internal static void Shown(StoreGui store)
        {
            if (!ReferenceEquals(owner, store))
            {
                Dispose();
                owner = store;
            }
            isOpen = true;
        }

        internal static void Opened(StoreGui store, bool newTrader)
        {
            Ensure(store);
            isOpen = true;
            if (newTrader)
            {
                SellService.Clear();
                sell?.ResetFilter();
            }
            SellService.Refresh(store);
            Apply(store);
        }

        internal static void Closed(StoreGui store)
        {
            if (!ReferenceEquals(owner, store)) return;
            isOpen = false;
            TraderServicesWindow.Close();
            ListEditorWindow.Close();
            sell?.ReleaseController();
            purse?.Show(false);
            sell?.Show(false);
            SellService.Clear();
        }

        /// <summary>The player's inventory changed: the sell list is built again at the next refresh.</summary>
        internal static void InventoryChanged() => inventoryDirty = true;

        internal static void Tick(StoreGui store)
        {
            if (!isOpen || !ReferenceEquals(owner, store) || store == null || store.m_trader == null ||
                Player.m_localPlayer == null || store.m_rootPanel == null || !store.m_rootPanel.activeSelf)
                return;

            Ensure(store);
            if (inventoryDirty)
            {
                inventoryDirty = false;
                SellService.Refresh(store);
            }
            purse?.Fit();
            if (Time.unscaledTime < next)
                return;
            next = Time.unscaledTime + RefreshSeconds;
            if (Plugin.Active) Currencies.Apply(store, store.m_trader);
            else Currencies.Restore(store);
            TraderServicesWindow.Refresh();
            Apply(store);
        }

        internal static bool SellPanelAllowed => Plugin.Settings != null && Plugin.Settings.Get(BarterSettings.Interface.SellPanel) && !OptionalTradeIntegrations.EpicLootAdventure;

        internal static void RulesChanged()
        {
            next = 0; inventoryDirty = true;
            if (!isOpen || owner == null || owner.m_trader == null || Player.m_localPlayer == null ||
                owner.m_rootPanel == null || !owner.m_rootPanel.activeSelf || !Plugin.Active) return;
            try
            {
                Currencies.Apply(owner, owner.m_trader);
                owner.FillList();
            }
            catch (Exception error)
            {
                // UI refresh failure must not interrupt accepted list publication to joining clients.
                Plugin.Log?.Warning("Store refresh failed after rules changed: " + error.GetType().Name + ": " + error.Message);
            }
        }

        internal static bool BeforeNativeUpdate(StoreGui store)
        {
            if (store == null || store.m_rootPanel == null || !store.m_rootPanel.activeSelf) return false;
            if (ShouldClose(store))
            {
                store.Hide();
                return true;
            }
            if (TraderServicesWindow.IsOpen) { TraderServicesWindow.HandleInput(); return true; }
            if (ListEditorWindow.IsOpen) { ListEditorWindow.HandleInput(); return true; }
            if (!KitTextField.Typing && MeadworksInput.GamepadActive && MeadworksInput.PadDown(PadButton.Y))
            { MeadworksInput.PadConsume(PadButton.Y); TraderServicesWindow.Open(store); return true; }
            return sell != null && sell.BeforeNativeUpdate(store);
        }

        // StoreGui.Update owns these guards, so keep them ahead of Barter's input takeover.
        private static bool ShouldClose(StoreGui store)
        {
            Player player = Player.m_localPlayer;
            if (store.m_trader == null || player == null || player.IsDead() || player.InCutscene()) return true;
            if (Vector3.Distance(store.m_trader.transform.position, player.transform.position) > store.m_hideDistance) return true;
            if (InventoryGui.IsVisible() || Minimap.IsOpen()) return true;
            if ((Chat.instance == null || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !Menu.IsVisible() &&
                TextViewer.instance != null && !TextViewer.instance.IsVisible() &&
                (ZInput.GetButtonDown("JoyButtonB") || ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("Use")))
            {
                ZInput.ResetButtonStatus("JoyButtonB");
                return true;
            }
            return false;
        }

        internal static void ModalOpened() => sell?.ReleaseController();

        private static void Ensure(StoreGui store)
        {
            if (!ReferenceEquals(owner, store) || (purse != null && !purse.Alive) || (sell != null && !sell.Alive))
            {
                bool wasOpen = isOpen && ReferenceEquals(owner, store);
                Dispose();
                owner = store;
                isOpen = wasOpen;
            }
            if (purse == null)
                purse = PurseCard.Create(store);
            if (sell == null)
                sell = SellPanel.Create(store);
        }

        private static void Apply(StoreGui store)
        {
            if (Plugin.Settings == null)
                return;
            bool active = Plugin.Active && store.m_trader != null;
            bool showPurse = active && Plugin.Settings.Get(BarterSettings.Interface.ShowPurse) && Purses.Applies(store.m_trader);
            bool showSell = active && TradeLists.Ready && SellPanelAllowed;
            purse?.Show(active);
            sell?.Show(showSell);
            if (active)
                purse?.Update(store, showPurse);
            if (showSell)
                sell?.Update(store);
        }

        internal static void Dispose()
        {
            isOpen = false;
            TraderServicesWindow.Dispose();
            ListEditorWindow.Dispose();
            purse?.Dispose();
            sell?.Dispose();
            purse = null;
            sell = null;
            owner = null;
        }
    }
}
