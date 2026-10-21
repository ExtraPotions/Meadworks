// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: cheat values (design 4.6, with the recorded toggle deviation).
using System;

namespace ExtraPotions.Keel.Config
{
    /// <summary>
    /// A blocked cheat evaluates to its safe value. Every Keel cheat is a toggle that defaults to off; the ship list that
    /// says which ships it covers is a ServerAuthoritative setting that acts only while its cheat is allowed and on.
    /// </summary>
    public static class KeelCheats
    {
        public static T Effective<T>(KeelSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }

        /// <summary>The ships a cheat covers now: its list while the cheat is allowed and on, else no ship.</summary>
        public static ShipList ShipsInEffect(KeelSetting<bool> cheat, bool locked, bool enabled, KeelSetting<string> list, string value)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (list.Scope != SettingScope.ServerAuthoritative) throw new ArgumentException(list.Id + " must be a ServerAuthoritative ship list.", nameof(list));
            return Effective(cheat, locked, enabled) ? ShipList.Parse(list.Normalize(value)) : ShipList.Empty;
        }
    }
}