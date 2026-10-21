// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: behavior from TimeWeatherHud 1.1.0's README (strip compass, N in red, numbers every
// 15 degrees, heading text such as "127° SE", markers nearest first that wait at the strip's edge). No code or art used.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>A map pin the compass may show, in world x and z.</summary>
    public readonly struct MarkerPin
    {
        public MarkerPin(string id, float x, float z)
        {
            Id = id;
            X = x;
            Z = z;
        }

        public string Id { get; }
        public float X { get; }
        public float Z { get; }
    }

    /// <summary>Where a bearing lands on the strip: pixels from the center (right positive), parked when outside the view.</summary>
    public readonly struct StripPosition
    {
        public StripPosition(float offset, bool parked)
        {
            Offset = offset;
            Parked = parked;
        }

        public float Offset { get; }
        public bool Parked { get; }
    }

    /// <summary>A strip tick: degrees 0 to 359, label (a point letter or the number) and its offset.</summary>
    public readonly struct StripTick
    {
        public StripTick(int degrees, string label, float offset)
        {
            Degrees = degrees;
            Label = label;
            Offset = offset;
        }

        public int Degrees { get; }
        public string Label { get; }
        public float Offset { get; }

        /// <summary>North, drawn in red.</summary>
        public bool North => Degrees == 0;

        /// <summary>A compass point (multiples of 45 degrees): drawn larger than the numbers between.</summary>
        public bool Major => Degrees % 45 == 0;
    }

    /// <summary>How tall a strip mark is: a compass point (multiples of 45), a labeled number (multiples of 15) or a minor tick between.</summary>
    public enum StripMarkKind { Minor, Number, Point }

    /// <summary>A strip tick mark: degrees 0 to 359, its offset in pixels from the center (right positive) and its height class.</summary>
    public readonly struct StripMark
    {
        public StripMark(int degrees, StripMarkKind kind, float offset)
        {
            Degrees = degrees;
            Kind = kind;
            Offset = offset;
        }

        public int Degrees { get; }
        public StripMarkKind Kind { get; }
        public float Offset { get; }
    }

    public static class Compass
    {
        public const int MarkStep = 5;

        private static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        public const int TickStep = 15;

        /// <summary>Alpha at the strip's very edge; labels and markers fade to it so they slide in and out instead of popping.</summary>
        public const float MinFade = 0.25f;

        /// <summary>Alpha of a marker parked at the edge (outside the view).</summary>
        public const float ParkedAlpha = 0.55f;

        /// <summary>1 across the middle 70 percent of the strip, falling linearly to <see cref="MinFade"/> at the edge.</summary>
        public static float EdgeFade(float offset, int width)
        {
            var half = Math.Max(1, width) / 2f;
            var t = Math.Min(1f, Math.Abs(offset) / half);
            if (t <= 0.7f) return 1f;
            return 1f - (t - 0.7f) / 0.3f * (1f - MinFade);
        }

        /// <summary>A marker's alpha: parked ones are dimmer; the others fade towards the strip's edge.</summary>
        public static float MarkerAlpha(bool parked, float offset, int width) => parked ? ParkedAlpha : EdgeFade(offset, width);

        /// <summary>Degrees 0 to 360 (0 north, +z; 90 east, +x) of a horizontal direction.</summary>
        public static float Heading(float x, float z)
        {
            if (x == 0f && z == 0f) return 0f;
            return Wrap360((float)(Math.Atan2(x, z) * 180d / Math.PI));
        }

        public static float Bearing(float fromX, float fromZ, float toX, float toZ) => Heading(toX - fromX, toZ - fromZ);

        public static float Distance(float fromX, float fromZ, float toX, float toZ)
        {
            var dx = toX - fromX;
            var dz = toZ - fromZ;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Whole degrees 0 to 359 for the heading text.</summary>
        public static int Degrees(float heading) => (int)Math.Round(Wrap360(heading), MidpointRounding.AwayFromZero) % 360;

        /// <summary>The nearest of eight compass points (English letters; the plugin localizes them).</summary>
        public static string Point(float heading) => Points[(int)Math.Round(Wrap360(heading) / 45f, MidpointRounding.AwayFromZero) % 8];

        public static string DistanceText(float metres)
        {
            if (!(metres >= 0f) || float.IsInfinity(metres)) return "";
            return metres < 1000f
                ? Math.Round(metres, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + " m"
                : (metres / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " km";
        }

        /// <summary>A bearing on a strip of <paramref name="width"/> pixels showing <paramref name="stripDegrees"/> degrees around the heading.</summary>
        public static StripPosition OnStrip(float bearing, float heading, int stripDegrees, int width)
        {
            var span = Math.Max(1, stripDegrees);
            var half = span / 2f;
            var delta = Delta(bearing, heading);
            var parked = Math.Abs(delta) > half;
            var clamped = Math.Max(-half, Math.Min(half, delta));
            return new StripPosition(clamped / span * width, parked);
        }

        /// <summary>Ticks every 15 degrees inside the view, left to right; points at multiples of 45, numbers between.</summary>
        public static IEnumerable<StripTick> Ticks(float heading, int stripDegrees, int width = 0)
        {
            var span = Math.Max(1, stripDegrees);
            var half = span / 2f;
            var first = (int)Math.Ceiling((heading - half) / TickStep) * TickStep;
            for (var d = first; d <= heading + half + 1e-4f; d += TickStep)
            {
                var degrees = ((d % 360) + 360) % 360;
                var label = degrees % 45 == 0 ? Points[degrees / 45] : degrees.ToString(CultureInfo.InvariantCulture);
                yield return new StripTick(degrees, label, (d - heading) / span * width);
            }
        }

        /// <summary>Tick marks every 5 degrees inside the view, left to right.</summary>
        public static IEnumerable<StripMark> Marks(float heading, int stripDegrees, int width)
        {
            var span = Math.Max(1, stripDegrees);
            var half = span / 2f;
            var first = (int)Math.Ceiling((heading - half) / MarkStep) * MarkStep;
            for (var d = first; d <= heading + half + 1e-4f; d += MarkStep)
            {
                var degrees = ((d % 360) + 360) % 360;
                var kind = degrees % 45 == 0 ? StripMarkKind.Point : degrees % TickStep == 0 ? StripMarkKind.Number : StripMarkKind.Minor;
                yield return new StripMark(degrees, kind, (d - heading) / span * width);
            }
        }

        /// <summary>Markers nearest first, at most <paramref name="max"/>, within <paramref name="maxDistance"/> meters (0 is no limit).</summary>
        public static IReadOnlyList<MarkerPin> Nearest(IEnumerable<MarkerPin> pins, float x, float z, int max, float maxDistance) =>
            (pins ?? Enumerable.Empty<MarkerPin>())
                .Select(p => (Pin: p, Distance: Distance(x, z, p.X, p.Z)))
                .Where(p => maxDistance <= 0f || p.Distance <= maxDistance)
                .OrderBy(p => p.Distance).ThenBy(p => p.Pin.Id, StringComparer.Ordinal)
                .Take(Math.Max(0, max))
                .Select(p => p.Pin).ToList();

        private static float Delta(float bearing, float heading)
        {
            var d = Wrap360(bearing - heading);
            return d >= 180f ? d - 360f : d;
        }

        private static float Wrap360(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) return 0f;
            var d = degrees % 360f;
            return d < 0f ? d + 360f : d;
        }
    }
}
