// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Magpie.Items
{
    /// <summary>
    /// Item identity rule shaped like the proposed Crucible helper CS5 (design section 6 and 8.2): two items are
    /// "identical" when their full saved identity, without count, position and operational flags, is equal. A null key
    /// means the identity is unknown; an unknown identity never equals anything, including itself. When CS5 lands in
    /// Crucible, Magpie swaps this implementation for the shared one without changing its callers.
    /// </summary>
    public interface IItemIdentityKey<TItem>
    {
        /// <summary>Canonical identity string, compared ordinally; null when the item has no reliable identity.</summary>
        string? Key(TItem? item);

        /// <summary>True only when both items have a key and the keys are equal.</summary>
        bool Same(TItem? left, TItem? right);
    }
}
