// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Burrow
{
    /// <summary>
    /// The game saves an item's durability as whole hundredths, <c>(int)(durability * 100f)</c>, and loads them back as
    /// <c>hundredths * 0.01f</c>. For about a fifth of the values that float lands just under the hundredth and saves one
    /// lower (38009 loads as 380.08997 and saves as 38008). A payment image loaded and saved again must keep every item it
    /// did not pay from byte for byte, or applying it never matches (soak 2026-10-11: ImageNotApplied, then
    /// InventoryImageConflict on every retry).
    /// </summary>
    public static class SavedDurability
    {
        /// <summary>A loaded durability moved to the middle of its hundredth, so it saves back to the same hundredth.</summary>
        public static float Exact(float loaded) => ((float)Math.Round(loaded * 100f) + 0.5f) * 0.01f;
    }
}
