// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Magpie.Items
{
    /// <summary>Magpie's copy of the CS5 identity rule over portable <see cref="ItemIdentity"/> facts.</summary>
    public sealed class ItemIdentityKey : IItemIdentityKey<ItemIdentity>
    {
        public string? Key(ItemIdentity? item) => item?.CanonicalKey();

        public bool Same(ItemIdentity? left, ItemIdentity? right)
        {
            var key = Key(left);
            return key != null && string.Equals(key, Key(right), StringComparison.Ordinal);
        }
    }
}
