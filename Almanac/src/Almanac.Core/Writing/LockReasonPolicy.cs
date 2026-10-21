// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Almanac.Writing
{
    /// <summary>
    /// Which reason a locked setting shows. A Meadworks product's blocked-options layer also stamps a read-only tag on an entry the
    /// server controls, so a tag alone does not say who locked it; the sync system's own reason does.
    /// </summary>
    public static class LockReasonPolicy
    {
        /// <summary>The reason for an entry that carries a read-only tag: the server's own reason when one is known, else the tag text.</summary>
        public static string ForReadOnlyTag(bool entrySynced, bool known, bool editable, string? factsReason, string tagText) =>
            entrySynced && known && !editable && !string.IsNullOrEmpty(factsReason) ? factsReason! : tagText;
    }
}
