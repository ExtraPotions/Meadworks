// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Day light for the winter clear skies this client owns. Mountain and Deep North weather is not in this list.
using System;

namespace ExtraPotions.Solstice.Weather
{
    /// <summary>Modest day-light scale for lowland clear winter, so open snow and the player are not washed out at midday.</summary>
    public static class SeasonSky
    {
        public const float ClearDaySun = 0.84f;
        public const float ClearDayAmbient = 0.88f;
        public const float ClearDayIntensity = 0.82f;
        public const float ClearDayFogSun = 0.90f;

        public static bool DimsLowlandClear(string? name) =>
            string.Equals(name, "Clear Winter", StringComparison.Ordinal)
            || string.Equals(name, "Heath clear Winter", StringComparison.Ordinal);

        public static float Scale(float value, float factor)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return value;
            if (float.IsNaN(factor) || factor < 0f) return value;
            return value * factor;
        }
    }
}
