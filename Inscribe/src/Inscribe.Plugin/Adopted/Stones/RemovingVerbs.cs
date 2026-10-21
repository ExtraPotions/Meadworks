// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Affixes;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// <c>strip</c> (the Cleansing Rune, confirm-gated): every affix goes, dormant ones included, and the item becomes
    /// the base rarity. Kept: the vanilla upgrade level, durability, crafter and variant (none of which live in our
    /// keys), and unreadable segments (item-data.md section 4: never destroyed, IMP-71). Sealed is permanent: the
    /// pipeline has already refused a sealed item.
    /// </summary>
    internal sealed class StripVerb : IStoneVerb
    {
        public StoneResult Run(StoneJob job)
        {
            ItemState stripped = job.State.ToBuilder().ClearAffixes().SetRarity(null).Build();
            return StoneResult.Success(stripped, "stripped", job.ItemName);
        }
    }
}
