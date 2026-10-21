// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: behavior from TimeWeatherHud 1.1.0's README (no license; no code or art used).
using System;
using System.Globalization;

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>Time of day text from the game's day fraction (0 is midnight), 24-hour or 12-hour (design C1).</summary>
    public static class Clock
    {
        public static (int Hour, int Minute) Parts(float dayFraction)
        {
            var f = float.IsNaN(dayFraction) || float.IsInfinity(dayFraction) ? 0d : dayFraction - Math.Floor(dayFraction);
            var hours = f * 24d;
            var hour = (int)Math.Floor(hours);
            var minute = (int)Math.Floor((hours - hour) * 60d);
            return (Math.Min(23, Math.Max(0, hour)), Math.Min(59, Math.Max(0, minute)));
        }

        public static string Text(float dayFraction, bool use24Hour)
        {
            var (hour, minute) = Parts(dayFraction);
            if (use24Hour) return hour.ToString("00", CultureInfo.InvariantCulture) + ":" + minute.ToString("00", CultureInfo.InvariantCulture);
            var twelve = hour % 12 == 0 ? 12 : hour % 12;
            return twelve.ToString(CultureInfo.InvariantCulture) + ":" + minute.ToString("00", CultureInfo.InvariantCulture) + (hour < 12 ? " AM" : " PM");
        }
    }
}
