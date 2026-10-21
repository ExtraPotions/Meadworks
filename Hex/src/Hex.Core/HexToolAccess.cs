// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: whether one routed Hex tool may run. A stored grant covers only an admins-only refusal.
namespace ExtraPotions.Hex
{
    public static class HexToolAccess
    {
        public static bool Allows(bool fullAdmin, bool granted, bool mischiefAllowed, bool refusedBecauseNotAdmin, bool adminRequired)
        {
            if (adminRequired && !fullAdmin && !granted) return false;
            if (mischiefAllowed) return true;
            return granted && refusedBecauseNotAdmin;
        }
    }
}
