// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. A season plate under other HUD rectangles. It is not a control.
using System;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Config;
using ExtraPotions.Solstice.Integration;
using ExtraPotions.Solstice.Localization;
using ExtraPotions.Solstice.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// The current season and day, in a rounded plate. Placement uses <c>meadworks.hud.occupied</c> and the status-effect
    /// icons, and Solstice publishes its own rectangle on that contract. The plate is hidden with the game HUD.
    /// </summary>
    public static class SeasonIndicator
    {
        const float LayoutSeconds = 0.5f;

        static readonly Tuple<bool, float, float, float, float> Hidden = Tuple.Create(false, 0f, 0f, 0f, 0f);

        static RectTransform plate;
        static Image plateIcon;
        static TMP_Text plateText;
        static int shownSeason = -1;
        static int shownDay = -1;
        static string shownText = "";
        static bool shownIcons;
        static bool shownIconsKnown;
        static float shownOccupied = -2f;
        static float nextLayout;
        static HudBox bounds;
        static KitHudPanel? block;

        public static HudBox Bounds => bounds;

        public static bool Showing => plate != null && plate.gameObject.activeInHierarchy;

        /// <summary><c>meadworks.hud.occupied</c> for this plate. Hidden while the plate is off.</summary>
        public static Tuple<bool, float, float, float, float> Occupied()
        {
            var root = Root();
            HudBox plateBox = default;
            if (root != null && plate != null)
            {
                var reading = KitHudOccupancy.Measure(root, plate);
                if (reading.Item1) plateBox = new HudBox(reading.Item2, reading.Item3, reading.Item4, reading.Item5);
            }
            var box = HeatMeterLayout.Union(plateBox, SeasonHeatHud.Bounds);
            return box.Shown ? Tuple.Create(true, box.Left, box.Top, box.Width, box.Height) : Hidden;
        }

        public static void Tick()
        {
            if (!Wanted(out var view))
            {
                Hide();
                return;
            }
            float now = Time.unscaledTime;
            bool icons = IconsOn();
            float occupied = OccupiedBottom();
            bool textDue = view.Season != shownSeason || view.DayInSeason != shownDay;
            bool shift = !shownIconsKnown || icons != shownIcons || Math.Abs(occupied - shownOccupied) > 0.5f;
            if (!textDue && !shift && plate != null && plate.gameObject.activeSelf && now < nextLayout) return;
            nextLayout = now + LayoutSeconds;
            Layout(view, icons, occupied);
        }

        public static void Shutdown()
        {
            ForgetPlate();
            shownOccupied = -2f;
            nextLayout = 0f;
            bounds = default;
            if (block != null) block.Dispose();
            block = null;
        }

        static void ForgetPlate()
        {
            if (plate != null) UnityEngine.Object.Destroy(plate.gameObject);
            plate = null;
            plateIcon = null;
            plateText = null;
            shownSeason = -1;
            shownDay = -1;
            shownText = "";
            shownIconsKnown = false;
        }

        static bool Wanted(out SeasonView view)
        {
            view = SolsticeSeasons.View;
            var settings = Plugin.Settings;
            if (!Plugin.Ready || settings == null || !view.Available || !MeadworksHeadless.AllowsUi || Player.m_localPlayer == null)
                return false;
            if (view.Season < 0 || view.Season >= Seasons.Count) return false;
            if (!settings.Get(SolsticeSettings.Visuals.SeasonIndicator)) return false;
            if (!CrucibleUiPlugin.Active || KitWindows.HudBlocked) return false;
            if (global::Hud.IsUserHidden() || Minimap.IsOpen() || InventoryGui.IsVisible() || Menu.IsVisible()) return false;
            return true;
        }

        static bool IconsOn()
        {
            var settings = Plugin.Settings;
            return settings != null && settings.Get(SolsticeSettings.Visuals.SeasonIcons);
        }

        static float OccupiedBottom()
        {
            var root = Root();
            if (root == null || CruciblePlugin.Services == null) return -1f;
            float left = SeasonHudLayout.Left(root.rect.width, SeasonHudLayout.ChipWidth);
            return KitHudOccupancy.Lowest(CruciblePlugin.Services.Capabilities, Plugin.Guid, left, left + SeasonHudLayout.ChipWidth);
        }

        static void Layout(SeasonView view, bool icons, float occupied)
        {
            var root = Root();
            if (root == null || !CrucibleUiPlugin.Active)
            {
                Hide();
                return;
            }
            bool fresh = false;
            if (plate == null || plateText == null)
            {
                ForgetPlate();
                if (KitCanvas.Font == null) return;
                plate = Create(root);
                fresh = true;
            }
            else if (plate.parent != root)
                plate.SetParent(root, false);
            float width = root.rect.width;
            if (width < 1f) return;
            var place = SeasonHudLayout.Place(width, SeasonHudLayout.ChipWidth, SeasonHudLayout.ChipHeight, occupied, StatusRow(root));
            plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0f, 1f);
            plate.sizeDelta = new Vector2(place.Width, place.Height);
            plate.anchoredPosition = new Vector2(place.Left, -place.Top);
            plate.gameObject.SetActive(true);
            bounds = place;
            shownIcons = icons;
            shownIconsKnown = true;
            shownOccupied = occupied;
            Write(view, fresh);
            PublishObstacle();
        }

        static void Write(SeasonView view, bool fresh)
        {
            if (plateText == null) return;
            var season = (Season)view.Season;
            string next = SolsticeText.Format(SolsticeTextKeys.HudSeason, SolsticeText.Text(SolsticeTextKeys.SeasonName(season)), view.DayInSeason);
            var mark = SeasonMarks.For(season);
            bool icon = mark != null && plateIcon != null;
            if (plateIcon != null)
            {
                plateIcon.sprite = mark;
                plateIcon.enabled = icon;
            }
            var label = (RectTransform)plateText.transform;
            float inset = icon ? 24f : 8f;
            if (label.offsetMin.x != inset) label.offsetMin = new Vector2(inset, 1f);
            if (!SeasonRebuild.Text(!fresh, next, shownText, view.Season, shownSeason, view.DayInSeason, shownDay)) return;
            shownSeason = view.Season;
            shownDay = view.DayInSeason;
            shownText = next;
            plateText.richText = false;
            plateText.text = next;
        }

        static RectTransform Create(RectTransform parent)
        {
            var go = new GameObject("Solstice_Season", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            var fill = go.GetComponent<Image>();
            fill.sprite = RoundedFill.Sprite;
            fill.type = Image.Type.Sliced;
            var ink = KitPalette.SheetInset;
            fill.color = new Color(ink.R, ink.G, ink.B, 0.92f);
            fill.raycastTarget = false;
            var iconObject = new GameObject("Mark", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(4f, 0f);
            iconRect.sizeDelta = new Vector2(18f, 18f);
            plateIcon = iconObject.GetComponent<Image>();
            plateIcon.raycastTarget = false;
            plateIcon.enabled = false;
            var label = KitCanvas.Text(rect, "Label", "", 15f, KitUnity.Colour(KitPalette.SheetLabel), TextAlignmentOptions.Center, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 1f);
            labelRect.offsetMax = new Vector2(-8f, -1f);
            label.richText = false;
            KitCanvas.Fit(label);
            plateText = label;
            return rect;
        }

        static RectTransform Root()
        {
            var hud = global::Hud.instance;
            var root = hud != null ? hud.m_rootObject : null;
            return root != null ? root.transform as RectTransform : null;
        }

        internal static HudBox StatusRow(RectTransform root)
        {
            var hud = global::Hud.instance;
            var list = hud != null ? hud.m_statusEffectListRoot : null;
            if (root == null || list == null || !list.gameObject.activeInHierarchy) return default;
            bool any = false;
            float left = 0f, top = 0f, right = 0f, bottom = 0f;
            for (int i = 0; i < list.childCount; i++)
            {
                if (!(list.GetChild(i) is RectTransform child) || !child.gameObject.activeInHierarchy) continue;
                var reading = KitHudOccupancy.Measure(root, child);
                if (!reading.Item1) continue;
                float l = reading.Item2, t = reading.Item3, r = l + reading.Item4, b = t + reading.Item5;
                if (!any) { left = l; top = t; right = r; bottom = b; any = true; }
                else
                {
                    if (l < left) left = l;
                    if (t < top) top = t;
                    if (r > right) right = r;
                    if (b > bottom) bottom = b;
                }
            }
            if (any) return new HudBox(left, top, right - left, bottom - top);
            var row = KitHudOccupancy.Measure(root, list);
            if (!row.Item1 || row.Item4 >= root.rect.width * 0.5f) return default;
            return new HudBox(row.Item2, row.Item3, row.Item4, row.Item5);
        }

        internal static void PublishObstacle()
        {
            if (!MeadworksHeadless.AllowsUi) return;
            var root = Root();
            var box = HeatMeterLayout.Union(bounds, SeasonHeatHud.Bounds);
            if (block != null && block.Root == null)
            {
                block.Dispose();
                block = null;
            }
            if (root == null || !box.Shown || !CrucibleUiPlugin.Active)
            {
                if (block != null) block.Update(false, false, 1f, 0f);
                return;
            }
            if (block == null)
            {
                block = KitHudPanel.Create(root, "Solstice_HudBlock", new Vector2(box.Width, box.Height), new Vector2(box.Left, box.Top), Plugin.Guid, "Solstice_SeasonPlate");
                if (block == null || block.Root == null) return;
            }
            else if (block.Root.parent != root)
                block.Root.SetParent(root, false);
            block.Root.sizeDelta = new Vector2(Math.Max(8f, box.Width), Math.Max(8f, box.Height));
            block.SetPosition(new Vector2(box.Left, box.Top));
            block.Update(true, false, 1f, 0f);
            var group = block.Root.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 0f;
            var background = KitPanel.Background(block.Root);
            if (background != null)
            {
                background.enabled = false;
                background.raycastTarget = false;
            }
        }

        static void Hide()
        {
            bounds = default;
            shownIconsKnown = false;
            shownOccupied = -2f;
            if (plate != null && plate.gameObject.activeSelf) plate.gameObject.SetActive(false);
            PublishObstacle();
        }
    }
}
