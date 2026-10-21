// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using HarmonyLib;
using ExtraPotions.Kindle.Repair;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>
    /// The station's Repair button repairs every worn item this station can repair in one press (<see cref="RepairRule"/>,
    /// the game's own rule; no cost, the station's repair effect and sound once, one "Repaired N items" message). The
    /// game's button keeps its native placement and bindings; only its press, tooltip and enabled state change.
    /// Salvage remains on its horizontal crafting tab.
    /// </summary>
    public static class KindleRepair
    {
        static readonly List<ItemDrop.ItemData> worn = new List<ItemDrop.ItemData>();
        static Harmony patches;
        static float nextCount;
        static int repairable;

        public static void Initialize()
        {
            Shutdown();
            patches = new Harmony(Plugin.Guid + ".repair");
            patches.CreateClassProcessor(typeof(KindleRepairPatches)).Patch();
        }

        public static void Shutdown()
        {
            patches?.UnpatchSelf(); patches = null;
            nextCount = 0f; tipOwner = null;
        }

        internal static void Guard(Action action)
        {
            try { action(); }
            catch (Exception error) { KindleSalvage.Warn("Repair all: " + error.GetType().Name + ": " + error.Message); }
        }

        static bool Active => Plugin.GameplayAvailable && Plugin.AllowsUi && Player.m_localPlayer;

        /// <summary>The worn items the station the player stands at can repair, by the game's rule.</summary>
        public static List<ItemDrop.ItemData> Repairable(Player player)
        {
            var found = new List<ItemDrop.ItemData>();
            if (!player) return found;
            var station = player.GetCurrentCraftingStation();
            bool noCost = player.NoCostCheat();
            if (!station && !noCost) return found;
            if (station && !station.CheckUsable(player, false)) return found;
            worn.Clear(); player.GetInventory().GetWornItems(worn);
            foreach (var item in worn)
                if (RepairRule.Qualifies(Facts(item, station, noCost))) found.Add(item);
            return found;
        }

        public static RepairFacts Facts(ItemDrop.ItemData item, CraftingStation station, bool noCost)
        {
            var recipe = ObjectDB.instance ? ObjectDB.instance.GetRecipe(item) : null;
            return new RepairFacts
            {
                CanBeRepaired = item.m_shared.m_canBeReparied, NoCost = noCost,
                Station = station ? station.m_name : null, StationLevel = station ? station.GetLevel() : 0,
                HasRecipe = recipe, CraftingStation = recipe && recipe.m_craftingStation ? recipe.m_craftingStation.m_name : null,
                RepairStation = recipe && recipe.m_repairStation ? recipe.m_repairStation.m_name : null,
                MinStationLevel = recipe ? recipe.m_minStationLevel : 0, ItemWorldLevel = item.m_worldLevel, GameWorldLevel = Game.m_worldLevel
            };
        }

        /// <summary>The press: every qualifying item to full durability, the crafting skill raised for each as the game does.</summary>
        internal static bool Pressed(InventoryGui gui)
        {
            if (!Active) return true;
            var player = Player.m_localPlayer;
            var station = player.GetCurrentCraftingStation();
            var items = Repairable(player);
            foreach (var item in items)
            {
                player.RaiseSkill(Skills.SkillType.Crafting, 1f - item.m_durability / item.GetMaxDurability());
                item.m_durability = item.GetMaxDurability();
            }
            if (items.Count > 0 && station) station.m_repairItemDoneEffects.Create(station.transform.position, Quaternion.identity);
            player.Message(MessageHud.MessageType.Center, items.Count == 0 ? KindleText.Text("repair_all_none")
                : items.Count == 1 ? KindleText.Text("repair_all_done_one") : KindleText.Format("repair_all_done", items.Count));
            if (gui.m_uiGroups != null && gui.m_uiGroups.Length > 3) gui.SetActiveGroup(gui.m_uiGroups[3]);
            nextCount = 0f;
            gui.UpdateRepair();
            gui.UpdateCraftingPanel();
            return false;
        }

        internal static void StationOpened(CraftingStation station, Humanoid user, bool repeat, bool opened, bool ranOriginal)
        {
            if (!opened || !ranOriginal || repeat || !Active || !station || !station.m_canRepair
                || Plugin.Settings?.Get(ExtraPotions.Kindle.Config.KindleSettings.Repair.OnStationOpen) != true
                || user != Player.m_localPlayer || !(user is Player player) || player.IsDead() || player.InCutscene() || player.IsTeleporting()) return;
            var view = player.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || !view.IsOwner() || player.GetCurrentCraftingStation() != station
                || !station.InUseDistance(player) || !station.CheckUsable(player, false)
                || !InventoryGui.instance || !InventoryGui.IsVisible() || Repairable(player).Count == 0) return;
            Pressed(InventoryGui.instance);
        }

        /// <summary>
        /// Off a repairing station the game hides Repair, its plate and its selection every frame, and <see cref="AfterRepair"/>
        /// showed the button again in the same frame. The button was disabled and enabled each frame the inventory was open,
        /// which rebuilt the crafting panel's canvas and layout every frame (playtest 2026-10-20, item 25). While the crafting
        /// column keeps the grayed button, the game's hiding is done here without the button and the game's method is skipped.
        /// </summary>
        internal static bool BeforeRepair(InventoryGui gui)
        {
            if (!Active || !gui.m_repairButton) return true;
            var player = Player.m_localPlayer;
            var station = player.GetCurrentCraftingStation();
            bool canRepair = station ? station.m_canRepair : player.NoCostCheat();
            if (canRepair || !(gui.m_crafting && gui.m_crafting.gameObject.activeInHierarchy)) return true;
            if (gui.m_repairPanel) gui.m_repairPanel.gameObject.SetActive(false);
            if (gui.m_repairPanelSelection) gui.m_repairPanelSelection.gameObject.SetActive(false);
            return false;
        }

        static GameObject tipOwner;
        static int tipShown;

        /// <summary>After the game set Repair up this frame: its tooltip and enabled state.</summary>
        internal static void AfterRepair(InventoryGui gui)
        {
            if (!Active || !gui.m_repairButton) return;
            var player = Player.m_localPlayer;
            if (Time.unscaledTime >= nextCount)
            {
                nextCount = Time.unscaledTime + 0.25f;
                repairable = Repairable(player).Count;
                tipOwner = null;
            }
            var repair = gui.m_repairButton;
            var station = player.GetCurrentCraftingStation();
            bool noCost = player.NoCostCheat();
            bool canRepair = station ? station.m_canRepair : noCost;
            bool panel = gui.m_crafting && gui.m_crafting.gameObject.activeInHierarchy;
            // Off a repairing station the game hides Repair; the crafting column keeps it, grayed, with the reason (user, 2026-10-12 14:46).
            if (!canRepair && panel && !repair.gameObject.activeSelf) repair.gameObject.SetActive(true);
            if (repair.gameObject.activeSelf)
            {
                bool ready = canRepair && repairable > 0;
                repair.interactable = ready;
                if (gui.m_repairButtonGlow) gui.m_repairButtonGlow.gameObject.SetActive(ready);
                // Set again with the repairable count (four times a second) or when the text changes, not every frame.
                int tipKey = canRepair ? repairable : -1;
                if (tipOwner != repair.gameObject || tipKey != tipShown)
                {
                    string tip = !canRepair ? KindleText.Text("station_needed")
                        : repairable == 0 ? KindleText.Text("repair_all_tip_none") : repairable == 1 ? KindleText.Text("repair_all_tip_one") : KindleText.Format("repair_all_tip", repairable);
                    KitTooltip.Set(repair.gameObject, KindleText.Text("repair_all"), tip);
                    tipOwner = repair.gameObject; tipShown = tipKey;
                }
            }
            Back(gui);
        }

        /// <summary>
        /// The game's wood plate behind Repair (m_repairPanel, a sibling shown only at a repairing station) sits above and
        /// right of the button once the crafting panel is resized; it goes directly behind it (user, 2026-10-14 13:39):
        /// centered on Repair, <see cref="StationStack.BackingMargin"/> wider on every side.
        /// </summary>
        static void Back(InventoryGui gui)
        {
            var plate = gui.m_repairPanel as RectTransform;
            var repair = (RectTransform)gui.m_repairButton.transform;
            if (!plate || !plate.gameObject.activeInHierarchy || !repair.gameObject.activeInHierarchy || !(plate.parent is RectTransform parent)) return;
            if (repair.rect.width < 1f || repair.rect.height < 1f) return;
            var (width, height) = StationStack.Backing(repair.rect.width, repair.rect.height);
            if (Mathf.Abs(plate.rect.width - width) > 0.01f) plate.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            if (Mathf.Abs(plate.rect.height - height) > 0.01f) plate.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            Vector2 shift = parent.InverseTransformPoint(repair.TransformPoint(repair.rect.center)) - parent.InverseTransformPoint(plate.TransformPoint(plate.rect.center));
            if (shift.sqrMagnitude > 0.0001f) plate.anchoredPosition += shift;
        }

    }

    [HarmonyPatch]
    internal static class KindleRepairPatches
    {
        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Interact)), HarmonyPrefix]
        static void Opening(CraftingStation __instance, Humanoid user, bool repeat, out bool __state)
        {
            // Native Interact returns false even after opening; capture the actual opening transition instead.
            __state = !repeat && user && user == Player.m_localPlayer
                && (Player.m_localPlayer.GetCurrentCraftingStation() != __instance || !InventoryGui.IsVisible());
        }

        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Interact)), HarmonyPostfix]
        static void Opened(CraftingStation __instance, Humanoid user, bool repeat, bool __state, bool __runOriginal)
            => KindleRepair.Guard(() => KindleRepair.StationOpened(__instance, user, repeat, __state, __runOriginal));

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnRepairPressed)), HarmonyPrefix]
        static bool Pressed(InventoryGui __instance)
        {
            bool run = true;
            KindleRepair.Guard(() => run = KindleRepair.Pressed(__instance));
            return run;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRepair)), HarmonyPrefix]
        static bool Repairing(InventoryGui __instance)
        {
            try { return KindleRepair.BeforeRepair(__instance); }
            catch (Exception error) { KindleSalvage.Warn("Repair all: " + error.GetType().Name + ": " + error.Message); return true; }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRepair)), HarmonyPostfix]
        static void Updated(InventoryGui __instance)
        {
            // Every frame the inventory is open: no closure, unlike Guard.
            try { KindleRepair.AfterRepair(__instance); }
            catch (Exception error) { KindleSalvage.Warn("Repair all: " + error.GetType().Name + ": " + error.Message); }
        }
    }
}
