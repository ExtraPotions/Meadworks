// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Wayfare's modes (Public, Private, Admin; GPL-3.0-or-later), chosen by the user for U2 on 2026-10-08.
// PotalMap's Guild mode was left out (no guild integration); its old number 3 is never reused.
namespace ExtraPotions.Threshold.Access
{
    /// <summary>Who may target a portal. Stored as its number on the portal ZDO; never renumber and never reuse 3 (the
    /// removed Guild mode): an unknown stored number reads back as Public.</summary>
    public enum AccessMode
    {
        Public = 0,
        Private = 1,
        Admin = 2,
    }
}
