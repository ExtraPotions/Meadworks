// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Solstice
{
    /// <summary>
    /// Runs one per-frame tick. A throw is reported at most once per <see cref="WindowSeconds"/> and does not leave the tick.
    /// </summary>
    public static class TickGuard
    {
        public const double WindowSeconds = 60;

        /// <summary>True when this failure should be written. The next true result is <see cref="WindowSeconds"/> later.</summary>
        public static bool Report(ref double nextReportAt, double nowSeconds)
        {
            if (nowSeconds < nextReportAt) return false;
            nextReportAt = nowSeconds + WindowSeconds;
            return true;
        }

        public static void Run(Action tick, ref double nextReportAt, double nowSeconds, Action<Exception> report)
        {
            if (tick == null) return;
            try
            {
                tick();
            }
            catch (Exception error)
            {
                if (Report(ref nextReportAt, nowSeconds)) report?.Invoke(error);
            }
        }
    }
}
