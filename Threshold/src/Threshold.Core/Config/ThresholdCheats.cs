// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: cheat values (design 4.3); same rule as Edge.Core.
using System;

namespace ExtraPotions.Threshold.Config
{
    /// <summary>
    /// A blocked cheat evaluates to its safe value. Every Threshold cheat is a toggle that defaults to off; a number that
    /// tunes a cheat is a ServerAuthoritative setting that acts only while its cheat is allowed and on.
    /// </summary>
    public static class ThresholdCheats
    {
        public static T Effective<T>(ThresholdSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }

        /// <summary>The tuning number in effect: its normalized value while the cheat is allowed and on, else its default.</summary>
        public static T Gated<T>(ThresholdSetting<bool> cheat, bool locked, bool enabled, ThresholdSetting<T> number, T value)
        {
            if (number == null) throw new ArgumentNullException(nameof(number));
            if (number.Scope != SettingScope.ServerAuthoritative) throw new ArgumentException(number.Id + " must be a ServerAuthoritative number to tune a cheat.", nameof(number));
            return Effective(cheat, locked, enabled) ? number.Normalize(value) : number.Default;
        }

        /// <summary>Ignore Teleport Restrictions in effect (Q3: Threshold owns carrying anything through portals).</summary>
        public static bool IgnoreTeleportRestrictions(bool locked, bool enabled) => Effective(ThresholdSettings.Cheats.IgnoreTeleportRestrictions, locked, enabled);
    }
}
