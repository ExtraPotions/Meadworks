// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. A kit meter for warm-clothes heat. It reads the overheat facts and does not change them.
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Solstice.Config;
using ExtraPotions.Solstice.Localization;
using ExtraPotions.Solstice.Visuals;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Solstice.Runtime
{
    public static class SeasonHeatHud
    {
        const float LayoutSeconds = 0.5f;

        static KitMeter? meter;
        static HotkeyBar? hotbar;
        static float fill, lastTime, nextLayout;
        static int shownMilli = -1;
        static int shownSize = -1;
        static string shownLabel = "";
        static float shownOccupied = -2f;
        static HudBox bounds;

        public static HudBox Bounds => bounds;

        public static void Tick()
        {
            if (meter != null && meter.Root == null)
            {
                meter.Dispose();
                meter = null;
                shownLabel = "";
                shownMilli = -1;
                shownSize = -1;
            }
            if (!Armed())
            {
                fill = 0f;
                lastTime = MeadworksHeadless.AllowsUi ? Time.unscaledTime : 0f;
                Hide();
                return;
            }
            float now = Time.unscaledTime;
            float step = now - lastTime;
            lastTime = now;
            if (step < 0f || step > 1f) step = 0f;
            fill = HeatMeter.Advance(fill, SeasonStatus.ShownHeat, step);
            if (!Shown() || (fill <= 0.004f && SeasonStatus.ShownHeat <= 0f))
            {
                Hide();
                return;
            }
            if (now >= nextLayout || meter == null || Mathf.Abs(Band() - shownOccupied) > 0.5f) Layout();
            Paint();
        }

        public static void Shutdown()
        {
            if (meter != null) meter.Dispose();
            meter = null;
            hotbar = null;
            fill = 0f;
            lastTime = 0f;
            nextLayout = 0f;
            shownMilli = -1;
            shownSize = -1;
            shownLabel = "";
            shownOccupied = -2f;
            bounds = default;
        }

        static bool Armed()
        {
            if (!MeadworksHeadless.AllowsUi) return false;
            var settings = Plugin.Settings;
            return Plugin.Ready && settings != null && settings.Get(SolsticeSettings.Visuals.HeatMeter) && Player.m_localPlayer != null;
        }

        static bool Shown()
        {
            if (!CrucibleUiPlugin.Active || KitWindows.HudBlocked) return false;
            return !global::Hud.IsUserHidden() && !Minimap.IsOpen() && !InventoryGui.IsVisible() && !Menu.IsVisible();
        }

        static void Layout()
        {
            nextLayout = Time.unscaledTime + LayoutSeconds;
            var root = HudRoot();
            if (root == null || !CrucibleUiPlugin.Active)
            {
                Hide();
                return;
            }
            if (meter != null && meter.Root == null)
            {
                meter.Dispose();
                meter = null;
                shownLabel = "";
                shownMilli = -1;
                shownSize = -1;
            }
            if (meter == null)
            {
                meter = KitMeter.Create(root, "Solstice_Heat", Vector2.zero, new Vector2(HeatMeterLayout.Width, HeatMeterLayout.Height));
                if (meter == null || meter.Root == null) return;
                Contrast();
            }
            else if (meter.Root.parent != root)
                meter.Root.SetParent(root, false);
            float width = root.rect.width;
            float height = root.rect.height;
            if (width < 1f) return;
            float occupied = Band();
            shownOccupied = occupied;
            var place = HeatMeterLayout.Place(width, height, SeasonIndicator.Bounds, occupied, SeasonIndicator.StatusRow(root), Notice(root), Hotbar(root));
            if (!place.Shown)
            {
                Hide();
                return;
            }
            bounds = place;
            var position = new Vector2(place.Left, -place.Top);
            if (meter.Root.anchoredPosition != position) meter.Root.anchoredPosition = position;
            var size = new Vector2(place.Width, place.Height);
            if (meter.Root.sizeDelta != size) meter.Root.sizeDelta = size;
            if (!meter.Root.gameObject.activeSelf) meter.Root.gameObject.SetActive(true);
            SeasonIndicator.PublishObstacle();
        }

        static float Band()
        {
            var root = HudRoot();
            if (root == null || CruciblePlugin.Services == null) return -1f;
            float left = SeasonHudLayout.Left(root.rect.width, HeatMeterLayout.Width);
            return KitHudOccupancy.Lowest(CruciblePlugin.Services.Capabilities, Plugin.Guid, left, left + HeatMeterLayout.Width);
        }

        static void Contrast()
        {
            if (meter == null || meter.Root == null) return;
            var track = meter.Root.GetComponent<Image>();
            if (track != null) track.color = new Color(0.09f, 0.08f, 0.07f, 1f);
            var label = meter.Label;
            if (label == null) return;
            label.outlineWidth = 0.28f;
            label.outlineColor = new Color32(38, 37, 34, 255);
            label.extraPadding = true;
            var outline = label.GetComponent<Outline>();
            if (outline == null) outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.09f, 0.07f, 0.05f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
            // Cream on the full orange bar is about 1.2:1. The words sit in the middle, so the face goes dark once the fill passes them.
            label.color = meter.Fraction >= 0.55f
                ? new Color(0.15f, 0.12f, 0.08f, 1f)
                : new Color(0.96f, 0.89f, 0.74f, 1f);
        }

        static void Paint()
        {
            if (meter == null) return;
            int milli = (int)(fill * 1000f);
            float size;
            string label = HeatWord(out size);
            int sizeMilli = (int)Math.Round(size * 100.0);
            if (milli == shownMilli && sizeMilli == shownSize && label == shownLabel) return;
            shownMilli = milli;
            shownSize = sizeMilli;
            shownLabel = label;
            meter.Set(fill, 1f, label);
            Contrast();
        }

        static string HeatWord(out float size)
        {
            float scale = 1f;
            var text = meter != null ? meter.Label : null;
            if (text != null && text.canvas != null && text.canvas.scaleFactor >= 0.05f)
                scale = text.canvas.scaleFactor;
            float hud = 0f;
            var root = HudRoot();
            if (root != null) hud = root.rect.width;
            size = HeatMeter.LabelFontSize(HeatMeterLayout.Height, scale, Screen.width, hud);
            if (text != null && size > 0f && Math.Abs(text.fontSize - size) > 0.05f)
                text.fontSize = size;
            if (size <= 0f) return "";
            return SolsticeText.Text(SolsticeTextKeys.HudHeat);
        }

        static void Hide()
        {
            bounds = default;
            shownMilli = -1;
            shownSize = -1;
            if (meter != null && meter.Root != null && meter.Root.gameObject.activeSelf)
                meter.Root.gameObject.SetActive(false);
            SeasonIndicator.PublishObstacle();
        }

        static RectTransform? HudRoot()
        {
            var hud = global::Hud.instance;
            var root = hud != null ? hud.m_rootObject : null;
            return root != null ? root.transform as RectTransform : null;
        }

        static HudBox Notice(RectTransform hudRoot)
        {
            var front = KitCanvas.Front as RectTransform;
            var stack = front != null ? front.Find("Crucible_NoticeStack") as RectTransform : null;
            if (stack == null || !stack.gameObject.activeInHierarchy) return default;
            var reading = KitHudOccupancy.Measure(hudRoot, stack);
            return reading.Item1 ? new HudBox(reading.Item2, reading.Item3, reading.Item4, reading.Item5) : default;
        }

        static HudBox Hotbar(RectTransform hudRoot)
        {
            var hud = global::Hud.instance;
            if (hud != null && hotbar == null) hotbar = hud.GetComponentInChildren<HotkeyBar>(true);
            if (hotbar == null) return default;
            var bar = hotbar.transform;
            HudBox box = default;
            bool any = false;
            for (int i = 0; i < bar.childCount; i++)
            {
                if (!(bar.GetChild(i) is RectTransform slot) || !slot.gameObject.activeInHierarchy) continue;
                var reading = KitHudOccupancy.Measure(hudRoot, slot);
                if (!reading.Item1) continue;
                var next = new HudBox(reading.Item2, reading.Item3, reading.Item4, reading.Item5);
                box = any ? HeatMeterLayout.Union(box, next) : next;
                any = true;
            }
            return box;
        }
    }
}
