// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Runes;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Rolling
{
    /// <summary>
    /// The bodies behind <see cref="ItemRoller"/>. Every operation copies the state into a builder, works on the copy
    /// and returns it built, or a failure with nothing changed; nothing here writes an item. Runs on whichever peer
    /// owns the item (the clicking client for a rune, the creature's ZDO owner for a drop) under the synced rules.
    /// </summary>
    internal static class RollOps
    {
        /// <summary>A fresh roll at <paramref name="rarity"/>: every affix is replaced, the count drawn in its range.</summary>
        public static RollOutcome RollFresh(ItemState current, RarityDef rarity, RollContext context)
        {
            return Recast(current, rarity, context, RecastFocus.Unrestricted, RecastSide.Prefix);
        }

        internal static RollOutcome Recast(ItemState current, RarityDef rarity, RollContext context, RecastFocus focus, RecastSide side)
        {
            if ((focus != RecastFocus.Unrestricted && focus != RecastFocus.Offense && focus != RecastFocus.Defense && focus != RecastFocus.Utility)
                || (focus != RecastFocus.Unrestricted && side != RecastSide.Prefix && side != RecastSide.Suffix))
                return Fail(RollFailure.NoEligibleAffix);
            ItemStateBuilder builder = current.ToBuilder();
            builder.ClearAffixes();
            builder.SetRarity(rarity.Id);
            int count = DrawCount(rarity, context);
            int floor = context.TierFloor;
            if (rarity.TierFloor > 0 && (floor <= 0 || rarity.TierFloor < floor))
            {
                context.TierFloor = rarity.TierFloor;   // higher rarities roll stronger values; the stricter floor wins
            }
            try
            {
                AffixDraw draw = new AffixDraw(builder, context);
                if (focus != RecastFocus.Unrestricted)
                {
                    AffixCategory category = focus == RecastFocus.Offense ? AffixCategory.Offense
                        : focus == RecastFocus.Defense ? AffixCategory.Defense : AffixCategory.Utility;
                    AffixKind kind = side == RecastSide.Prefix ? AffixKind.Prefix : AffixKind.Suffix;
                    if (count <= 0 || !draw.TryAdd(category, kind)) return Fail(RollFailure.NoEligibleAffix);
                }
                Fill(draw, count, allOrNothing: false);
            }
            finally
            {
                context.TierFloor = floor;
            }
            return builder.AffixCount >= rarity.MinAffixes ? RollOutcome.Ok(builder.Build()) : Fail(RollFailure.NoEligibleAffix);
        }

        public static RollOutcome AddAffixes(ItemState current, int count, RollContext context)
        {
            ItemStateBuilder builder = current.ToBuilder();
            AffixDraw draw = new AffixDraw(builder, context);
            return Fill(draw, builder.AffixCount + count, allOrNothing: true)
                ? RollOutcome.Ok(builder.Build())
                : Fail(RollFailure.NoEligibleAffix);
        }

        /// <summary>
        /// One rarity up (rarity.md section 3): every affix kept; adds <c>max(new.min - count, at_least)</c> capped at
        /// the new maximum. All or nothing.
        /// </summary>
        public static RollOutcome Promote(ItemState current, RarityDef to, RollContext context)
        {
            ItemStateBuilder builder = current.ToBuilder();
            builder.SetRarity(to.Id);
            int count = builder.AffixCount;
            int atLeast = context.Rules.Economy.Rolling.PromoteAddsAtLeast;
            int added = Math.Min(Math.Max(to.MinAffixes - count, atLeast), Math.Max(to.MaxAffixes - count, 0));
            AffixDraw draw = new AffixDraw(builder, context);
            return Fill(draw, count + added, allOrNothing: true)
                ? RollOutcome.Ok(builder.Build())
                : Fail(RollFailure.NoEligibleAffix);
        }

        /// <summary>The count a fresh roll draws in the rarity's range: uniform, or <c>rolling.count_weights</c>.</summary>
        internal static int DrawCount(RarityDef rarity, RollContext context)
        {
            context.Rules.Economy.Rolling.CountWeights.TryGetValue(rarity.Id, out var weights);
            return RollMath.PickCount(rarity.MinAffixes, rarity.MaxAffixes, weights, context.Random);
        }

        /// <summary>Draws until the item holds <paramref name="target"/> affixes.</summary>
        internal static bool Fill(AffixDraw draw, int target, bool allOrNothing)
        {
            while (draw.Builder.AffixCount < target)
            {
                if (!draw.TryAdd())
                {
                    return !allOrNothing;
                }
            }
            return true;
        }

        private static RollOutcome Fail(RollFailure failure) => RollOutcome.Fail(failure);
    }
}
