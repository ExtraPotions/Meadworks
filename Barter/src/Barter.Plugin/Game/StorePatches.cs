// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// The integration points follow TradersExtended 2.0.5 (Unlicense, pin 2d5dd879): Trader.GetAvailableItems, StoreGui.Show/Hide/FillList/
// CanAfford/BuySelectedItem/SellItem and EnvMan.UpdateTriggers. The patches are written for Barter; each is owned by Plugin and removed
// with it (Harmony.UnpatchSelf). Every patch does nothing unless Barter is active for the session.
using System;
using System.Collections.Generic;
using ExtraPotions.Barter.Config;
using ExtraPotions.Barter.Currency;
using ExtraPotions.Barter.Trading;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>The morning purse refill. Runs on single player, the host and the dedicated server (no UI needed).</summary>
    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.UpdateTriggers))]
    internal static class EnvMan_UpdateTriggers_Refill
    {
        private static bool IsMorning(float oldDayFraction, float newDayFraction) =>
            oldDayFraction > 0.2f && oldDayFraction < 0.25f && newDayFraction >= 0.25f && newDayFraction < 0.3f;

        private static void Postfix(float oldDayFraction, float newDayFraction)
        {
            if (!Plugin.Active || ZNet.instance == null || !ZNet.instance.IsServer() || !IsMorning(oldDayFraction, newDayFraction))
                return;
            bool timing = Plugin.Log != null && Plugin.Log.IsEnabled(LogSeverity.Debug);
            int frame = timing ? Time.frameCount : 0;
            bool completed = false;
            if (timing) RefillTiming.Begin();
            try
            {
                if (Purses.Refill() && ZRoutedRpc.instance != null)
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ShowMessage", (int)MessageHud.MessageType.TopLeft, BarterText.Token("purse_refilled"));
                completed = true;
            }
            catch (Exception error)
            {
                Plugin.Log?.Warning("Purse refill failed: " + error.GetType().Name + ": " + error.Message);
            }
            finally
            {
                if (timing)
                {
                    RefillTiming.Result cost = RefillTiming.End();
                    // Diagnostics must not let a failing log listener escape the morning hook.
                    try {
                        Plugin.Log.Debug(FormattableString.Invariant($"stall-timing event=morning-refill frame={frame} thread={System.Threading.Thread.CurrentThread.ManagedThreadId} old_fraction={oldDayFraction:R} new_fraction={newDayFraction:R} day={cost.Day} backend={ZNet.World?.m_fileSource} completed={completed} world_zdos={ZDOMan.instance?.m_objectsByID.Count ?? 0} scanned={cost.Scanned} traders={cost.Traders} refilled={cost.Refilled} frequency={System.Diagnostics.Stopwatch.Frequency} total_ticks={cost.TotalTicks} scan_and_rules_ticks={cost.TotalTicks - cost.AuthorityRead.Ticks - cost.AuthorityWrite.Ticks} auth_read_count={cost.AuthorityRead.Count} auth_read_ticks={cost.AuthorityRead.Ticks} auth_write_count={cost.AuthorityWrite.Count} auth_write_ticks={cost.AuthorityWrite.Ticks} local_read_count={cost.LocalRead.Count} local_read_ticks={cost.LocalRead.Ticks} local_write_count={cost.LocalWrite.Count} local_write_ticks={cost.LocalWrite.Ticks} cloud_read_count={cost.CloudRead.Count} cloud_read_ticks={cost.CloudRead.Ticks} cloud_write_count={cost.CloudWrite.Count} cloud_write_ticks={cost.CloudWrite.Ticks} promote_count={cost.Promote.Count} promote_ticks={cost.Promote.Ticks} flush_count={cost.Flush.Count} flush_ticks={cost.Flush.Ticks} cloud_finish_count={cost.CloudFinish.Count} cloud_finish_ticks={cost.CloudFinish.Ticks}"));
                    } catch { }
                }
            }
        }
    }

    /// <summary>The trader's list: Barter's rules are applied after every other mod has had its say.</summary>
    [HarmonyPatch(typeof(Trader), nameof(Trader.GetAvailableItems))]
    internal static class Trader_GetAvailableItems_Lists
    {
        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static Exception Finalizer(Exception __exception, Trader __instance, ref List<Trader.TradeItem> __result)
        {
            if (__exception != null || !Plugin.Active)
                return __exception;
            try
            {
                TradeOffers.Apply(__instance, ref __result);
            }
            catch (Exception error)
            {
                Plugin.Log?.Warning("Trader list failed for " + (__instance != null ? __instance.name : "?") + ": " + error.GetType().Name + ": " + error.Message);
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.Show))]
    internal static class StoreGui_Show_Barter
    {
        private static Trader before;

        private static void Prefix(StoreGui __instance, Trader trader)
        {
            before = StoreGui.IsVisible() ? __instance.m_trader : null;
            if (Plugin.Active)
                Currencies.Apply(__instance, trader);
        }

        private static void Postfix(StoreGui __instance, Trader trader)
        {
            StoreUi.Shown(__instance);
            if (Plugin.Active)
                StoreUi.Opened(__instance, !ReferenceEquals(before, trader));
        }
    }

    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.Hide))]
    internal static class StoreGui_Hide_Barter
    {
        private static void Postfix(StoreGui __instance)
        {
            Currencies.Restore(__instance);
            StoreUi.Closed(__instance);
        }
    }

    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.Update))]
    internal static class StoreGui_Update_Barter
    {
        private static bool Prefix(StoreGui __instance) => !Plugin.Active || !StoreUi.BeforeNativeUpdate(__instance);

        private static void Postfix(StoreGui __instance)
        {
            StoreUi.Tick(__instance);
        }
    }

    /// <summary>After the game filled the buy list: currency icons, affordability in the offer's own currency, and the quality bought.</summary>
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.FillList))]
    internal static class StoreGui_FillList_Barter
    {
        private static void Postfix(StoreGui __instance)
        {
            if (!Plugin.Active || __instance.m_trader == null || Player.m_localPlayer == null)
                return;

            List<Trader.TradeItem> items = __instance.m_trader.GetAvailableItems();
            Inventory inventory = Player.m_localPlayer.GetInventory();
            for (int i = 0; i < items.Count && i < __instance.m_itemList.Count; i++)
            {
                GameObject element = __instance.m_itemList[i];
                OfferInfo info = TradeOffers.InfoOf(items[i]);
                ItemDrop currency = TradeOffers.CurrencyOf(info, __instance);
                Currencies.SetIcons(element, currency);
                if (info == null || !info.FromList)
                    continue;

                bool affordable = items[i].m_price <= Currencies.Count(inventory, currency);
                Image icon = element.transform.Find("icon")?.GetComponent<Image>();
                if (icon != null)
                    icon.color = affordable ? Color.white : new Color(1f, 0f, 1f, 0f);
                TMP_Text name = element.transform.Find("name")?.GetComponent<TMP_Text>();
                if (name != null)
                {
                    name.color = affordable ? Color.white : Color.grey;
                    if (info.Quality > 1)
                        name.text += " <color=#add8e6ff>(" + info.Quality + ")</color>";
                }
                TMP_Text price = Utils.FindChild(element.transform, "price")?.GetComponent<TMP_Text>();
                if (price != null)
                    price.color = affordable ? Color.white : Color.grey;
            }
            SellService.Refresh(__instance);
        }
    }

    /// <summary>Affordability and the buy price of a list offer count the offer's own currency.</summary>
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.CanAfford))]
    internal static class StoreGui_CanAfford_Barter
    {
        private static bool Prefix(StoreGui __instance, Trader.TradeItem item, ref bool __result)
        {
            OfferInfo info = Plugin.Active ? TradeOffers.InfoOf(item) : null;
            if (info == null || !info.FromList || Player.m_localPlayer == null)
                return true;
            __result = item.m_price <= Currencies.Count(Player.m_localPlayer.GetInventory(), TradeOffers.CurrencyOf(info, __instance));
            return false;
        }
    }

    /// <summary>All active purchases use the checked inventory exchange and server reservation.</summary>
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.BuySelectedItem))]
    internal static class StoreGui_BuySelectedItem_Barter
    {
        // A void prefix without ref arguments still runs when Burrow has suppressed the purchase.
        [HarmonyPrefix]
        private static void NotifyHeld(StoreGui __instance)
        {
            Player player = Player.m_localPlayer;
            if (Plugin.Active && __instance.m_selectedItem != null && player != null &&
                OptionalTradeIntegrations.InventoryHeld(player.GetInventory()))
                player.Message(MessageHud.MessageType.Center, BarterText.Text("trade_inventory_held"));
        }

        private static bool Prefix(StoreGui __instance)
        {
            Trader.TradeItem item = __instance.m_selectedItem;
            if (Plugin.AwaitingRules)
                return false;
            if (!Plugin.Active)
                return true;
            if (item == null)
                return false;
            OfferInfo info = TradeOffers.InfoOf(item);
            if (__instance.CanAfford(item))
                BuyService.Buy(__instance, item, info);
            return false;
        }
    }

    /// <summary>
    /// The game finds the selected row by reference. When the purse moves a price the copies are new objects, so the selection is found
    /// again by the offer it stands for instead of falling back to the first row.
    /// </summary>
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.GetSelectedItemIndex))]
    internal static class StoreGui_GetSelectedItemIndex_Barter
    {
        private static void Postfix(StoreGui __instance, ref int __result)
        {
            Trader.TradeItem selected = __instance.m_selectedItem;
            if (!Plugin.Active || selected == null || __instance.m_trader == null)
                return;

            List<Trader.TradeItem> items = __instance.m_trader.GetAvailableItems();
            if (__result >= 0 && __result < items.Count && ReferenceEquals(items[__result], selected))
                return;

            object origin = TradeOffers.OriginOf(selected);
            for (int i = 0; i < items.Count; i++)
                if (ReferenceEquals(TradeOffers.OriginOf(items[i]), origin))
                {
                    __result = i;
                    return;
                }
        }
    }

    /// <summary>
    /// The game's Sell button sells the offer selected in Barter's sell list, but only while that list is shown: with the sell panel
    /// turned off the button is the game's own again, so it never sells an offer the player cannot see.
    /// </summary>
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.UpdateSellButton))]
    internal static class StoreGui_UpdateSellButton_Barter
    {
        internal static bool Takeover => Plugin.Active && TradeLists.Ready && StoreUi.SellPanelAllowed;

        private static bool Prefix(StoreGui __instance)
        {
            if (!Takeover)
            {
                if (!Plugin.Active) return true;
                __instance.m_sellButton.interactable = SellService.CanSellValuables(__instance, out _);
                return false;
            }
            __instance.m_sellButton.interactable = SellService.CanSell(__instance, out _);
            return false;
        }
    }

    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.SellItem))]
    internal static class StoreGui_SellItem_Barter
    {
        private static bool Prefix(StoreGui __instance)
        {
            if (Plugin.AwaitingRules)
                return false;
            if (!Plugin.Active)
                return true;
            if (StoreGui_UpdateSellButton_Barter.Takeover)
                SellService.Sell(__instance);
            else
                SellService.SellValuables(__instance);
            return false;
        }
    }

    /// <summary>Carried items changed: the sell list is rebuilt at the next refresh.</summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.Changed))]
    internal static class Inventory_Changed_Barter
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Inventory __instance) => SafeTradeInventory.ObserveBeforeChanged(__instance);

        private static void Postfix(Inventory __instance)
        {
            if (__instance != null && !__instance.m_temoraryInventory && Player.m_localPlayer != null && __instance == Player.m_localPlayer.GetInventory())
                StoreUi.InventoryChanged();
        }
    }
}
