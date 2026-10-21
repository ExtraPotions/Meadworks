// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: cheat values (design 4.12 and 5).
using System;
using ExtraPotions.Wildblood.Loot;

namespace ExtraPotions.Wildblood.Config
{
    /// <summary>A blocked cheat evaluates to its safe value; the drop multiplier numbers act only through their cheat.</summary>
    public static class WildbloodCheats
    {
        public static T Effective<T>(WildbloodSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }

        /// <summary>The multipliers in effect: none unless the Drop Multipliers cheat is allowed and on; numbers are clamped.</summary>
        public static DropMultipliers Multipliers(bool locked, bool enabled, float creature, float boss, float trophy)
        {
            if (!Effective(WildbloodSettings.Cheats.DropMultipliers, locked, enabled)) return DropMultipliers.None;
            return new DropMultipliers(
                WildbloodSettings.Cheats.CreatureDropMultiplier.Normalize(creature),
                WildbloodSettings.Cheats.BossDropMultiplier.Normalize(boss),
                WildbloodSettings.Cheats.TrophyDropMultiplier.Normalize(trophy));
        }
    }
}
