// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Comfort square in the minimap column (user decisions 2026-10-12 14:54 and 15:27).
using System;
using System.Globalization;

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>What Cairn's Comfort square right of the minimap shows: its place in the column and its value.</summary>
    public static class ColumnSquares
    {
        /// <summary>Column rank (lower sits higher): Mantle's armor 100, carry 300 and world tier 400 above; Keel's wind 600 and Solstice's season 700 below.</summary>
        public const int ComfortRank = 500;
        public const string ComfortId = "cairn_comfort";

        /// <summary>The comfort level, and on a second line the Rested time left (m:ss) while it runs.</summary>
        public static string Comfort(int level, float restedSeconds)
        {
            var rested = Rested(restedSeconds);
            var comfort = Math.Max(0, level).ToString(CultureInfo.InvariantCulture);
            return rested.Length == 0 ? comfort : comfort + "\n" + rested;
        }

        /// <summary>Time left as m:ss, rounded up so it reads 0:01 until it ends; empty when none is left.</summary>
        public static string Rested(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return "";
            var whole = (int)Math.Ceiling(seconds);
            return (whole / 60).ToString(CultureInfo.InvariantCulture) + ":" + (whole % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
