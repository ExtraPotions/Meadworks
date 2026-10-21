// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. AchievementSync is a behavior only reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexMarks
{
    private static Player? lastPlayer;
    private static bool lastInventoryGrant;
    private static bool On(string key) => HexAchievements.Available && HexMovement.Owned(Player.m_localPlayer)
        && Plugin.Settings?.Effective(key, false) == true;
    private static bool Local(Inventory inventory) => On("clear_inventory_marks")
        && ReferenceEquals(Player.m_localPlayer.GetInventory(), inventory);
    private static bool Owned(ZNetView view) => view != null && view.IsValid() && view.IsOwner();

    private static bool Clear(Inventory inventory, bool notify)
    {
        var changed = false;
        foreach (var item in inventory.GetAllItems())
            if (item != null && item.m_cheated) { item.m_cheated = false; changed = true; }
        if (changed && notify) inventory.Changed();
        return changed;
    }
    internal static void Tick()
    {
        var player = Player.m_localPlayer;
        var granted = On("clear_inventory_marks");
        if (granted && (player != lastPlayer || !lastInventoryGrant)) Clear(player.GetInventory(), notify: true);
        lastPlayer = player;
        lastInventoryGrant = granted;
    }
    internal static void Reset() { lastPlayer = null; lastInventoryGrant = false; }

    private struct Incoming
    {
        public ItemDrop.ItemData? Item;
        public bool Marked;
    }
    [HarmonyPatch]
    private static class Add
    {
        private static bool Prepare() => HexAchievements.Available;
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) });
            yield return AccessTools.DeclaredMethod(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData), typeof(Vector2i) });
            yield return AccessTools.DeclaredMethod(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) });
        }
        // C14 must observe the temporarily clean incoming mark before its guarded-stack snapshot.
        [HarmonyPriority(Priority.First + 100)]
        private static void Prefix(Inventory __instance, ItemDrop.ItemData item, out Incoming __state)
        {
            __state = default;
            if (!Local(__instance) || item == null) return;
            // Existing removals are an independent authorized change, even if the following add fails.
            Clear(__instance, notify: true);
            __state = new Incoming { Item = item, Marked = item.m_cheated };
            item.m_cheated = false;
        }
        private static Exception? Finalizer(Inventory __instance, Incoming __state, Exception? __exception)
        {
            // Native whole-stack insertion keeps this exact object. A merge or partial/failed
            // insertion leaves the original outside the target; preserve that source/residual mark.
            if (__state.Marked && __state.Item != null && __instance.GetAllItems().FindIndex(item => ReferenceEquals(item, __state.Item)) < 0
                && !__state.Item.m_cheated) __state.Item.m_cheated = true;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Inventory), "Changed")]
    private static class InventoryChanged
    {
        private static bool Prepare() => HexAchievements.Available;
        private static void Prefix(Inventory __instance, ref bool cheatedStateChanged)
        {
            if (!Local(__instance)) return;
            Clear(__instance, notify: false);
            cheatedStateChanged = false;
        }
    }

    private static void Container(Container container)
    {
        if (!On("clear_container_marks") || container == null || !Owned(container.m_nview)
            || container.m_loading || container.GetInventory() == null) return;
        Clear(container.GetInventory(), notify: true); // native OnContainerChanged saves the owned inventory
    }
    [HarmonyPatch(typeof(global::Container), "CheckForChanges")]
    private static class ContainerSync
    {
        private static bool Prepare() => HexAchievements.Available;
        private static void Postfix(global::Container __instance) => Container(__instance);
    }
    [HarmonyPatch(typeof(global::Container), "OnContainerChanged")]
    private static class ContainerSave
    {
        private static bool Prepare() => HexAchievements.Available;
        private static void Prefix(global::Container __instance)
        {
            if (On("clear_container_marks") && Owned(__instance.m_nview) && !__instance.m_loading)
                Clear(__instance.GetInventory(), notify: false);
        }
    }

    private static bool Drop(ItemDrop drop)
    {
        if (!On("clear_drop_marks") || drop == null || !Owned(drop.m_nview)
            || drop.m_itemData == null || !drop.m_itemData.m_cheated) return false;
        drop.m_itemData.m_cheated = false;
        return true;
    }
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Load))]
    private static class DropSync
    {
        private static bool Prepare() => HexAchievements.Available;
        private static void Postfix(ItemDrop __instance) { if (Drop(__instance)) __instance.Save(); }
    }
    [HarmonyPatch(typeof(ItemDrop), "Save")]
    private static class DropSave
    {
        private static bool Prepare() => HexAchievements.Available;
        private static void Prefix(ItemDrop __instance) => Drop(__instance);
    }
    [HarmonyPatch(typeof(ItemDrop), "SlowUpdate")]
    private static class DropRefresh
    {
        private static bool Prepare() => HexAchievements.Available;
        private static void Postfix(ItemDrop __instance) { if (Drop(__instance)) __instance.Save(); }
    }
}
