// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: cheat values (design 4.11 and 5).
using System;

namespace ExtraPotions.Saga.Config
{
    /// <summary>
    /// A blocked cheat evaluates to its safe value. Every Saga cheat is a toggle that defaults to off; a number that tunes a
    /// cheat is a ServerAuthoritative setting that acts only while its cheat is allowed and on.
    /// </summary>
    public static class SagaCheats
    {
        public static T Effective<T>(SagaSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }

        /// <summary>The tuning number in effect: its normalized value while the cheat is allowed and on, else its default.</summary>
        public static T Gated<T>(SagaSetting<bool> cheat, bool locked, bool enabled, SagaSetting<T> number, T value)
        {
            if (number == null) throw new ArgumentNullException(nameof(number));
            if (number.Scope != SettingScope.ServerAuthoritative) throw new ArgumentException(number.Id + " must be a ServerAuthoritative number to tune a cheat.", nameof(number));
            return Effective(cheat, locked, enabled) ? number.Normalize(value) : number.Default;
        }

        /// <summary>The forced mastery level of every power (PP Debug Level Override), or null while the cheat is off or blocked.</summary>
        public static int? MasteryOverride(bool locked, bool enabled, int level) =>
            Effective(SagaSettings.Cheats.MasteryOverride, locked, enabled) ? SagaSettings.Cheats.MasteryOverrideLevel.Normalize(level) : (int?)null;
    }
}
