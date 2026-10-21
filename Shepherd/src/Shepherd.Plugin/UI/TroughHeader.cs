// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Uses the shared kit and native grid edge/focus, without changing inventory groups or container contents.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Feeding;
using ExtraPotions.Shepherd.Runtime.Integration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ExtraPotions.Shepherd.Runtime.UI
{
    internal static class TroughHeader
    {
        internal const string LastFeed = "shepherd.last_feed_sent";
        static Harmony? harmony;
        static RectTransform? panel;
        static TMP_Text? stock, last;
        static Button? book;
        static InventoryGrid? grid;
        static Container? source;
        static readonly List<KitIconButton> species = new List<KitIconButton>();
        static float next;
        static ZNetScene? speciesScene;
        static int prefabCount;
        static string speciesLanguage = "", speciesFilter = "";
        static string language = "";
        static string Text(string key) => ShepherdLocalization.Text(key);
        static string Local(string token) => LoreText.Literal(global::Localization.instance != null ? global::Localization.instance.Localize(token) : token);
        static bool Available => Plugin.Ready && Plugin.AllowsUi && NativeFeeding.FeedingActive && InventoryGui.IsVisible()
            && InventoryGui.instance != null && InventoryGui.instance.m_currentContainer != null
            && TroughPieces.IsTrough(InventoryGui.instance.m_currentContainer) && FeedSources.IsUsable(InventoryGui.instance.m_currentContainer);
        internal static void Initialize()
        {
            Shutdown(); harmony = new Harmony(Plugin.Guid + ".trough_header");
            harmony.CreateClassProcessor(typeof(GridInput)).Patch();
        }
        internal static void Tick()
        {
            if (!Available) { Clear(); return; }
            var gui = InventoryGui.instance;
            if (source != gui.m_currentContainer || grid != gui.m_containerGrid || panel == null)
            {
                Clear(); source = gui.m_currentContainer; grid = gui.m_containerGrid;
                if (grid == null || KitCanvas.Font == null) return;
                float width = Mathf.Clamp(((RectTransform)grid.transform).rect.width, 400, 700);
                panel = KitPanel.Create(grid.transform, "Shepherd_TroughHeader", new Vector2(width, 88));
                if (panel == null) return;
                panel.anchorMin = panel.anchorMax = new Vector2(0, 1); panel.pivot = Vector2.zero;
                panel.anchoredPosition = new Vector2(0, 18);
                stock = KitAlmanacDialog.Label(panel, "Stock", "", new Vector2(10, -6), new Vector2(width-190, 24), 14);
                last = KitAlmanacDialog.Label(panel, "LastFeed", "", new Vector2(10, -31), new Vector2(width-20, 23), 12);
                stock.richText = last.richText = false;
                book = KitButton.Create(panel, "HerdBook", "", OpenBook, new Vector2(width-175, -4), new Vector2(165, 30));
                var navigation = book.navigation; navigation.mode = Navigation.Mode.None; book.navigation = navigation;
                next = 0;
            }
            if (book != null) book.interactable = HerdBook.Active;
            if (Time.unscaledTime < next && language == MeadworksBlockedOptions.Language) return;
            next = Time.unscaledTime + 1; language = MeadworksBlockedOptions.Language;
            Refresh();
        }
        static void OpenBook()
        {
            if (!Available || !HerdBook.Active || MeadworksInput.Typing || Menu.IsVisible()) return;
            HerdBook.Open();
        }
        static void Refresh()
        {
            if (source == null || !FeedSources.IsUsable(source) || panel == null || book == null) return;
            int total = source.GetInventory().GetAllItems().Sum(item => item.m_stack);
            stock!.text = ShepherdLocalization.Format("trough_stock", total);
            long timestamp = source.m_nview.GetZDO().GetLong(LastFeed);
            double elapsed = timestamp > 0 ? (Herd.Now.Ticks - timestamp) / (double)TimeSpan.TicksPerSecond : -1;
            last!.text = elapsed >= 0 ? ShepherdLocalization.Format("trough_last", Duration(elapsed)) : Text("trough_no_meal");
            GameButtons.Caption(book.gameObject, Text("book_title"));
            var scene = ZNetScene.instance; if (scene == null) return;
            var filter = ShepherdSettings.DisallowFeed.Value + "\n" + ShepherdSettings.DisallowAnimals.Value;
            if (speciesScene == scene && prefabCount == scene.m_prefabs.Count && speciesLanguage == language && speciesFilter == filter) return;
            speciesScene = scene; prefabCount = scene.m_prefabs.Count; speciesLanguage = language; speciesFilter = filter;
            foreach (var icon in species) if (icon.Button != null) Object.Destroy(icon.Button.gameObject);
            species.Clear();
            var candidates = scene.m_prefabs.Where(p => p != null).Select(p => p.GetComponent<MonsterAI>())
                .Where(ai => ai != null && ai.GetComponent<Character>() != null && ai.GetComponent<Tameable>() != null && ai.m_consumeItems != null && !NativeFeeding.AnimalDisallowed(global::Utils.GetPrefabName(ai.gameObject))
                    && ai.m_consumeItems.Any(food => food != null && !NativeFeeding.FoodDisallowed(global::Utils.GetPrefabName(food.gameObject))))
                .GroupBy(ai => global::Utils.GetPrefabName(ai.gameObject)).Select(g => g.First()).OrderBy(ai => Local(ai.GetComponent<Character>().m_name), StringComparer.Ordinal).ToArray();
            string Details(MonsterAI ai) => Local(ai.GetComponent<Character>().m_name) + ": " + string.Join(", ", ai.m_consumeItems.Where(food => food != null && !NativeFeeding.FoodDisallowed(global::Utils.GetPrefabName(food.gameObject)))
                .Select(food => Local(food.m_itemData.m_shared.m_name)).Distinct());
            var all = string.Join("\n", candidates.Select(Details));
            KitTooltip.Set(panel.gameObject, Text("trough_foods"), all.Length <= 8192 ? all : all.Substring(0,8192));
            int slots = Math.Max(1, Math.Min(12, (int)(panel.rect.width-20) / 32));
            foreach (var ai in candidates.Take(slots))
            {
                var icon = KitIconButton.Create(panel, "Species" + species.Count, KitGlyph.SmallGrid, new Vector2(10 + species.Count*32, -57), 26);
                if (icon == null) continue;
                var drops = ai.GetComponent<CharacterDrop>();
                var trophy = drops != null ? drops.m_drops.Select(d => d.m_prefab != null ? d.m_prefab.GetComponent<ItemDrop>() : null)
                    .FirstOrDefault(d => d != null && d.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy) : null;
                var item = trophy != null ? trophy : ai.m_consumeItems.FirstOrDefault(d => d != null);
                if (item != null) icon.SetSprite(item.m_itemData.GetIcon(), false);
                icon.Tooltip(Local(ai.GetComponent<Character>().m_name), Details(ai));
                icon.Button.interactable = false; species.Add(icon);
            }
        }
        static string Duration(double seconds)
        {
            int value = (int)Math.Min(int.MaxValue, Math.Max(0, seconds));
            return value < 60 ? ShepherdLocalization.Format("time_seconds", value)
                : value < 3600 ? ShepherdLocalization.Format("time_minutes", value/60)
                : ShepherdLocalization.Format("time_hours_minutes", value/3600, value/60%60);
        }
        // The native grid already owns edge navigation and A. Redirect only its lower edge to the owned button;
        // while that button owns focus, suppress the grid's separate A mutation to avoid moving an item too.
        [HarmonyPatch(typeof(InventoryGrid), "UpdateGamepad")]
        sealed class GridInput
        {
            [HarmonyPrefix] static bool Prefix(InventoryGrid __instance)
            {
                if (__instance != grid || !Available || book == null || !book.interactable || !__instance.m_uiGroup.IsActive
                    || !MeadworksInput.GamepadActive || ZInput.IsTouchActive() || !MeadworksInput.Allows(InputContext.Window)
                    || KitAlmanacDialog.Active != null) return true;
                var events = EventSystem.current; if (events == null) return true;
                bool down = ZInput.GetButtonDown("JoyDPadDown") || ZInput.GetButtonDown("JoyLStickDown");
                bool up = ZInput.GetButtonDown("JoyDPadUp") || ZInput.GetButtonDown("JoyLStickUp");
                if (events.currentSelectedGameObject == book.gameObject)
                {
                    if (MeadworksInput.PadDown(PadButton.A))
                    {
                        MeadworksInput.PadConsume(PadButton.A); book.onClick.Invoke();
                    }
                    else if (up)
                    {
                        ZInput.ResetButtonStatus("JoyDPadUp"); ZInput.ResetButtonStatus("JoyLStickUp");
                        var selected = __instance.GetGamepadSelectedElement(); if (selected != null) events.SetSelectedGameObject(selected.gameObject);
                    }
                    else if (down)
                    {
                        ZInput.ResetButtonStatus("JoyDPadDown"); ZInput.ResetButtonStatus("JoyLStickDown");
                        __instance.OnMoveToLowerInventoryGrid?.Invoke(__instance.SelectionGridPosition);
                    }
                    return false;
                }
                if (down && __instance.jumpToNextContainer && __instance.m_selected.y == __instance.m_height-1)
                {
                    ZInput.ResetButtonStatus("JoyDPadDown"); ZInput.ResetButtonStatus("JoyLStickDown");
                    events.SetSelectedGameObject(book.gameObject); return false;
                }
                return true;
            }
        }
        static void Clear()
        {
            var events = EventSystem.current;
            if (events != null && panel != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(panel))
                events.SetSelectedGameObject(null);
            if (panel != null) Object.Destroy(panel.gameObject);
            panel = null; stock = last = null; book = null; grid = null; source = null; species.Clear(); language = ""; speciesScene = null; prefabCount = 0; speciesLanguage = speciesFilter = "";
        }
        internal static void Shutdown() { harmony?.UnpatchSelf(); harmony = null; Clear(); }
    }
}
