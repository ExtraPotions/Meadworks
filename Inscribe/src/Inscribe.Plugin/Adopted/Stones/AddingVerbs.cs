// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; one Ascension Rune
// for every step from Common to Epic (Awakening folded in, 2026-10-19).
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// <c>promote</c> (the Ascension Rune): one rarity up, every affix kept, adds to the new minimum and at least
    /// <c>rolling.promote_adds_at_least</c>, capped at the new maximum: two on a Common item, one more on a two-inscription
    /// Uncommon item (Rare holds three) and one more on a Rare item (Epic holds four), within the new rarity's prefix and
    /// suffix limits, per rarity.md section 3. Never into a source-only rarity (Legendary). A pool that cannot supply them refuses.
    /// </summary>
    internal sealed class PromoteVerb : IStoneVerb
    {
        public StoneResult Run(StoneJob job)
        {
            RarityDef? next = job.Rules.Economy.Next(job.Rarity!);
            if (next == null || next.SourceOnly)
            {
                // An owner listed the top rarity in a promote rune's applies_to: there is nowhere to go. A source-only
                // rarity (Legendary) is never reached by a rune.
                return StoneResult.Refuse("ascension_top", StoneNames.Rarity(job.Rarity!));
            }
            RollOutcome outcome = ItemRoller.Promote(job.State, next, job.RollContext());
            if (!outcome.Success)
            {
                return StoneResult.Refuse("no_eligible_affix");
            }
            return StoneResult.Success(outcome.State!, "promoted", job.ItemName, StoneNames.Rarity(next));
        }
    }
}
