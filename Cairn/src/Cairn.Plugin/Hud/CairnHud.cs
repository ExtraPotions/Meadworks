// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room from TimeWeatherHud's documented behavior; no source or artwork used.
// Presentation and drag ownership use the existing Crucible.UI KitHudPanel and KitCanvas baseline; the plates are MeadWorks'
// own art (Crucible.UI KitHudArt: the iron-bound compass plank and its heading inset) and the icons are drawn from shapes in code (HudIcons),
// except a Tempest event's mark, which is the Crucible.UI kit glyph Tempest's own badge shows (KitGlyphSprites).
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Cairn.Integration;
using ExtraPotions.Cairn.Localization;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameHud = global::Hud;
using Compass = ExtraPotions.Cairn.Hud.Compass;

namespace ExtraPotions.Cairn.Runtime
{
    public static class CairnHud
    {
        const int IconSize = HudLayout.IconSize;
        static readonly Color Cream = new Color(1f, .9f, .68f, 1f);
        static readonly Color Gold = new Color(.86f, .7f, .4f, 1f);
        static readonly Color Bronze = new Color(.72f, .5f, .26f, .9f);

        static KitHudPanel? panel;
        static Transform? owner;
        // The heading and every extra line; the status line has its own parts below.
        static readonly Dictionary<HudRow, TMP_Text> labels = new Dictionary<HudRow, TMP_Text>();
        static readonly Dictionary<HudRow, Image?> plates = new Dictionary<HudRow, Image?>();
        static readonly Image?[] capRivets = new Image?[2];
        const int CapRivetMinHeight = 34;
        static TMP_Text? clockText, weatherText;
        static Image? clockIcon, weatherIcon, eventIcon, divider, pointer;
        static readonly List<TMP_Text> ticks = new List<TMP_Text>();
        static readonly List<Image> marks = new List<Image>();
        static readonly List<(Image Icon, TMP_Text Distance)> markers = new List<(Image, TMP_Text)>();
        static readonly Dictionary<HudIcon, Sprite> icons = new Dictionary<HudIcon, Sprite>();
        static bool toggledOff, ownsSleep, showingSleep;
        static (int ParentWidth, int ParentHeight, int Width, int Height)? placement;
        static float nextMarkers;
        static Player? character;
        static long world;
        static List<Minimap.PinData> selected = new List<Minimap.PinData>();
        // Each part is drawn again only when what it shows changes (a per-frame redraw cost 0.61 ms
        // and 4 KB). A key holds every input of its part; a part whose key matches the last drawn one is left as it is.
        static HudOptions drawnOptions;
        static IReadOnlyList<HudRow>? drawnRows;
        static HudSize drawnSize;
        static int drawnCompassWidth;
        static readonly List<HudRow> extraRows = new List<HudRow>();
        static (IReadOnlyList<HudRow>? Rows, int Width, int Day, int Hour, int Minute, bool Use24, bool Night, WeatherCategory Category, string? Language, long Revision, string? Event) statusDrawn;
        static (IReadOnlyList<HudRow>? Rows, int Width, bool Plate) stripDrawn;
        static (IReadOnlyList<HudRow>? Rows, int Width, int Degrees, string? Point, string? Language, long Revision, bool Plate) headingDrawn;
        static (IReadOnlyList<HudRow>? Rows, int Width, int Plates) extrasDrawn;
        static (bool Has, float Radius, float Base, float Multiplier, float Light, float Weather, float Altitude, float Location, string? Language, long Revision)? radiusDrawn, inputsDrawn;
        static (TempestLineKind Kind, string? TextKey, string? EventKey, string? Countdown, string? Language, long Revision, IReadOnlyList<HudRow>? Rows, int Width)? tempestDrawn;
        static (bool Visible, string? Sleeping, string? Players, string? Skip, string? Boost, string? Language, long Revision)? sleepDrawn;
        static (bool Show, IReadOnlyList<HudRow>? Rows, int Width, int Degrees, float Heading, string? Language, long Revision, bool Markers, List<Minimap.PinData>? Selected, float X, float Z, int Max, float MaxDistance) compassDrawn;
        static readonly List<(float X, float Z, Sprite? Icon, bool IconAlive, Minimap.PinType Type)> drawnPins = new List<(float, float, Sprite?, bool, Minimap.PinType)>();
        static readonly List<float> pinOffsets = new List<float>();
        static readonly Dictionary<string, PadButton[]?> chords = new Dictionary<string, PadButton[]?>(StringComparer.Ordinal);
        static string? toggleKeyText;
        static KeyboardShortcut toggleKey = KeyboardShortcut.Empty;
        static bool toggleKeyValid;

        /// <summary>The optional Tempest consumer reads actual presentation, never just the event's presence.</summary>
        static readonly Tuple<bool, float, float, float, float> NotShown = Tuple.Create(false, 0f, 0f, 0f, 0f);
        /// <summary>meadworks.hud.occupied: the compass HUD's rectangle in its parent's (the HUD root's) units, or not shown.</summary>
        public static Tuple<bool, float, float, float, float> Occupied() =>
            panel?.Root != null && owner is RectTransform root ? KitHudOccupancy.Measure(root, panel.Root) : NotShown;
        public static bool ShowingWeather() => TempestNotice.HudRowDrawn && panel?.Root != null && panel.Root.gameObject.activeInHierarchy;
        /// <summary>
        /// Ember hides its own sleep board while this is true. Cairn owns the display whenever its HUD runs and Ember reports a
        /// reading, so Ember's board does not pop up in the moments the line is empty (nobody in bed), nor while a window, the
        /// inventory or a menu only blocks the HUD (HudLayout.OwnsSleep); the toggle key or stepping aside hands it back.
        /// </summary>
        // Ember steps its own sleep HUD aside only while this row is on screen, or hidden on purpose under a game window.
        public static bool ShowingSleep() => showingSleep;

        public static void Tick()
        {
            TempestNotice.HudRowDrawn = false;
            ownsSleep = showingSleep = false;
            var settings = Plugin.Settings;
            var player = Player.m_localPlayer;
            var root = GameHud.instance != null ? GameHud.instance.m_rootObject?.transform : null;
            if (!Plugin.Ready || !Plugin.AllowsUi || settings == null || player == null || root == null || !CrucibleUiPlugin.Active || KitCanvas.Font == null)
            {
                Hide();
                return;
            }
            var currentWorld = ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0;
            if (character != player || world != currentWorld)
            {
                character = player;
                world = currentWorld;
                toggledOff = false;
                nextMarkers = 0;
                selected.Clear();
            }
            var aside = CairnModules.StepAside.SteppedAside(CairnModule.Hud);
            if (!aside && MeadworksInput.Allows(InputContext.Gameplay) && TogglePressed(settings)) toggledOff = !toggledOff;
            ownsSleep = HudLayout.OwnsSleep(settings.Get(CairnSettings.Hud.Enabled), Plugin.AllowsUi, aside, toggledOff, CairnSleep.Reading);
            // Both hide while any MeadWorks window is open (user decision 2026-10-12 19:15), as the game's HUD does for the Compendium.
            var windowOpen = KitWindows.HudBlocked;
            // The sleep row also hides under the large map and every game window, the Esc menu included (user decision 2026-10-12).
            // InventoryGui covers crafting, containers, texts, trophies and skills.
            var sleepBlocked = windowOpen || Minimap.IsOpen() || InventoryGui.IsVisible() || Menu.IsVisible() || StoreGui.IsVisible() || TextInput.IsVisible() || CairnSettingsWindow.IsOpen;
            // A hidden row is not showing: Ember then shows the boost exactly as it does without Cairn.
            if (ownsSleep && !sleepBlocked) showingSleep = CairnMinimapEffects.Draw(root); else { CairnMinimapEffects.Hide(); showingSleep = false; }
            var blocked = windowOpen || GameHud.IsUserHidden() || Minimap.IsOpen() || InventoryGui.IsVisible() || Menu.IsVisible() || CairnSettingsWindow.IsOpen;
            if (!HudLayout.Visible(settings.Get(CairnSettings.Hud.Enabled), Plugin.AllowsUi, aside, toggledOff, blocked))
            {
                HideHud();
                return;
            }
            if (panel != null && (panel.Root == null || owner != root)) DisposePanel();
            if (panel == null) Build(root);
            if (panel == null) return;

            var options = new HudOptions(settings.Get(CairnSettings.Hud.ShowDay), settings.Get(CairnSettings.Hud.ShowTime),
                settings.Get(CairnSettings.Hud.ShowWeather), settings.Get(CairnSettings.Hud.ShowCompass), settings.Get(CairnSettings.Hud.ShowHeading),
                // Show Radius Inputs stays a file-only diagnostic setting and is no longer drawn in game.
                settings.Get(CairnSettings.Radius.ShowRadius), false, CairnTempest.Line.Kind);
            var compassWidth = settings.Get(CairnSettings.Hud.CompassWidth);
            if (drawnRows == null || !SameOptions(options, drawnOptions) || compassWidth != drawnCompassWidth)
            {
                drawnOptions = options;
                drawnCompassWidth = compassWidth;
                drawnRows = HudLayout.Rows(options);
                // A new width changes panelWidth, which every Draw* cache key and the placement below include: all parts move at once.
                drawnSize = HudLayout.Size(drawnRows, compassWidth);
            }
            var rows = drawnRows;
            if (rows.Count == 0) { HideHud(); return; }
            var size = drawnSize;
            // Debug inputs are one optional row; fit the text without expanding the ordinary compact HUD.
            var panelWidth = options.ShowRadiusInputs ? Math.Max(size.Width, 640) : size.Width;
            panel.Root.sizeDelta = new Vector2(panelWidth, size.Height);
            var parent = root as RectTransform;
            var parentWidth = Mathf.RoundToInt(parent != null ? parent.rect.width : Screen.width);
            var parentHeight = Mathf.RoundToInt(parent != null ? parent.rect.height : Screen.height);
            var position = HudLayout.Home(parentWidth, Mathf.CeilToInt(panelWidth * HudLayout.Scale));
            var requested = (parentWidth, parentHeight, panelWidth, size.Height);
            if (!placement.HasValue || placement.Value != requested)
            {
                panel.SetPosition(new Vector2(position.X, position.Y));
                placement = requested;
            }

            var env = EnvMan.instance;
            var player3 = player.transform;
            var forward = GameCamera.instance != null ? GameCamera.instance.transform.forward : player3.forward;
            var heading = Compass.Heading(forward.x, forward.z);
            var language = CairnText.Language;
            var revision = CairnText.Revision;
            DrawStatus(rows, options, panelWidth, env, player, settings, language, revision);
            DrawStrip(rows, options, heading, panelWidth);
            DrawHeading(rows, heading, panelWidth, language, revision);
            DrawExtras(rows, panelWidth, language, revision);
            DrawCompass(options.ShowCompass, rows, heading, panelWidth - 2 * HudLayout.StripCap, panelWidth, player, settings, language, revision);
            // The kit's own board is hidden (background opacity 0): the plank, its heading inset and the extra lines are the HUD.
            panel.Update(true, ZCursor.LockState != CursorLockMode.Locked && ZCursor.IsVisible, HudLayout.Scale, 0f);
            TempestNotice.HudRowDrawn = WeatherDisplayCapability.Showing(panel.Root.gameObject.activeInHierarchy,
                settings.Get(CairnSettings.Hud.TempestWarnings), CairnTempest.Line.Kind);
        }

        static bool SameOptions(HudOptions a, HudOptions b) => a.ShowDay == b.ShowDay && a.ShowTime == b.ShowTime && a.ShowWeather == b.ShowWeather
            && a.ShowCompass == b.ShowCompass && a.ShowHeading == b.ShowHeading && a.ShowRadius == b.ShowRadius && a.ShowRadiusInputs == b.ShowRadiusInputs
            && a.Tempest == b.Tempest && a.Sleep == b.Sleep;

        /// <summary>The row's plate exists: until it does, the part is drawn every frame, as before, so the plate appears once its art reads.</summary>
        static bool PlateReady(HudRow row) => plates.TryGetValue(row, out var image) && image != null;

        static void Redraw()
        {
            statusDrawn = default; stripDrawn = default; headingDrawn = default; extrasDrawn = default; compassDrawn = default;
            radiusDrawn = inputsDrawn = null; tempestDrawn = null; sleepDrawn = null;
            drawnPins.Clear();
        }

        static void DrawStatus(IReadOnlyList<HudRow> rows, HudOptions options, int panelWidth, EnvMan? env, Player player, CairnConfig settings, string language, long revision)
        {
            var day = env != null ? env.GetCurrentDay() : 0;
            var fraction = env != null ? env.GetDayFraction() : 0;
            var (hour, minute) = ExtraPotions.Cairn.Hud.Clock.Parts(fraction);
            var use24 = settings.Get(CairnSettings.Hud.Use24Hour);
            var night = EnvMan.IsNight();
            var environment = env != null ? env.GetCurrentEnvironment() : null;
            var fog = environment == null ? 0 : night ? environment.m_fogDensityNight : environment.m_fogDensityDay;
            var category = WeatherCategories.Of(environment?.m_name, fog, environment?.m_isWet ?? false, player.InInterior());
            // During a Tempest event here, the weather name and mark are the event's (as on the Tempest row and badge).
            var tempestEvent = TempestWarning.StatusEvent(category, environment?.m_name, CairnTempest.Line);
            var activeEvent = tempestEvent?.Key;
            var key = (rows, panelWidth, day, hour, minute, use24, night, category, language, revision, activeEvent);
            if (key == statusDrawn) return;
            statusDrawn = key;
            var on = rows.Contains(HudRow.Status);
            clockText!.gameObject.SetActive(on); weatherText!.gameObject.SetActive(on);
            clockIcon!.gameObject.SetActive(false); weatherIcon!.gameObject.SetActive(false); divider!.gameObject.SetActive(false);
            if (!on) return;
            var clock = HudText.Clock(options.ShowDay, options.ShowTime, day, fraction, use24);
            var clockString = HudText.Dot(clock.Day.Length == 0 ? "" : CairnText.Format(CairnTextKeys.HudDay, clock.Day), clock.Time);
            var weatherString = options.ShowWeather ? CairnText.Text(activeEvent ?? CairnTextKeys.Weather(category)) : "";
            var hasClock = clockString.Length > 0;
            var hasWeather = weatherString.Length > 0;
            var gap = HudLayout.StatusGap;
            var top = HudLayout.Top(rows, HudRow.Status);
            var iconY = top + (HudLayout.StatusHeight - IconSize) * .5f;
            clockText.text = clockString;
            weatherText.text = weatherString;
            var showClockIcon = hasClock && options.ShowTime;
            // The clock keeps its full text; the weather name takes what is left and best-fits into it (a smaller font, then an ellipsis).
            var clockWidth = hasClock ? Mathf.Min(clockText.GetPreferredValues(clockString).x, HudLayout.StatusRoom(panelWidth) - (showClockIcon ? IconSize + 3f : 0f)) : 0f;
            var clockGroup = (showClockIcon ? IconSize + 3f : 0f) + clockWidth;
            var weatherWidth = 0f;
            if (hasWeather)
            {
                var weatherRoom = HudLayout.WeatherTextRoom(panelWidth, hasClock ? clockGroup : 0f);
                weatherText.fontSize = HudLayout.StatusFontSize;
                var (size, ellipsis) = HudLayout.FitText(weatherText.GetPreferredValues(weatherString).x, weatherRoom);
                weatherText.fontSize = size;
                weatherText.overflowMode = ellipsis ? TextOverflowModes.Ellipsis : TextOverflowModes.Truncate;
                weatherWidth = Mathf.Min(weatherText.GetPreferredValues(weatherString).x, weatherRoom);
            }
            var weatherGroup = hasWeather ? IconSize + 3f + weatherWidth : 0f;
            var (clockLeft, dividerX, weatherLeft) = HudLayout.StatusPlaces(panelWidth, hasClock ? clockGroup : 0f, weatherGroup, gap);
            if (hasClock && hasWeather)
            {
                divider!.gameObject.SetActive(true);
                Place(divider.rectTransform, dividerX - .5f, top + 3, 1, HudLayout.StatusHeight - 6);
            }
            if (hasClock)
            {
                var textLeft = clockLeft;
                if (showClockIcon)
                {
                    SetIcon(clockIcon!, HudIcons.Clock(night), night ? MoonTint : Gold);
                    Place(clockIcon!.rectTransform, clockLeft, iconY, IconSize, IconSize);
                    textLeft = clockLeft + IconSize + 3f;
                }
                clockText.alignment = TextAlignmentOptions.Left;
                Place(clockText.rectTransform, textLeft, top, clockWidth + 2f, HudLayout.StatusHeight);
            }
            else clockText.gameObject.SetActive(false);
            if (hasWeather)
            {
                var eventGlyph = tempestEvent != null ? EventGlyph(tempestEvent.Value.Glyph) : null;
                if (eventGlyph != null) SetGlyph(weatherIcon!, eventGlyph);
                else
                {
                    var weatherMark = HudIcons.For(category, night);
                    SetIcon(weatherIcon!, weatherMark, weatherMark == HudIcon.Moon ? MoonTint : WeatherTint(category));
                }
                Place(weatherIcon!.rectTransform, weatherLeft, iconY, IconSize, IconSize);
                weatherText.alignment = TextAlignmentOptions.Left;
                Place(weatherText.rectTransform, weatherLeft + IconSize + 3f, top, weatherWidth + 2f, HudLayout.StatusHeight);
            }
            else weatherText.gameObject.SetActive(false);
        }

        static readonly Color MoonTint = new Color(.8f, .84f, .92f, 1f);

        static Color WeatherTint(WeatherCategory category)
        {
            switch (category)
            {
                case WeatherCategory.Clear: return Gold;
                case WeatherCategory.Rain: case WeatherCategory.Thunderstorm: return new Color(.62f, .78f, 1f, 1f);
                case WeatherCategory.Snow: case WeatherCategory.Blizzard: return new Color(.88f, .95f, 1f, 1f);
                case WeatherCategory.AshRain: return new Color(.85f, .62f, .5f, 1f);
                case WeatherCategory.Boss: return new Color(1f, .45f, .35f, 1f);
                default: return new Color(.86f, .88f, .92f, 1f);
            }
        }

        static void DrawStrip(IReadOnlyList<HudRow> rows, HudOptions options, float heading, int panelWidth)
        {
            var key = (rows, panelWidth, PlateReady(HudRow.Compass));
            if (key == stripDrawn) return;
            stripDrawn = key;
            // One iron-bound plank behind the status line, the strip and the heading, whichever show (user decision 2026-10-13, option 2).
            var height = HudLayout.PlankHeight(rows);
            SetPlate(HudRow.Compass, height > 0, 0, 0, panelWidth, height, KitHudArt.CompassPlank, height * KitHudArt.PlankPixelsPerUnit);
            // Each end cap's middle rivet (three per cap, option 2): the plank's middle rows stretch, so it is an image of its own,
            // left off on a plank too short to fit it between the painted top and bottom rivets.
            var rivets = PlateReady(HudRow.Compass) && height >= CapRivetMinHeight;
            for (var i = 0; i < capRivets.Length; i++)
            {
                if (rivets)
                {
                    var x = (i == 0 ? KitHudArt.CapRivetX : panelWidth - KitHudArt.CapRivetX) - KitHudArt.CapRivetSize * .5f;
                    capRivets[i] = KitHudArt.Plate(panel!.Content, "Cairn_CapRivet" + i, KitHudArt.IronRivet, 32f,
                        new Vector2(x, -(height - KitHudArt.CapRivetSize) * .5f), new Vector2(KitHudArt.CapRivetSize, KitHudArt.CapRivetSize));
                }
                if (capRivets[i] != null) capRivets[i]!.gameObject.SetActive(rivets);
            }
        }

        static void DrawHeading(IReadOnlyList<HudRow> rows, float heading, int panelWidth, string language, long revision)
        {
            // The label shows whole degrees and the nearest point (HudText.Heading), so those are its key, not the exact heading.
            var key = (rows, panelWidth, Compass.Degrees(heading), Compass.Point(heading), language, revision, PlateReady(HudRow.Heading));
            if (key == headingDrawn) return;
            headingDrawn = key;
            var on = rows.Contains(HudRow.Heading);
            // The heading sits in a notch cut into the plank's lower band, not on a plate of its own.
            const float plateWidth = HudLayout.HeadingInsetWidth;
            var top = on ? HudLayout.Top(rows, HudRow.Heading) : 0;
            var insetTop = top + HudLayout.HeadingHeight - HudLayout.HeadingInsetHeight;
            SetPlate(HudRow.Heading, on, (panelWidth - plateWidth) * .5f, insetTop, plateWidth, HudLayout.HeadingInsetHeight, KitHudArt.CompassInset, HudLayout.HeadingInsetHeight * KitHudArt.PlankPixelsPerUnit);
            var label = labels[HudRow.Heading];
            label.gameObject.SetActive(on);
            if (!on) return;
            var headingText = HudText.Heading(heading);
            label.text = CairnText.Format(CairnTextKeys.HudHeading, headingText.Degrees, CairnText.Text(headingText.PointKey));
            Place(label.rectTransform, (panelWidth - plateWidth) * .5f + 6f, insetTop + 1f, plateWidth - 12f, HudLayout.HeadingInsetHeight - 1f);
        }

        static void DrawExtras(IReadOnlyList<HudRow> rows, int panelWidth, string language, long revision)
        {
            var ready = 0;
            for (var i = 0; i < extraRows.Count; i++) if (PlateReady(extraRows[i])) ready |= 1 << i;
            var key = (rows, panelWidth, ready);
            if (key != extrasDrawn)
            {
                extrasDrawn = key;
                foreach (var row in extraRows)
                {
                    var on = rows.Contains(row);
                    var top = on ? HudLayout.Top(rows, row) : 0;
                    SetPlate(row, on, 0, top, panelWidth, HudLayout.ExtraRowHeight, KitHudArt.HeadingPlate, KitHudArt.HeadingPlateSourceHeight);
                    labels[row].gameObject.SetActive(on);
                    // The Tempest row places its own glyph and text (DrawTempest).
                    if (on && row != HudRow.Tempest) Place(labels[row].rectTransform, HudLayout.StripCap * .6f, top, panelWidth - HudLayout.StripCap * 1.2f, HudLayout.ExtraRowHeight);
                }
            }
            // A row's text is written while the row shows and its values (radius, Tempest countdown, sleep reading) or the language change.
            // A hidden row's label is inactive, so its text is never seen; it is written on the frame the row shows.
            var last = CairnRadius.Last;
            var radius = last.HasValue
                ? (true, last.Value.Radius, last.Value.Base, last.Value.Multiplier, last.Value.LightTerm, last.Value.WeatherTerm, last.Value.AltitudeTerm, last.Value.LocationTerm, language, revision)
                : (false, 0f, 0f, 0f, 0f, 0f, 0f, 0f, language, revision);
            if (rows.Contains(HudRow.Radius) && radiusDrawn != radius)
            {
                radiusDrawn = radius;
                labels[HudRow.Radius].text = last.HasValue ? CairnText.Format(CairnTextKeys.RadiusLine, HudText.RadiusArgument(last.Value)) : "";
            }
            if (rows.Contains(HudRow.RadiusInputs) && inputsDrawn != radius)
            {
                inputsDrawn = radius;
                labels[HudRow.RadiusInputs].text = last.HasValue ? CairnText.Format(CairnTextKeys.RadiusInputs, HudText.RadiusInputArguments(last.Value).Cast<object>().ToArray()) : "";
            }
            var line = CairnTempest.Line;
            var tempest = (line.Kind, line.TextKey, line.EventKey, line.Countdown, language, revision, rows, panelWidth);
            if (!rows.Contains(HudRow.Tempest)) { if (eventIcon!.gameObject.activeSelf) eventIcon.gameObject.SetActive(false); }
            else if (tempestDrawn != tempest)
            {
                tempestDrawn = tempest;
                DrawTempest(rows, line, panelWidth);
            }
            var sleepLine = CairnSleep.Line;
            var sleep = (sleepLine.Visible, sleepLine.Sleeping, sleepLine.Players, sleepLine.SkipPercent, sleepLine.BoostPercent, language, revision);
            if (rows.Contains(HudRow.Sleep) && sleepDrawn != sleep)
            {
                sleepDrawn = sleep;
                labels[HudRow.Sleep].text = CairnSleep.Text;
            }
        }

        /// <summary>The event's glyph just left of the centered text, as on Tempest's own badge.</summary>
        static void DrawTempest(IReadOnlyList<HudRow> rows, TempestLine line, int panelWidth)
        {
            var label = labels[HudRow.Tempest];
            var text = CairnTempest.Text(line);
            label.text = text;
            label.color = KitAppearance.Color(KitColorRole.OnPrimary);
            var top = HudLayout.Top(rows, HudRow.Tempest);
            var left = HudLayout.StripCap * .6f;
            var room = panelWidth - HudLayout.StripCap * 1.2f;
            var glyph = line.Kind == TempestLineKind.None ? null : EventGlyph(line.Glyph);
            if (glyph == null)
            {
                eventIcon!.gameObject.SetActive(false);
                label.alignment = TextAlignmentOptions.Center;
                Place(label.rectTransform, left, top, room, HudLayout.ExtraRowHeight);
                return;
            }
            const float size = HudLayout.ExtraRowHeight - 3f, gap = 3f;
            var width = Mathf.Min(label.GetPreferredValues(text).x, room - size - gap);
            var start = (panelWidth - (size + gap + width)) * .5f;
            SetGlyph(eventIcon!, glyph);
            Place(eventIcon!.rectTransform, start, top + (HudLayout.ExtraRowHeight - size) * .5f, size, size);
            label.alignment = TextAlignmentOptions.Left;
            Place(label.rectTransform, start + size + gap, top, width + 2f, HudLayout.ExtraRowHeight);
        }

        /// <summary>Tempest's badge glyph for the event: snowflake, bolt for both storms, cloud for fog, star for the ash storm and meteor shower.</summary>
        static Sprite? EventGlyph(TempestGlyph glyph)
        {
            switch (glyph)
            {
                case TempestGlyph.Snowflake: return KitGlyphSprites.Of(KitGlyph.Snowflake);
                case TempestGlyph.Cloud: return KitGlyphSprites.Of(KitGlyph.Cloud);
                case TempestGlyph.Star: return KitGlyphSprites.Of(KitGlyph.Star);
                default: return KitGlyphSprites.Of(KitGlyph.Bolt);
            }
        }

        /// <summary>A kit glyph in the badge's tint (the MeadWorks lit-text color, the same in Timber and Brown).</summary>
        static void SetGlyph(Image image, Sprite glyph)
        {
            image.sprite = glyph;
            image.color = KitAppearance.Color(KitColorRole.OnPrimary);
            image.gameObject.SetActive(true);
        }

        /// <summary>Shows or hides one row's plate image and puts it at the given place; a missing image leaves the text on the bare HUD.</summary>
        static void SetPlate(HudRow row, bool on, float x, float y, float width, float height, Sprite? sprite, float sourceHeight)
        {
            if (!plates.TryGetValue(row, out var image) || image == null)
            {
                if (!on || sprite == null || panel == null) return;
                image = KitHudArt.Plate(panel.Content, "Cairn_Plate_" + row, sprite, sourceHeight, new Vector2(x, -y), new Vector2(width, height));
                if (image == null) return;
                plates[row] = image;
                image.transform.SetAsFirstSibling();
                // The plank lies under every other plate (the heading inset is cut into it).
                if (plates.TryGetValue(HudRow.Compass, out var plank) && plank != null) plank.transform.SetAsFirstSibling();
            }
            image.gameObject.SetActive(on);
            if (!on) return;
            KitHudArt.Plate(panel!.Content, image.gameObject.name, sprite!, sourceHeight, new Vector2(x, -y), new Vector2(width, height));
            image.raycastTarget = false;
        }

        /// <summary>The HUD is hidden by its toggle (key, pad chord or the MeadWorks menu entry) this session.</summary>
        internal static bool ToggledOff => toggledOff;

        /// <summary>The MeadWorks menu entry: the same toggle as the key.</summary>
        internal static void ToggleFromMenu()
        {
            if (!CairnModules.StepAside.SteppedAside(CairnModule.Hud)) toggledOff = !toggledOff;
        }

        static bool TogglePressed(CairnConfig settings)
        {
            // The key text is parsed again only when the setting changes.
            var text = settings.Get(CairnSettings.Hud.ToggleKey);
            if (!string.Equals(text, toggleKeyText, StringComparison.Ordinal))
            {
                toggleKeyText = text;
                toggleKeyValid = Enum.TryParse(text, true, out KeyCode key);
                toggleKey = toggleKeyValid ? new KeyboardShortcut(key) : KeyboardShortcut.Empty;
            }
            // MeadworksInput.Pressed fires only on the frame Input.GetKeyDown reports the main key, so asking Unity first skips its
            // per-call chord parse on every other frame and answers exactly as before.
            if (toggleKeyValid && Input.GetKeyDown(toggleKey.MainKey) && MeadworksInput.Pressed(toggleKey)) return true;
            return PadChordPressed(settings.Get(CairnSettings.Hud.GamepadToggle));
        }

        internal static bool PadChordPressed(string value)
        {
            // A chord is parsed once per setting text; null when any part is not a pad button.
            if (!chords.TryGetValue(value, out var buttons))
            {
                var parsed = new List<PadButton>();
                foreach (var name in value.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!GamepadMap.TryParse(name, out var button)) { parsed = null; break; }
                    parsed.Add(button);
                }
                buttons = parsed?.ToArray();
                chords[value] = buttons;
            }
            if (buttons == null) return false;
            if (!MeadworksInput.GamepadActive || buttons.Length == 0) return false;
            foreach (var button in buttons) if (!MeadworksInput.PadHeld(button)) return false;
            var down = false;
            foreach (var button in buttons) if (MeadworksInput.PadDown(button)) { down = true; break; }
            if (!down) return false;
            foreach (var button in buttons) MeadworksInput.PadConsume(button);
            return true;
        }

        static void Build(Transform root)
        {
            panel = KitHudPanel.Create(root, "Cairn_Hud", new Vector2(HudLayout.MinWidth, 51), Vector2.zero, global::ExtraPotions.Cairn.Runtime.Plugin.Guid, "Cairn_Hud");
            if (panel == null) return;
            owner = root;
            // Plates are created lazily (first frame a row shows) so they sit under the text; texts and marks are created here.
            clockText = Label("Clock", 12, Cream); weatherText = Label("Weather", 12, Cream);
            clockIcon = Glyph("ClockIcon"); weatherIcon = Glyph("WeatherIcon"); eventIcon = Glyph("TempestIcon");
            // A Timber/Brown or theme change draws every part again with the current kit colors.
            KitAppearance.Changed -= Redraw;
            KitAppearance.Changed += Redraw;
            divider = Bar("Divider", Bronze);
            pointer = Bar("Pointer", Gold);
            foreach (HudRow row in Enum.GetValues(typeof(HudRow)))
            {
                if (row == HudRow.Status || row == HudRow.Compass) continue;
                var text = Label(row.ToString(), row == HudRow.RadiusInputs ? 10 : row == HudRow.Heading ? 9 : 11, row == HudRow.Heading ? Cream : KitAppearance.Color(KitColorRole.OnPrimary));
                labels.Add(row, text);
                if (HudLayout.IsExtra(row)) extraRows.Add(row);
            }
            Redraw();
            // TMP drops a whole line taller than its rect under Truncate; the heading never needs cutting.
            labels[HudRow.Heading].overflowMode = TextOverflowModes.Overflow;
        }

        static TMP_Text Label(string name, int size, Color colour)
        {
            var text = KitCanvas.Text(panel!.Content, "Cairn_" + name, "", size, colour, TextAlignmentOptions.Center, false);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        static Image Glyph(string name)
        {
            var image = Bar(name, Color.white);
            image.preserveAspect = true;
            return image;
        }

        static Image Bar(string name, Color colour)
        {
            var go = new GameObject("Cairn_" + name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(panel!.Content, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = colour;
            go.SetActive(false);
            return image;
        }

        static void SetIcon(Image image, HudIcon icon, Color tint)
        {
            if (!icons.TryGetValue(icon, out var sprite) || sprite == null)
            {
                var mask = HudIcons.Mask(icon, 16);
                var pixels = new Color32[mask.Length];
                for (var y = 0; y < 16; y++)
                    for (var x = 0; x < 16; x++)
                        pixels[(15 - y) * 16 + x] = new Color32(255, 255, 255, mask[y * 16 + x]);
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Cairn_Icon_" + icon };
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f), 100f);
                sprite.name = texture.name;
                icons[icon] = sprite;
            }
            image.sprite = sprite;
            image.color = tint;
            image.gameObject.SetActive(true);
        }

        static void DrawCompass(bool show, IReadOnlyList<HudRow> rows, float heading, int width, int panelWidth, Player player, CairnConfig settings, string language, long revision)
        {
            var markersOn = show && settings.Get(CairnSettings.Hud.ShowMarkers) && Minimap.instance != null;
            // The pin choice is read again every quarter second, as before; only while the compass draws markers.
            if (markersOn && Time.unscaledTime >= nextMarkers)
            {
                nextMarkers = Time.unscaledTime + .25f;
                var choices = CairnMarkers.Load();
                var next = Minimap.instance!.m_pins.Where(p => p != null && (choices.Contains("type:" + p.m_type) || choices.Contains(MarkerChoices.PinKey(p.m_pos.x, p.m_pos.z, p.m_type.ToString())))).ToList();
                // The same pins keep the same list, so the compass key (which holds the list) does not change four times a second for nothing.
                if (!SameList(next, selected)) selected = next;
            }
            var at = player.transform.position;
            // Where the player stands only moves the chosen pins on the strip (bearing and distance). With no pin chosen nothing drawn
            // depends on it, so it is left out of the key: standing still still drifts the position a little every frame.
            var pinsMatter = markersOn && selected.Count > 0;
            var key = show
                ? (true, rows, panelWidth, settings.Get(CairnSettings.Hud.StripDegrees), heading, language, revision, markersOn, markersOn ? selected : null,
                    pinsMatter ? at.x : 0f, pinsMatter ? at.z : 0f, markersOn ? settings.Get(CairnSettings.Hud.MaxMarkers) : 0, markersOn ? settings.Get(CairnSettings.Hud.MaxDistance) : 0f)
                : (false, rows, panelWidth, 0, 0f, null, 0L, false, null, 0f, 0f, 0, 0f);
            // A chosen pin can move (another player's pin) or change its icon without the list changing, so those are compared too.
            var pinsSame = !markersOn || SamePins(selected);
            if (key == compassDrawn && pinsSame) return;
            compassDrawn = key;
            if (!pinsSame) { drawnPins.Clear(); foreach (var p in selected) drawnPins.Add((p.m_pos.x, p.m_pos.z, p.m_icon, p.m_icon != null, p.m_type)); }
            foreach (var tick in ticks) tick.gameObject.SetActive(false);
            foreach (var mark in marks) mark.gameObject.SetActive(false);
            foreach (var marker in markers) { marker.Icon.gameObject.SetActive(false); marker.Distance.gameObject.SetActive(false); }
            pointer!.gameObject.SetActive(show);
            if (!show) return;
            var top = HudLayout.Top(rows, HudRow.Compass);
            var centre = panelWidth * .5f;
            var degrees = settings.Get(CairnSettings.Hud.StripDegrees);
            Place(pointer.rectTransform, centre - 1f, top + HudLayout.PointerTop, 2f, HudLayout.PointerHeight);
            var i = 0;
            foreach (var mark in Compass.Marks(heading, degrees, width))
            {
                if (i == marks.Count) marks.Add(Bar("Mark" + i, Cream));
                var image = marks[i++];
                var high = mark.Kind == StripMarkKind.Point ? 6f : mark.Kind == StripMarkKind.Number ? 4f : 2f;
                var tint = Cream;
                tint.a = (mark.Kind == StripMarkKind.Minor ? .55f : .9f) * Compass.EdgeFade(mark.Offset, width);
                if (mark.Degrees == 0 && mark.Kind == StripMarkKind.Point) tint = new Color(.92f, .25f, .2f, tint.a);
                image.color = tint;
                image.gameObject.SetActive(true);
                Place(image.rectTransform, centre + mark.Offset - .5f, top + 3f, 1f, high);
            }
            // Pins first, so the numbers can step aside for a pin's distance.
            pinOffsets.Clear();
            var pinsShown = false;
            if (markersOn)
            {
                var pins = selected.Select((p, index) => new MarkerPin(index.ToString(), p.m_pos.x, p.m_pos.z));
                var m = 0;
                foreach (var pin in Compass.Nearest(pins, at.x, at.z, settings.Get(CairnSettings.Hud.MaxMarkers), settings.Get(CairnSettings.Hud.MaxDistance)))
                {
                    if (m == markers.Count)
                    {
                        var go = new GameObject("Cairn_CompassPin" + m, typeof(RectTransform), typeof(Image));
                        go.transform.SetParent(panel!.Content, false);
                        var image = go.GetComponent<Image>(); image.raycastTarget = false;
                        markers.Add((image, Label("CompassDistance" + m, 8, Cream)));
                    }
                    var marker = markers[m++];
                    var source = selected[int.Parse(pin.Id)];
                    var offset = Compass.OnStrip(Compass.Bearing(at.x, at.z, pin.X, pin.Z), heading, degrees, width);
                    var x = centre + offset.Offset;
                    marker.Icon.sprite = source.m_icon != null ? source.m_icon : Minimap.instance!.GetSprite(source.m_type);
                    marker.Icon.gameObject.SetActive(marker.Icon.sprite != null);
                    Place(marker.Icon.rectTransform, x - 5, top + 3, 10, 10);
                    marker.Distance.gameObject.SetActive(true);
                    marker.Distance.text = Compass.DistanceText(Compass.Distance(at.x, at.z, pin.X, pin.Z));
                    var alpha = Compass.MarkerAlpha(offset.Parked, offset.Offset, width);
                    var iconTint = marker.Icon.color; iconTint.a = alpha; marker.Icon.color = iconTint;
                    var textTint = marker.Distance.color; textTint.a = alpha; marker.Distance.color = textTint;
                    Place(marker.Distance.rectTransform, x - 24, top + 13, 48, 9);
                    pinOffsets.Add(offset.Offset);
                    pinsShown = true;
                }
            }
            i = 0;
            foreach (var tick in Compass.Ticks(heading, degrees, width))
            {
                if (i == ticks.Count)
                {
                    ticks.Add(Label("CompassTick" + i, 9, Cream));
                    // Truncate drops a whole line taller than its rect; a point letter must never vanish.
                    ticks[i].overflowMode = TextOverflowModes.Overflow;
                }
                var label = ticks[i++];
                var covered = false;
                if (pinsShown) foreach (var o in pinOffsets) if (Math.Abs(o - tick.Offset) < 22f) { covered = true; break; }
                label.gameObject.SetActive(!covered);
                if (covered) continue;
                label.text = tick.Degrees % 45 == 0 ? CairnText.Text(CairnTextKeys.CompassPoint(tick.Label)) : tick.Label;
                var tint = tick.North ? new Color(.95f, .27f, .22f) : Cream;
                tint.a *= Compass.EdgeFade(tick.Offset, width);
                label.color = tint;
                label.fontSize = tick.Major ? 10 : 8;
                Place(label.rectTransform, centre + tick.Offset - 14, top + HudLayout.TickLabelTop, 28, HudLayout.TickLabelHeight);
            }
        }

        static bool SameList(List<Minimap.PinData> a, List<Minimap.PinData> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++) if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        static bool SamePins(List<Minimap.PinData> pins)
        {
            if (pins.Count != drawnPins.Count) return false;
            for (var i = 0; i < pins.Count; i++)
            {
                var p = pins[i]; var d = drawnPins[i];
                if (p.m_pos.x != d.X || p.m_pos.z != d.Z || !ReferenceEquals(p.m_icon, d.Icon) || (p.m_icon != null) != d.IconAlive || p.m_type != d.Type) return false;
            }
            return true;
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void Hide() { HideHud(); CairnMinimapEffects.Hide(); }

        static void HideHud()
        {
            TempestNotice.HudRowDrawn = false;
            panel?.Update(false, false, 1, 0);
        }
        static void DisposePanel()
        {
            TempestNotice.HudRowDrawn = false;
            if (panel != null) KitAppearance.Changed -= Redraw;
            panel?.Dispose(); panel = null; owner = null;
            labels.Clear(); plates.Clear(); System.Array.Clear(capRivets, 0, capRivets.Length); ticks.Clear(); marks.Clear(); markers.Clear(); placement = null;
            extraRows.Clear(); Redraw();
            clockText = weatherText = null; clockIcon = weatherIcon = eventIcon = divider = pointer = null;
        }
        public static void Reset()
        {
            CairnMinimapEffects.Dispose(); DisposePanel(); selected.Clear(); character = null; world = 0; toggledOff = false; ownsSleep = showingSleep = false; nextMarkers = 0;
            foreach (var sprite in icons.Values) if (sprite != null) { UnityEngine.Object.Destroy(sprite.texture); UnityEngine.Object.Destroy(sprite); }
            icons.Clear();
        }
    }
}
