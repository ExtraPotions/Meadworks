// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Rolling;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// <c>reroll</c> (the Recasting Rune): the item is rolled completely fresh at its own rarity
    /// (<see cref="ItemRoller.RollFresh"/>, user decision 2026-10-05): every affix, dormant ones included, goes, and a new
    /// count is drawn in the rarity's range - 1 or 2 on a Magic item - at the item's own tiers and the rune's floor. The
    /// rarity stays. Not confirm-gated by default: it is the rune a player uses again and again.
    /// Never on the base rarity (an owner's <c>applies_to</c>): a Normal item holds no affixes, so it refuses.
    /// </summary>
    internal sealed class RerollVerb : IStoneVerb
    {
        public StoneResult Run(StoneJob job)
        {
            if (job.Rarity!.IsBase)
            {
                return StoneResult.Refuse("wrong_rarity", job.StoneName, StoneNames.Rarity(job.Rarity));
            }
            RollOutcome outcome = ItemRoller.RollFresh(job.State, job.Rarity, job.RollContext());
            if (!outcome.Success)
            {
                return StoneResult.Refuse("no_eligible_affix");
            }
            return StoneResult.Success(outcome.State!, "rerolled", job.ItemName);
        }
    }
}
