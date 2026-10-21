// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original presentation on Crucible.UI; BetterSleepBruh HUD artwork and source are not adopted.
using System;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Integration;
using ExtraPotions.Ember.Sleep;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Ember.Runtime
{
    public static class EmberSleepHud
    {
        const float Height = 68;
        static KitHudPanel? panel;
        static Transform? owner;
        static TMP_Text? caption;
        static Image? moon, bed;
        static readonly Image[] pillows = new Image[SleepHud.MaxCompactAbove];
        static readonly Image[] chevrons = new Image[3];
        static readonly Vector3[] corners = new Vector3[4];
        static Player? actor;
        static long generation;
        static Vector2 lastDefault = new Vector2(-1, -1);
        static MeadworksLog? log;
        static bool warnedDisplay;

        public static void Initialize(MeadworksLog logger) => log = logger;

        public static void Tick()
        {
            if (!Plugin.AllowsUi) return;
            var settings = Plugin.Settings;
            var player = Player.m_localPlayer;
            var root = Hud.instance != null ? Hud.instance.m_rootObject?.transform as RectTransform : null;
            var reading = SleepCapability.Reading;
            if (actor != player || generation != CruciblePlugin.Session.Generation)
            {
                Reset(); actor = player; generation = CruciblePlugin.Session.Generation;
            }
            if (settings == null || player == null || root == null || !CrucibleUiPlugin.Active || KitCanvas.Font == null ||
                !EmberModules.Active(EmberModule.Sleep) || !settings.Get(EmberSettings.Hud.Show) ||
                !reading.WindowOpen || Hud.IsUserHidden() || GameWindowOpen() || CairnDisplaysSleep())
            { Hide(); return; }
            if (panel != null && (panel.Root == null || owner != root)) Reset();
            if (panel == null && !Build(root)) return;

            var compact = SleepHud.Compact(reading.Players, settings.Get(EmberSettings.Hud.CompactAbove));
            var count = compact ? 1 : Math.Min(SleepHud.MaxCompactAbove, reading.Players);
            var width = Mathf.Max(260, 116 + count * 20);
            panel!.Root.sizeDelta = new Vector2(width, Height);
            var share = reading.Players > 1 ? Math.Min(1.0, (double)reading.Sleeping / (reading.Players - 1)) : 0;
            // ExtraRate is the server's actual effective rate; no boost means the label must stay zero.
            var maximum = share > 0 && reading.ExtraRate > 0 ? reading.ExtraRate / share : 0;
            var fraction = SleepHud.BoostFraction(reading.ExtraRate, maximum);
            caption!.text = EmberText.Format("hud_sleeping", reading.Sleeping, reading.Players) + "   " + SleepHud.Label(100 * fraction);
            for (var i = 0; i < pillows.Length; i++)
            {
                pillows[i].gameObject.SetActive(i < count);
                if (i < count) pillows[i].color = KitAppearance.Color((compact ? reading.Sleeping > 0 : i < reading.Sleeping) ? KitColorRole.OnPrimary : KitColorRole.Muted);
            }
            var boosting = reading.ExtraRate > 0;
            bed!.gameObject.SetActive(!boosting);
            bed.rectTransform.anchoredPosition = new Vector2(width - 42, -35);
            var step = SleepHud.ChevronStep(Time.unscaledTime, SleepHud.ChevronStepSeconds(fraction));
            var lit = SleepHud.ChevronLit(step);
            for (var i = 0; i < chevrons.Length; i++)
            {
                chevrons[i].gameObject.SetActive(boosting);
                chevrons[i].rectTransform.anchoredPosition = new Vector2(width - 66 + i * 16, -37);
                chevrons[i].color = KitAppearance.Color(lit[i] ? KitColorRole.OnPrimary : KitColorRole.Muted);
            }
            var at = DefaultPosition(root, width);
            if (at != lastDefault) panel.SetPosition(at);
            lastDefault = at;
            panel.Update(true, ZCursor.LockState != CursorLockMode.Locked && ZCursor.IsVisible, 1, 1);
        }

        // The board hides under the large map and every game window, the same set as Cairn's sleep row.
        // InventoryGui covers crafting, containers, texts, trophies and skills; KitWindows covers every MeadWorks window.
        static bool GameWindowOpen() => KitWindows.HudBlocked || Minimap.IsOpen() || InventoryGui.IsVisible() || Menu.IsVisible()
            || StoreGui.IsVisible() || TextInput.IsVisible() || (TextViewer.instance != null && TextViewer.instance.IsVisible());

        static bool CairnDisplaysSleep()
        {
            try
            {
                Func<bool>? display = null;
                return CruciblePlugin.Services.Capabilities.TryResolve("meadworks.cairn.sleep_display", new System.Version(1, 0), out display) && display != null && display();
            }
            catch (Exception error)
            {
                if (!warnedDisplay) log?.Warning("Cairn sleep display unavailable; Ember keeps its HUD: " + error.GetType().Name);
                warnedDisplay = true;
                return false;
            }
        }

        static bool Build(RectTransform root)
        {
            panel = KitHudPanel.Create(root, "Ember_SleepHud", new Vector2(260, Height), Vector2.zero, global::ExtraPotions.Ember.Runtime.Plugin.Guid, "Ember_SleepHud");
            if (panel == null) return false;
            owner = root;
            moon = Glyph("Moon", KitGlyph.Moon, 10, 8, 24);
            caption = KitCanvas.Text(panel.Content, "Ember_SleepCaption", "", 16, KitAppearance.Color(KitColorRole.OnPrimary), TextAlignmentOptions.Center, false);
            var rect = caption.rectTransform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(40, -32); rect.offsetMax = new Vector2(-8, -6);
            for (var i = 0; i < pillows.Length; i++) pillows[i] = Glyph("Pillow_" + i, KitGlyph.Pillow, 12 + i * 20, 37, 18);
            bed = Glyph("Bed", KitGlyph.Bed, 218, 35, 24);
            for (var i = 0; i < chevrons.Length; i++) chevrons[i] = Glyph("Chevron_" + i, KitGlyph.Chevron, 194 + i * 16, 37, 18);
            lastDefault = new Vector2(-1, -1);
            return true;
        }

        static Image Glyph(string name, KitGlyph glyph, float x, float y, float side)
        {
            var go = new GameObject("Ember_Sleep" + name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel!.Content, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(side, side);
            var image = go.GetComponent<Image>();
            image.sprite = KitGlyphSprites.Of(glyph); image.preserveAspect = true; image.raycastTarget = false;
            image.color = KitAppearance.Color(KitColorRole.OnPrimary);
            return image;
        }

        static Vector2 DefaultPosition(RectTransform root, float width)
        {
            var at = new Vector2(Mathf.Max(0, root.rect.width - width - 10), 220);
            var map = Minimap.instance != null ? Minimap.instance.m_smallRoot?.transform as RectTransform : null;
            if (map != null && map.gameObject.activeInHierarchy)
            {
                map.GetWorldCorners(corners);
                var bottomRight = root.InverseTransformPoint(corners[3]);
                at = new Vector2(bottomRight.x - root.rect.xMin - width, root.rect.yMax - bottomRight.y + 8);
            }
            // Other products' HUD rectangles (meadworks.hud.occupied, Cairn's among them); runs only while the sleep window is open.
            var lowest = KitHudOccupancy.Lowest(CruciblePlugin.Services.Capabilities, Plugin.Guid, at.x, at.x + width);
            if (lowest >= 0 && at.y < lowest + 8) at.y = lowest + 8;
            return at;
        }

        static void Hide() => panel?.Update(false, false, 1, 1);
        static void Reset()
        {
            panel?.Dispose(); panel = null; owner = null; caption = null; moon = bed = null;
            lastDefault = new Vector2(-1, -1);
            Array.Clear(pillows, 0, pillows.Length); Array.Clear(chevrons, 0, chevrons.Length);
        }
        public static void Shutdown() { Reset(); actor = null; log = null; warnedDisplay = false; }
    }
}
