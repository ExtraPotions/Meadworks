// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: outdoors status, warm-clothes overheat, and the snow and frozen-water facts.
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Effects
{
    /// <summary>Player-facing climate decisions. Snow cover and frozen water are facts for other mods. Solstice does not spawn meshes or floes, and client visuals do not write piece snow into the world.</summary>
    public static class ClimateRules
    {
        /// <summary>Stamina and Eitr regen while overheated from warm clothes.</summary>
        public const float OverheatRegen = 0.8f;

        public const int WaterFreezesFromDay = 6;
        public const int WaterFreezesUntilDay = 9;

        /// <summary>The season status applies outdoors, and not in the Ashlands or the Deep North.</summary>
        public static bool StatusApplies(bool enabled, bool interior, bool ashlandsOrDeepNorth) =>
            enabled && !interior && !ashlandsOrDeepNorth;

        /// <summary>Summer (or any season flagged for it), two or more frost-resistant clothes, no cooling food, and not cold or sheltered.</summary>
        public static bool Overheating(bool settingOn, bool seasonOverheats, bool shelteredOrCold, int warmClothes, bool coolingFood) =>
            settingOn && seasonOverheats && !shelteredOrCold && !coolingFood && warmClothes > 1;

        /// <summary>0 outside winter, in a protected place, or beside heat. Otherwise the winter day divided by the season length, at most 1.</summary>
        public static float SnowCover(Season season, int dayInSeason, int daysInSeason, bool placeProtected, bool heatProtected)
        {
            if (season != Season.Winter || placeProtected || heatProtected || daysInSeason < 1 || dayInSeason < 1) return 0f;
            float cover = dayInSeason / (float)daysInSeason;
            return cover > 1f ? 1f : cover;
        }

        /// <summary>Frozen-water fact for winter days from <see cref="WaterFreezesFromDay"/> through <see cref="WaterFreezesUntilDay"/>, inclusive.</summary>
        public static bool WaterFrozen(Season season, int dayInSeason) =>
            season == Season.Winter && dayInSeason >= WaterFreezesFromDay && dayInSeason <= WaterFreezesUntilDay;
    }
}
