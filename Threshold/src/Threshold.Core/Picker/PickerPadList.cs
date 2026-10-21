// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the destination picker's gamepad list is rebuilt only when its stamp changes.
using System.Collections.Generic;

namespace ExtraPotions.Threshold.Picker
{
    /// <summary>Whether the picker's gamepad controls are the same list as last time. The stamp is a generation, then
    /// one state per row and per action button (0 missing or hidden, 1 shown but blocked, 2 ready).</summary>
    public static class PickerPadList
    {
        public const int Missing = 0;
        public const int Blocked = 1;
        public const int Ready = 2;

        public static bool Unchanged(IReadOnlyList<int> cached, IReadOnlyList<int> now)
        {
            if (cached == null || now == null || cached.Count != now.Count)
                return false;
            for (int i = 0; i < now.Count; i++)
                if (cached[i] != now[i])
                    return false;
            return true;
        }
    }
}
