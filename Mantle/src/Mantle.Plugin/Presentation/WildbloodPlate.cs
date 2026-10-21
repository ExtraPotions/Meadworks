// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Mantle.Embedded.PlateColumn;
using Mantle.Core;
using TMPro;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using Object = UnityEngine.Object;

namespace Mantle.Presentation
{
    public static class WildbloodPlate
    {
        const string Product = "com.meadworks.wildblood";
        const string PlateId = "Mantle_Wildblood_Tier";
        static Func<Tuple<bool, string, string, string>> provider;
        static Player actor;
        static ZNet network;
        static InventoryGui gui;
        static RectTransform inventoryRoot;
        static long generation = -1, world;
        static string session;
        static float nextLook;
        static Plate plate;
        static GameObject tipHolder, literalTip;
        static Tuple<bool, string, string, string> shown;
        static string shownText = "";

        static bool WorldReady() => Plugin.WorldReady && InventoryState.Active && InventoryState.IsLocal(Player.m_localPlayer) &&
            ExtraPotions.Meadworks.Crucible.MeadworksHeadless.AllowsUi && ZNet.m_world != null &&
            ZNet.instance.GetWorldUID() == CruciblePlugin.Session.WorldId;
        static bool Current() => WorldReady() && ReferenceEquals(actor, Player.m_localPlayer) && ReferenceEquals(network, ZNet.instance) &&
            ReferenceEquals(gui, InventoryGui.instance) && gui != null && gui.m_player != null && ReferenceEquals(inventoryRoot, gui.m_player) &&
            generation == CruciblePlugin.Session.Generation && world == CruciblePlugin.Session.WorldId && session == CruciblePlugin.Session.SessionId;
        static bool Visible() => Current() && InventoryGui.IsVisible() && gui.m_player.gameObject.activeInHierarchy && Column.Active;
        static Tuple<bool, string, string, string> Read()
        {
            try { var raw = provider?.Invoke(); return WildbloodPlateReading.Valid(raw) ? raw : null; }
            catch { return null; }
        }
        public static bool Showing(string product)
        {
            if (product != Product || !Visible() || plate?.Rect == null || !plate.Rect.gameObject.activeInHierarchy ||
                plate.Rect.parent != gui.m_player.Find("PlateColumn_boxes") || shown == null ||
                plate.Text == null || !plate.Text.enabled || plate.Text.font == null || !plate.Text.gameObject.activeInHierarchy || plate.Text.text != shownText) return false;
            var current = Read();
            return current != null && current.Equals(shown);
        }
        public static void Tick()
        {
            if (!WorldReady() || InventoryGui.instance == null) { Reset(); return; }
            if (!Current())
            {
                Reset(); actor = Player.m_localPlayer; network = ZNet.instance; gui = InventoryGui.instance; inventoryRoot = gui.m_player;
                generation = CruciblePlugin.Session.Generation; world = CruciblePlugin.Session.WorldId; session = CruciblePlugin.Session.SessionId;
            }
            if (!Visible()) { Hide(); return; }
            if (provider == null && Time.unscaledTime >= nextLook)
            {
                nextLook = Time.unscaledTime + 5;
                try { CruciblePlugin.Services.Capabilities.TryResolve("meadworks.wildblood.tierplate", new System.Version(1, 0), out provider); }
                catch { provider = null; }
            }
            var reading = Read();
            if (reading == null) { Hide(); return; }
            if (shown != null && shown.Equals(reading) && plate?.Rect != null && plate.Rect.gameObject.activeInHierarchy &&
                plate.Text != null && plate.Text.enabled && !plate.Text.richText && plate.Text.text == shownText) return;
            if (plate?.Rect == null)
                plate = Column.Add(gui, new PlateSpec(PlateId, 400, null, true, reading.Item2, reading.Item4));
            if (plate?.Rect == null || plate.Text == null) { Hide(); return; }
            plate.Icon.enabled = false;
            var text = plate.Text;
            text.richText = false;
            text.enableAutoSizing = true; text.fontSizeMin = 8; text.fontSizeMax = 14;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(4, 4); text.rectTransform.offsetMax = new Vector2(-4, -4);
            shownText = reading.Item2 + "\n" + reading.Item3;
            text.text = shownText;
            PlateTips.Set(gui, plate.Rect, reading.Item2, reading.Item4);
            var tip = plate.Rect.GetComponent<PlateTip>();
            if (tip != null)
            {
                if (literalTip == null && TipTemplate.For(gui) is GameObject source)
                {
                    tipHolder = new GameObject("Mantle_Wildblood_TipTemplate");
                    tipHolder.SetActive(false); tipHolder.transform.SetParent(gui.m_player, false);
                    literalTip = Object.Instantiate(source, tipHolder.transform, false);
                    foreach (var label in literalTip.GetComponentsInChildren<TMP_Text>(true)) label.richText = false;
                }
                tip.Prefab = literalTip;
            }
            plate.Rect.gameObject.SetActive(true);
            shown = reading;
            Column.Arrange(gui);
        }
        static void Hide()
        {
            shown = null; shownText = "";
            if (plate?.Rect != null) plate.Rect.gameObject.SetActive(false);
            if (plate?.Text != null) plate.Text.text = "";
            if (plate?.Rect != null && plate.Rect.GetComponent<PlateTip>() is PlateTip tip) { tip.Topic = ""; tip.Text = ""; }
        }
        public static void Reset()
        {
            Hide();
            if (plate?.Rect != null) Object.Destroy(plate.Rect.gameObject);
            if (tipHolder != null) Object.Destroy(tipHolder);
            plate = null; tipHolder = null; literalTip = null; provider = null; actor = null; network = null; gui = null; inventoryRoot = null;
            generation = -1; world = 0; session = null; nextLook = 0;
        }
    }
}
