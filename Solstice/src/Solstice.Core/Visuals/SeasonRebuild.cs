// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. A new world can reuse the season, the day and the translated words on a new object.
using System;

namespace ExtraPotions.Solstice.Visuals
{
    /// <summary>
    /// Whether a plate label or a status effect must be written again.
    /// A destroyed object is a new object even when the words match the previous world.
    /// </summary>
    public static class SeasonRebuild
    {
        public static bool Text(bool sameObject, string? text, string? cached, int day, int cachedDay, int length, int cachedLength)
        {
            if (!sameObject) return true;
            if (!string.Equals(text, cached, StringComparison.Ordinal)) return true;
            return day != cachedDay || length != cachedLength;
        }
    }
}
