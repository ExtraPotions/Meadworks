// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// The API's magic-base filters (api.md section 5): vetoes another mod registers. <see cref="ItemClasses.IsMagicBase"/>
    /// asks them last, so a false keeps an item from ever becoming magic wherever that question decides: drops (the gear
    /// pool asks with the prefab's item data), runes, commands and the API. A filter should decide from the item type,
    /// not the instance. A filter that throws counts as no veto. Main thread only.
    /// </summary>
    internal static class MagicBaseFilters
    {
        public static readonly Callbacks<Func<ItemDrop.ItemData, bool>> Filters =
            new Callbacks<Func<ItemDrop.ItemData, bool>>("magic-base filter");

        /// <summary>False when a filter vetoes the item; true with no filters (one array length check).</summary>
        public static bool Allows(ItemDrop.ItemData item)
        {
            foreach (Callbacks<Func<ItemDrop.ItemData, bool>>.Entry filter in Filters.Snapshot)
            {
                try
                {
                    if (!filter.Callback(item))
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Filters.Failed(filter, e);
                }
            }
            return true;
        }
    }
}
