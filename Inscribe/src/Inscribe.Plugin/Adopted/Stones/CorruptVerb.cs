// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// <c>corrupt</c> (the Serpent Rune, confirm-gated): one outcome drawn by weight from the rune's <c>outcomes</c>
    /// table, carried out on the copy, then the item is sealed for good (<c>ecf_sealed = serpent</c>): no rune works
    /// on it again. An outcome that cannot be carried out falls back to <c>seal_only</c>, so once the checks pass it
    /// always succeeds.
    /// <para>
    /// The outcome is drawn in the dry run and that dry run is what commits; with the Dialog confirm mode the Yes
    /// re-runs the pipeline and so draws afresh (the player never saw the first draw). Local client only.
    /// </para>
    /// </summary>
    internal sealed class CorruptVerb : IStoneVerb
    {
        public StoneResult Run(StoneJob job)
        {
            RollContext context = job.RollContext();
            CorruptOutcome outcome = Draw(job.Def!.Outcomes, context);
            StoneResult? result = SerpentOutcomes.Carry(job, outcome, context);
            return result ?? SerpentOutcomes.SealOnly(job);
        }

        // Weights are relative; a table with nothing above 0 is disabled at load, and falls back to seal_only here.
        private static CorruptOutcome Draw(IReadOnlyList<CorruptWeight> table, RollContext context)
        {
            List<float> weights = new List<float>(table.Count);
            for (int i = 0; i < table.Count; i++)
            {
                weights.Add(table[i].Weight);
            }
            int index = RollMath.PickWeighted(weights, context.Random);
            return index < 0 ? CorruptOutcome.SealOnly : table[index].Outcome;
        }
    }

    /// <summary>
    /// The three Serpent outcomes: sealed as it is, one inscription past the cap, or a chaotic reroll. Each returns the
    /// sealed success, or null when it cannot be carried out and the caller falls back to <see cref="SealOnly"/>.
    /// </summary>
    internal static class SerpentOutcomes
    {
        public static StoneResult? Carry(StoneJob job, CorruptOutcome outcome, RollContext context)
        {
            switch (outcome)
            {
                case CorruptOutcome.AddInscription:
                    return AddAffix(job, context, job.Def!.Overflow);
                case CorruptOutcome.ChaoticReroll:
                    return Chaotic(job, context);
                default:
                    return null;
            }
        }

        public static StoneResult SealOnly(StoneJob job) =>
            StoneResult.Success(Sealed(job.State), "corrupt_seal", job.ItemName);

        // One affix by the normal roll; it may take the item past its rarity's maximum, and past its prefix or suffix
        // limit, by `overflow` (default 1; classes-and-tiers.md section 6). Never on the base rarity (an owner's
        // applies_to): a Normal item holds no affixes, so that falls back to seal_only.
        private static StoneResult? AddAffix(StoneJob job, RollContext context, int overflow)
        {
            if (job.Rarity!.IsBase || job.State.AffixCount >= job.Rarity.MaxAffixes + overflow)
            {
                return null;
            }
            context.LimitOverflow = overflow;
            RollOutcome rolled = ItemRoller.AddAffixes(job.State, 1, context);
            if (!rolled.Success)
            {
                return null;
            }
            string added = rolled.State!.AffixCount == 0 ? "" : rolled.State!.Affixes[rolled.State!.AffixCount - 1].Id;
            return StoneResult.Success(Sealed(rolled.State!), "corrupt_add", job.ItemName, StoneNames.Affix(job.Rules, added));
        }

        // Every affix out, rolled again at any tier the affix defines; nothing rolled = seal only.
        private static StoneResult? Chaotic(StoneJob job, RollContext context)
        {
            RollOutcome rolled = ItemRoller.RollChaotic(job.State, job.Rarity!, context);
            return rolled.Success ? StoneResult.Success(Sealed(rolled.State!), "corrupt_chaos", job.ItemName) : null;
        }

        private static ItemState Sealed(ItemState state) => state.ToBuilder().Seal(ItemKeys.SealedSerpent).Build();
    }
}
