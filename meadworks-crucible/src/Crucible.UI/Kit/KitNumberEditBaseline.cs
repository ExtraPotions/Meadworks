// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    // Remember what the owner last displayed, not a remote value received while the user is editing.
    internal sealed class KitNumberEditBaseline
    {
        string shown = "";
        internal bool Observe(string value, bool focused)
        {
            if (focused) return false;
            shown = value ?? "";
            return true;
        }
        internal bool ShouldCommit(bool commit, string value)
            => commit && !string.Equals(shown, value ?? "", StringComparison.Ordinal);
    }
}
