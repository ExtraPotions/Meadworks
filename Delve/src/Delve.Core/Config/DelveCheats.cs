// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Regenerate cheat (user decision 2026-10-08, a change from design section 5, which had none).
using System;

namespace ExtraPotions.Delve.Config
{
    /// <summary>Why an admin's regenerate-without-waiting request is refused, or Allowed.</summary>
    public enum RegenerateVerdict { Allowed, NotAdmin, CheatOff, CheatLocked }

    /// <summary>
    /// A blocked cheat evaluates to its safe value (off). Regenerating a site without waiting (design B9) needs the caller
    /// to be an admin (or the single-player or host player) and the Regenerate cheat to be on and allowed by the Mischief
    /// gate. The no-player and build clearance guards still apply.
    /// </summary>
    public static class DelveCheats
    {
        public static T Effective<T>(DelveSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }

        public static RegenerateVerdict Regenerate(bool isAdmin, bool locked, bool cheatValue)
        {
            if (!isAdmin) return RegenerateVerdict.NotAdmin;
            if (locked) return RegenerateVerdict.CheatLocked;
            return Effective(DelveSettings.Cheats.RegenerateWithoutWaiting, false, cheatValue) ? RegenerateVerdict.Allowed : RegenerateVerdict.CheatOff;
        }
    }
}
