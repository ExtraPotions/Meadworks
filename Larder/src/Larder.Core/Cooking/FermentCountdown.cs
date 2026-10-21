// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original optional hover presentation; no third-party fermenter timer source adopted.
using System;
using System.Globalization;

namespace ExtraPotions.Larder.Cooking
{
    public static class FermentCountdown
    {
        /// <summary>Remaining world seconds, from the native elapsed clock after Cooking's speed scaling.</summary>
        public static string? Format(double scaledElapsed, double duration, double speed)
        {
            if (double.IsNaN(scaledElapsed) || double.IsInfinity(scaledElapsed) || scaledElapsed < 0 ||
                double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0 ||
                double.IsNaN(speed) || double.IsInfinity(speed) || speed <= 0 || scaledElapsed > duration)
                return null;
            double seconds = Math.Ceiling((duration - scaledElapsed) / speed);
            // Keep malformed external values away from integer overflow and a misleading countdown.
            if (seconds > int.MaxValue) return null;
            int remaining = (int)seconds;
            return remaining >= 3600
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", remaining / 3600, remaining / 60 % 60, remaining % 60)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", remaining / 60, remaining % 60);
        }
    }
}
