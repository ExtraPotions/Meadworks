// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the clock and weather icons are drawn from simple shapes (circles, bars, polygons) in code, so the
// HUD does not depend on glyphs the game's font may lack and ships no image. Clean room: no TimeWeatherHud art or code.
using System;

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>The small marks of the status line: the sun or moon beside the time and one per weather.</summary>
    public enum HudIcon { Sun, Moon, Cloud, Rain, Storm, Snow, Mist, Ash, Boss, Indoors }

    /// <summary>Alpha masks of the HUD icons, built from shapes in a unit square (y grows downwards).</summary>
    public static class HudIcons
    {
        const int Samples = 4;

        /// <summary>The clock's mark: the moon at night, else the sun.</summary>
        public static HudIcon Clock(bool night) => night ? HudIcon.Moon : HudIcon.Sun;

        /// <summary>The weather's mark; a clear sky at night shows the clock's moon instead of the sun.</summary>
        public static HudIcon For(WeatherCategory category, bool night)
        {
            var icon = For(category);
            return icon == HudIcon.Sun ? Clock(night) : icon;
        }

        public static HudIcon For(WeatherCategory category)
        {
            switch (category)
            {
                case WeatherCategory.Clear: return HudIcon.Sun;
                case WeatherCategory.Mist: return HudIcon.Mist;
                case WeatherCategory.Rain: return HudIcon.Rain;
                case WeatherCategory.Thunderstorm: return HudIcon.Storm;
                case WeatherCategory.Snow: case WeatherCategory.Blizzard: return HudIcon.Snow;
                case WeatherCategory.AshRain: return HudIcon.Ash;
                case WeatherCategory.Boss: return HudIcon.Boss;
                case WeatherCategory.Indoors: return HudIcon.Indoors;
                default: return HudIcon.Cloud;
            }
        }

        /// <summary>Coverage 0 to 255 for each pixel of a <paramref name="size"/> by <paramref name="size"/> icon, row by row from the top.</summary>
        public static byte[] Mask(HudIcon icon, int size) => Sampled(size, (x, y) => Inside(icon, x, y));

        /// <summary>The Markers map's player marker: an arrow with its tip up and a notched base, mirror symmetric.</summary>
        public static byte[] PlayerArrow(int size) => Sampled(size, (x, y) => Polygon(x, y, Arrow));

        static readonly double[] Arrow = { .5, .06, .88, .9, .5, .7, .12, .9 };

        static byte[] Sampled(int size, Func<double, double, bool> inside)
        {
            if (size <= 0) return Array.Empty<byte>();
            var mask = new byte[size * size];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var hit = 0;
                    for (var sy = 0; sy < Samples; sy++)
                    {
                        for (var sx = 0; sx < Samples; sx++)
                        {
                            var x = (px + (sx + 0.5) / Samples) / size;
                            var y = (py + (sy + 0.5) / Samples) / size;
                            if (inside(x, y)) hit++;
                        }
                    }
                    mask[py * size + px] = (byte)(hit * 255 / (Samples * Samples));
                }
            }
            return mask;
        }

        static bool Inside(HudIcon icon, double x, double y)
        {
            switch (icon)
            {
                case HudIcon.Sun: return Disc(x, y, .5, .5, .2) || Rays(x, y, .5, .5, .31, .47, .055, 8);
                case HudIcon.Moon: return Disc(x, y, .5, .5, .38) && !Disc(x, y, .64, .4, .31);
                case HudIcon.Cloud: return Cloud(x, y, .5, .5, 1);
                case HudIcon.Rain:
                    return Cloud(x, y, .5, .36, .8) || Bar(x, y, .3, .66, .3, .86, .05) || Bar(x, y, .5, .7, .5, .9, .05) || Bar(x, y, .7, .66, .7, .86, .05);
                case HudIcon.Storm:
                    return Cloud(x, y, .5, .34, .8) || Polygon(x, y, Bolt);
                case HudIcon.Snow:
                    return Rays(x, y, .5, .5, 0, .44, .05, 6) || Disc(x, y, .5, .5, .08);
                case HudIcon.Mist:
                    return Bar(x, y, .14, .28, .86, .28, .055) || Bar(x, y, .14, .5, .86, .5, .055) || Bar(x, y, .14, .72, .86, .72, .055);
                case HudIcon.Ash:
                    return Cloud(x, y, .5, .36, .8) || Disc(x, y, .3, .8, .065) || Disc(x, y, .5, .9, .065) || Disc(x, y, .7, .8, .065);
                case HudIcon.Boss:
                    return Bar(x, y, .5, .1, .5, .6, .065) || Disc(x, y, .5, .82, .09);
                case HudIcon.Indoors:
                    return (Polygon(x, y, Roof) || (x >= .2 && x <= .8 && y >= .48 && y <= .9)) && !(x >= .43 && x <= .57 && y >= .62 && y <= .9);
                default: return false;
            }
        }

        static readonly double[] Bolt = { .56, .58, .4, .8, .5, .8, .44, .98, .66, .72, .54, .72, .62, .58 };

        static readonly double[] Roof = { .5, .1, .94, .5, .06, .5 };

        static bool Disc(double x, double y, double cx, double cy, double r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

        /// <summary>A rounded bar between two points.</summary>
        static bool Bar(double x, double y, double ax, double ay, double bx, double by, double halfWidth)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var length2 = dx * dx + dy * dy;
            var t = length2 <= 0 ? 0 : Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / length2));
            var qx = ax + t * dx;
            var qy = ay + t * dy;
            return (x - qx) * (x - qx) + (y - qy) * (y - qy) <= halfWidth * halfWidth;
        }

        /// <summary>Spokes from <paramref name="inner"/> to <paramref name="outer"/> radius, evenly spread round the center, the first pointing up.</summary>
        static bool Rays(double x, double y, double cx, double cy, double inner, double outer, double halfWidth, int count)
        {
            var dx = x - cx;
            var dy = y - cy;
            var r = Math.Sqrt(dx * dx + dy * dy);
            if (r < inner || r > outer) return false;
            var step = 2 * Math.PI / count;
            var angle = Math.Atan2(dx, -dy);
            var nearest = Math.Round(angle / step) * step;
            return r * Math.Abs(Math.Sin(angle - nearest)) <= halfWidth;
        }

        /// <summary>A cloud made of two side lobes, a taller middle lobe and a flat base, scaled <paramref name="s"/> round (cx, cy).</summary>
        static bool Cloud(double x, double y, double cx, double cy, double s)
        {
            var u = .5 + (x - cx) / s;
            var v = .5 + (y - cy) / s;
            return Disc(u, v, .3, .6, .17) || Disc(u, v, .7, .6, .17) || Disc(u, v, .5, .46, .22) || (u >= .3 && u <= .7 && v >= .5 && v <= .77);
        }

        /// <summary>Even-odd point in polygon; points are x, y pairs.</summary>
        static bool Polygon(double x, double y, double[] points)
        {
            var inside = false;
            var n = points.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var xi = points[2 * i];
                var yi = points[2 * i + 1];
                var xj = points[2 * j];
                var yj = points[2 * j + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }
    }
}
