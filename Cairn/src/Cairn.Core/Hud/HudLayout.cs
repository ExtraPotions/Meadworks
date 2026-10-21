// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: HUD rows and content follow TimeWeatherHud 1.1.0's documented behavior; Cairn uses a fixed
// top-center placement and a player-set compass width. No source code or art is used.
using System;
using System.Collections.Generic;
using System.Globalization;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Localization;
using ExtraPotions.Cairn.Radius;

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>
    /// The lines the kit HUD plate can draw, top to bottom. The first three are the plate itself (the status line with clock and
    /// weather, the compass strip, the heading); every other line is an extra that only appears when it has something to say.
    /// </summary>
    public enum HudRow { Status, Compass, Heading, Sleep, Tempest, Radius, RadiusInputs }

    /// <summary>The settings and state that decide which rows show.</summary>
    public readonly struct HudOptions
    {
        public HudOptions(bool showDay, bool showTime, bool showWeather, bool showCompass, bool showHeading, bool showRadius, bool showRadiusInputs, TempestLineKind tempest, bool sleep = false)
        {
            ShowDay = showDay;
            ShowTime = showTime;
            ShowWeather = showWeather;
            ShowCompass = showCompass;
            ShowHeading = showHeading;
            ShowRadius = showRadius;
            ShowRadiusInputs = showRadiusInputs;
            Tempest = tempest;
            Sleep = sleep;
        }

        public bool ShowDay { get; }
        public bool ShowTime { get; }
        public bool ShowWeather { get; }
        public bool ShowCompass { get; }
        public bool ShowHeading { get; }
        public bool ShowRadius { get; }
        public bool ShowRadiusInputs { get; }
        public bool Sleep { get; }

        /// <summary>The Tempest row this frame; <see cref="TempestLineKind.None"/> draws no row.</summary>
        public TempestLineKind Tempest { get; }
    }

    public readonly struct HudSize
    {
        public HudSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
    }

    /// <summary>Fixed pixel rules for the HUD plate.</summary>
    public static class HudLayout
    {
        /// <summary>The forged-iron end cap at each end of the compass plank; the ticks run between the two.</summary>
        public const int StripCap = 22;
        /// <summary>Solo's TimeWeatherHud is 320 pixels wide at 1080p with 14 pixels above it: the 240-pixel plate drawn at 4/3.</summary>
        public const float Scale = 4f / 3f;
        public const int TopGap = 14;
        public const int MinWidth = 240;
        /// <summary>The widest compass plank the Compass Strip Width setting allows.</summary>
        public const int MaxWidth = 640;
        /// <summary>The line with the clock and the weather.</summary>
        public const int StatusHeight = 17;
        /// <summary>The compass strip with its tick marks, numbers and pins.</summary>
        public const int CompassRowHeight = 22;
        /// <summary>The clock and weather marks, square, inside the status line.</summary>
        public const int IconSize = 15;
        /// <summary>The center pointer: inside the strip row, measured from the strip's top.</summary>
        public const int PointerTop = 1;
        public const int PointerHeight = 6;
        /// <summary>Numbers and point letters in the strip's lower part, under the tick marks; 14 units hold a size-10 line.</summary>
        public const int TickLabelTop = 8;
        public const int TickLabelHeight = 14;
        /// <summary>The exact heading under the strip, in small text.</summary>
        public const int HeadingHeight = 12;
        /// <summary>The notch in the plank's lower band that holds the heading (user decision 2026-10-13: inside the frame, not a plate).</summary>
        public const int HeadingInsetWidth = 64;
        public const int HeadingInsetHeight = 14;
        /// <summary>One extra line (sleep, storm, radius).</summary>
        public const int ExtraRowHeight = 14;
        /// <summary>
        /// Wood above the plank's first row, and under its last row unless that is the heading (whose notch sits in the lower strap):
        /// the iron straps fill the top and bottom 4.75 units, so the 15-unit status icons and the strip's ticks and numbers clear them
        /// with about a unit to spare, as in the concept sketch.
        /// </summary>
        public const int PlankPad = 5;

        /// <summary>The rows to draw for these options, in plate order. The heading needs the compass.</summary>
        public static IReadOnlyList<HudRow> Rows(HudOptions options)
        {
            var rows = new List<HudRow>();
            if (options.ShowDay || options.ShowTime || options.ShowWeather) rows.Add(HudRow.Status);
            if (options.ShowCompass) rows.Add(HudRow.Compass);
            if (options.ShowCompass && options.ShowHeading) rows.Add(HudRow.Heading);
            if (options.Sleep) rows.Add(HudRow.Sleep);
            if (options.Tempest != TempestLineKind.None) rows.Add(HudRow.Tempest);
            if (options.ShowRadius) rows.Add(HudRow.Radius);
            if (options.ShowRadiusInputs) rows.Add(HudRow.RadiusInputs);
            return rows;
        }

        /// <summary>
        /// The compass plank's height: the status line, the strip and the heading, whichever show, are one iron-bound plank
        /// (user decision 2026-10-13, compass option 2). Zero when none of the three shows.
        /// </summary>
        public static int PlankHeight(IReadOnlyList<HudRow> rows)
        {
            var height = 0;
            var heading = false;
            foreach (var row in rows ?? Array.Empty<HudRow>())
                if (!IsExtra(row)) { height += RowHeight(row); heading |= row == HudRow.Heading; }
            return height == 0 ? 0 : height + PlankPad + (heading ? 0 : PlankPad);
        }

        /// <summary>True for the lines under the plate that only show when they have something to say.</summary>
        public static bool IsExtra(HudRow row) => row != HudRow.Status && row != HudRow.Compass && row != HudRow.Heading;

        public static int RowHeight(HudRow row)
        {
            switch (row)
            {
                case HudRow.Status: return StatusHeight;
                case HudRow.Compass: return CompassRowHeight;
                case HudRow.Heading: return HeadingHeight;
                default: return ExtraRowHeight;
            }
        }

        /// <summary>The row's top edge in pixels from the plate's top: plank rows start under <see cref="PlankPad"/>, extras under the plank.</summary>
        public static int Top(IReadOnlyList<HudRow> rows, HudRow row)
        {
            rows ??= Array.Empty<HudRow>();
            var y = IsExtra(row) ? PlankHeight(rows) : PlankPad;
            foreach (var r in rows)
            {
                if (r == row) return y;
                if (IsExtra(r) == IsExtra(row)) y += RowHeight(r);
            }
            return y;
        }

        /// <summary>
        /// The plate: <paramref name="compassWidth"/> wide (clamped to <see cref="MinWidth"/>..<see cref="MaxWidth"/>) when the
        /// compass shows, else the minimum; tall enough for every row.
        /// </summary>
        public static HudSize Size(IReadOnlyList<HudRow> rows, int compassWidth = MinWidth)
        {
            rows ??= Array.Empty<HudRow>();
            var height = PlankHeight(rows);
            var compass = false;
            foreach (var row in rows)
            {
                if (IsExtra(row)) height += RowHeight(row);
                compass |= row == HudRow.Compass;
            }
            return new HudSize(compass ? Math.Min(MaxWidth, Math.Max(MinWidth, compassWidth)) : MinWidth, height);
        }

        /// <summary>
        /// Left edges of the clock group, the divider and the weather group on the status line. The whole line (clock, gap, divider,
        /// gap, weather) is centered on the plank, so a long clock beside a short weather word does not push the line off center.
        /// </summary>
        public static (float Clock, float Divider, float Weather) StatusPlaces(float panelWidth, float clockGroup, float weatherGroup, float gap)
        {
            var centre = panelWidth * .5f;
            if (clockGroup <= 0) return (centre, centre, centre - weatherGroup * .5f);
            if (weatherGroup <= 0) return (centre - clockGroup * .5f, centre, centre);
            var left = centre - (clockGroup + 2 * gap + weatherGroup) * .5f;
            return (left, left + clockGroup + gap, left + clockGroup + 2 * gap);
        }

        /// <summary>The status line's text size, and the smallest a long weather or event name shrinks to before it is ellipsized.</summary>
        public const float StatusFontSize = 12f, StatusMinFontSize = 8f;
        /// <summary>Space between the clock group, the divider and the weather group.</summary>
        public const float StatusGap = 7f;
        /// <summary>Between a status mark and its text.</summary>
        public const float IconGap = 3f;

        /// <summary>The status line's usable width: the plank between its iron end caps (each overlapped by 4 units).</summary>
        public static float StatusRoom(float panelWidth) => panelWidth - 2f * (StripCap - 4);

        /// <summary>
        /// Width left for the weather name once the clock group (mark and text, 0 when hidden), the two gaps and the divider, and
        /// the weather mark are placed. The clock is never cut for the weather.
        /// </summary>
        public static float WeatherTextRoom(float panelWidth, float clockGroup) =>
            Math.Max(0f, StatusRoom(panelWidth) - (clockGroup > 0 ? clockGroup + 2 * StatusGap : 0f) - IconSize - IconGap);

        /// <summary>
        /// Best fit for one line of text: <paramref name="widthAtBase"/> is its width at <paramref name="baseSize"/> (text width scales
        /// with font size). Keeps the base size when it fits, else shrinks in half-point steps down to <paramref name="minSize"/>;
        /// Ellipsis is true only when even the minimum size is too wide.
        /// </summary>
        public static (float Size, bool Ellipsis) FitText(float widthAtBase, float room, float baseSize = StatusFontSize, float minSize = StatusMinFontSize)
        {
            if (widthAtBase <= room) return (baseSize, false);
            var size = room <= 0 ? 0f : (float)Math.Floor(baseSize * room / widthAtBase * 2f) / 2f;
            return size >= minSize ? (size, false) : (minSize, true);
        }

        /// <summary>The fixed top-center location of a plate <paramref name="shownWidth"/> wide after scaling (y grows downwards).</summary>
        public static (int X, int Y) Home(int parentWidth, int shownWidth) => ((parentWidth - shownWidth) / 2, TopGap);

        /// <summary>Shown on a graphical client while the HUD module runs, unless the toggle key hid it or the game's HUD, map or a menu blocks it.</summary>
        public static bool Visible(bool enabled, bool allowsUi, bool steppedAside, bool toggledOff, bool blocked) =>
            enabled && allowsUi && !steppedAside && !toggledOff && !blocked;

        /// <summary>
        /// Cairn owns the sleep display (Ember hides its own board) while its HUD runs and Ember reports a reading. A window, the
        /// inventory or a menu only blocks the HUD for a moment, so it keeps ownership then; the toggle key or stepping aside gives it up.
        /// </summary>
        public static bool OwnsSleep(bool enabled, bool allowsUi, bool steppedAside, bool toggledOff, bool reading) =>
            enabled && allowsUi && !steppedAside && !toggledOff && reading;

        /// <summary>Space kept between the plank's edge and the nearest status effect cell.</summary>
        public const float StatusEffectMargin = 8f;

        /// <summary>
        /// How many of the game's status effects fit on one row before it wraps. The row grows leftwards from its first cell,
        /// <paramref name="cell"/> wide, whose center is <paramref name="firstCenter"/>; it must stop
        /// <see cref="StatusEffectMargin"/> short of the plank's right edge <paramref name="plankRight"/>. The game's own count
        /// (<paramref name="nativePerRow"/>) applies when the plank is not shown or does not share the row's height band; the
        /// result is never more than that count and never less than one.
        /// </summary>
        public static int StatusEffectsPerRow(int nativePerRow, float firstCenter, float cell, bool plankShown, float plankRight,
            float plankTop, float plankBottom, float rowTop, float rowBottom)
        {
            var native = Math.Max(1, nativePerRow);
            if (!plankShown || cell <= 0 || plankBottom <= rowTop || plankTop >= rowBottom) return native;
            var room = firstCenter - cell * .5f - (plankRight + StatusEffectMargin);
            if (room < 0) return 1;
            return Math.Max(1, Math.Min(native, 1 + (int)Math.Floor(room / cell)));
        }

    }

    /// <summary>
    /// Keeps the status effect row's wrap while a window is open (the inventory and crafting window, the Esc menu, the large map
    /// or a MeadWorks window that blocks the HUD). Those windows hide the plank, and without a hold the row would rewrap to the
    /// game's seven when one opens and wrap again when it closes. The hold ends <see cref="ReleaseFrames"/> frames after the
    /// last open frame, so a plank that reappears a frame late does not make the row jump on close. Mantle holds the same way.
    /// </summary>
    public sealed class StatusRowHold
    {
        /// <summary>Frames after the window closes during which the held layout stays.</summary>
        public const int ReleaseFrames = 2;

        bool holding;
        int lastOpenFrame;

        /// <summary>True while the row's layout is held. Called with the frame number once or more per frame.</summary>
        public bool Holding(bool windowOpen, int frame)
        {
            if (windowOpen)
            {
                holding = true;
                lastOpenFrame = frame;
                return true;
            }
            if (holding && frame >= lastOpenFrame && frame - lastOpenFrame <= ReleaseFrames) return true;
            holding = false;
            return false;
        }

        public void Reset() => holding = false;
    }

    /// <summary>Day and time parts of the clock row; empty parts are left out.</summary>
    public readonly struct ClockParts
    {
        public ClockParts(string day, string time)
        {
            Day = day;
            Time = time;
        }

        /// <summary>The day number as text (the plugin formats <see cref="CairnTextKeys.HudDay"/> with it), or empty.</summary>
        public string Day { get; }

        public string Time { get; }
    }

    /// <summary>The heading row: whole degrees and the catalog key of the nearest point.</summary>
    public readonly struct HeadingParts
    {
        public HeadingParts(string degrees, string pointKey)
        {
            Degrees = degrees;
            PointKey = pointKey;
        }

        public string Degrees { get; }
        public string PointKey { get; }
    }

    /// <summary>The texts of the HUD rows, culture-invariant; the plugin localizes the catalog keys.</summary>
    public static class HudText
    {
        public static ClockParts Clock(bool showDay, bool showTime, int day, float dayFraction, bool use24Hour) =>
            new ClockParts(showDay ? Math.Max(0, day).ToString(CultureInfo.InvariantCulture) : "", showTime ? Hud.Clock.Text(dayFraction, use24Hour) : "");

        /// <summary>Two parts on one row with two spaces between; a missing part leaves no gap.</summary>
        public static string Join(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second ?? "";
            if (string.IsNullOrEmpty(second)) return first;
            return first + "  " + second;
        }

        /// <summary>Two parts on one line with a middle dot between; a missing part leaves no dot.</summary>
        public static string Dot(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second ?? "";
            if (string.IsNullOrEmpty(second)) return first;
            return first + " \u00b7 " + second;
        }

        public static HeadingParts Heading(float heading)
        {
            var degrees = Compass.Degrees(heading);
            return new HeadingParts(degrees.ToString(CultureInfo.InvariantCulture), CairnTextKeys.CompassPoint(Compass.Point(heading)));
        }

        /// <summary>The argument of <see cref="CairnTextKeys.RadiusLine"/>: the radius in whole meters.</summary>
        public static string RadiusArgument(RadiusResult result) => Whole(result.Radius);

        /// <summary>The seven arguments of <see cref="CairnTextKeys.RadiusInputs"/>: radius, base, multiplier, light, weather, altitude, location.</summary>
        public static string[] RadiusInputArguments(RadiusResult result) => new[]
        {
            Whole(result.Radius), Whole(result.Base), result.Multiplier.ToString("0.00", CultureInfo.InvariantCulture),
            Signed(result.LightTerm), Signed(result.WeatherTerm), Signed(result.AltitudeTerm), Signed(result.LocationTerm),
        };

        private static string Whole(float value) => Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

        private static string Signed(float value) => value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Where the sleep row's parts sit on its forged-iron plate under the minimap (user decision 2026-10-13 09:30, panel option 1):
    /// the moon, the bed, the sleepers count ("1/3") and the bonus or skip progress, one group centered on the plate, clear of the
    /// corner rivets.
    /// </summary>
    public readonly struct SleepRowCells
    {
        public const float Moon = 18f, Bed = 20f, Gap = 3f, CountMinWidth = 26f, BoostGap = 8f, BoostWidth = 36f;
        /// <summary>The plate's corner rivets: their size and their inset from the plate's edges.</summary>
        public const float Rivet = 9f, RivetInset = 3f;
        /// <summary>Nothing of the group comes nearer the plate's ends than this (the rivets and a little air).</summary>
        public const float Edge = RivetInset + Rivet + 2f;
        /// <summary>The whole group's width: moon, bed, count and bonus with their gaps.</summary>
        public const float GroupWidth = Moon + Gap + Bed + Gap + CountMinWidth + BoostGap + BoostWidth;

        public float MoonX { get; }
        public float BedX { get; }
        public float CountX { get; }
        public float CountWidth { get; }
        public float BoostX { get; }

        public SleepRowCells(float width, float height)
        {
            MoonX = Math.Max(Edge, (width - GroupWidth) / 2f);
            BedX = MoonX + Moon + Gap;
            CountX = BedX + Bed + Gap;
            CountWidth = CountMinWidth;
            BoostX = CountX + CountWidth + BoostGap;
        }
    }

    /// <summary>
    /// The capability Cairn publishes so Tempest can hide its own badge while Cairn's HUD shows the warning
    /// (user decision, Tempest runtime item 2 of 2026-10-08): <c>Func&lt;bool&gt;</c>, true while the Tempest row is drawn.
    /// </summary>
    public static class WeatherDisplayCapability
    {
        public const string Id = "meadworks.cairn.weather_display";

        public static Version ContractVersion { get; } = new Version(1, 0);

        /// <summary>True while the panel is visible, Tempest Warnings is on and the row shows an event.</summary>
        public static bool Showing(bool panelVisible, bool tempestRowEnabled, TempestLineKind kind) =>
            panelVisible && tempestRowEnabled && kind != TempestLineKind.None;
    }
}
