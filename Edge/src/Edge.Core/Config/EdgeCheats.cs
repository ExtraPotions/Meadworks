// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: cheat values (design 4.6 and 5).
using System;

namespace ExtraPotions.Edge.Config
{
    /// <summary>
    /// A blocked cheat evaluates to its safe value. Every Edge cheat is a toggle that defaults to off; a number that tunes a
    /// cheat is a ServerAuthoritative setting that acts only while its cheat is allowed and on.
    /// </summary>
    public static class EdgeCheats
    {
        public static T Effective<T>(EdgeSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }

        /// <summary>The tuning number in effect: its normalized value while the cheat is allowed and on, else its default.</summary>
        public static T Gated<T>(EdgeSetting<bool> cheat, bool locked, bool enabled, EdgeSetting<T> number, T value)
        {
            if (number == null) throw new ArgumentNullException(nameof(number));
            if (number.Scope != SettingScope.ServerAuthoritative) throw new ArgumentException(number.Id + " must be a ServerAuthoritative number to tune a cheat.", nameof(number));
            return Effective(cheat, locked, enabled) ? number.Normalize(value) : number.Default;
        }

        /// <summary>No Secondary Cooldowns in effect.</summary>
        public static bool NoSecondaryCooldowns(bool locked, bool enabled) => Effective(EdgeSettings.Cheats.NoSecondaryCooldowns, locked, enabled);
    }
}
