// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Rolling
{
    /// <summary>
    /// The one affix-drawing procedure of the mod (classes-and-tiers.md sections 5 and 6): every rune that adds affixes,
    /// pre-rolled drops and <c>ecraft roll</c> call these. Every method is pure: it takes a state, returns a new state or
    /// a failure, and never writes the item; the caller commits with <see cref="ItemState.Write"/>. Rolls run on the
    /// peer that owns the item, under the synced rules.
    /// <para>
    /// Shared rules for every method: candidates are enabled affixes with weight above 0 that list the item's class in
    /// <c>classes.best</c> or <c>classes.allowed</c>, pass <c>requires</c> (<c>Items.ItemClasses.Satisfies</c>), are not
    /// on the item (dormant copies included), share no exclusion group with an affix on the item, keep the item within
    /// its rarity's prefix or suffix limit for their kind, and have an eligible tier: unlocked at the item level or
    /// below, below the closed top on an allowed class, among the rune floor's best (<see cref="TierEligibility"/>).
    /// Draw: affix by <c>weight</c>, then tier by tier <c>weight</c>, then value uniformly in [min, max] rounded to the
    /// tier's decimals, times the class's <c>damage_scale</c> when <c>scaled</c> (flags store 1). Chaotic rolls use
    /// every tier the affix defines, uniformly.
    /// </para>
    /// </summary>
    public static class ItemRoller
    {
        /// <summary>
        /// Rolls an item fresh at <paramref name="rarity"/> (pre-rolled drops, <c>ecraft roll</c>, the Recasting Rune): every affix is
        /// replaced; the count is drawn in [min, max] of the rarity (uniform, or <c>rolling.count_weights</c>). Sets
        /// the rarity. Fails with <see cref="RollFailure.NoEligibleAffix"/> when the pool cannot reach the rarity's
        /// minimum (drops then fall back to a lower rarity themselves).
        /// </summary>
        public static RollOutcome RollFresh(ItemState current, RarityDef rarity, RollContext context)
        {
            return RollLog.Report("fresh " + rarity.Id, current, RollOps.RollFresh(current, rarity, context), context);
        }

        /// <summary>
        /// Adds exactly <paramref name="count"/> affixes at the end of the list (the Serpent's
        /// add). Does not change the rarity and does not check the rarity's maximum count (the caller decides whether
        /// the item has room); the prefix and suffix limits apply, passed by <see cref="RollContext.LimitOverflow"/>. All or nothing: fails with <see cref="RollFailure.NoEligibleAffix"/> when fewer candidates
        /// exist than needed.
        /// </summary>
        public static RollOutcome AddAffixes(ItemState current, int count, RollContext context)
        {
            return RollLog.Report("add " + count, current, RollOps.AddAffixes(current, count, context), context);
        }

        /// <summary>
        /// Promotes one rarity up to <paramref name="to"/> (the Ascension Rune; rarity.md section 3): every affix
        /// kept, dormant included; adds <c>max(to.min - count, rolling.promote_adds_at_least)</c> capped at
        /// <c>to.max</c>. All or nothing: <see cref="RollFailure.NoEligibleAffix"/> when the pool cannot supply them.
        /// Sets the rarity.
        /// </summary>
        public static RollOutcome Promote(ItemState current, RarityDef to, RollContext context)
        {
            return RollLog.Report("promote " + to.Id, current, RollOps.Promote(current, to, context), context);
        }

        /// <summary>
        /// The Serpent Rune's chaotic reroll: every affix goes; the count is drawn in the rarity's range and every
        /// affix rolls at any tier it defines, uniformly, ignoring the item level, the closed tiers and the floor.
        /// The class pool and the prefix and suffix limits still apply. Fills what the pool can; fails only when it can fill nothing.
        /// </summary>
        public static RollOutcome RollChaotic(ItemState current, RarityDef rarity, RollContext context)
        {
            return RollLog.Report("chaotic " + rarity.Id, current, CorruptOps.Chaotic(current, rarity, context), context);
        }
    }
}
