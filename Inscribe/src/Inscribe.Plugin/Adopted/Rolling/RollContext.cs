// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Rolling
{
    /// <summary>Why a roll could not be made. A failed roll never changes anything.</summary>
    public enum RollFailure
    {
        None,
        /// <summary>The pool cannot supply the affixes the roll needs (<c>$ecf_msg_no_eligible_affix</c>).</summary>
        NoEligibleAffix,
        /// <summary>The item cannot carry affixes (stackable, no class that rolls, a rune).</summary>
        NotMagicBase,
        /// <summary>The item already holds its rarity's maximum.</summary>
        Full,
        /// <summary>The state was written by a newer version of the mod.</summary>
        NewerFormat,
    }

    /// <summary>
    /// The inputs every roll shares (classes-and-tiers.md section 5): the item's class and level, the rune's tier floor,
    /// the chaotic flag (the Serpent: every tier the affix defines, uniformly, level and closed tiers ignored), how far the
    /// Serpent's add may pass the prefix and suffix limits, and the random source. Build one with <see cref="For"/>; the
    /// drop code passes a base's own class and level.
    /// </summary>
    public sealed class RollContext
    {
        /// <summary>The item's class, hands, traits and skills.</summary>
        public ClassInfo Class { get; set; } = ClassInfo.None;

        /// <summary>The item level, 1-8: tiers unlocked at this level or below may roll.</summary>
        public int Level { get; set; } = 1;

        /// <summary>The rune's <c>tier_floor</c>: only the best this-many eligible tiers; 0 = none.</summary>
        public int TierFloor { get; set; }

        public bool Chaotic { get; set; }

        /// <summary>A tool/fishing rune may reuse the other kind's budget when its whole currently rollable class pool is one-sided.</summary>
        public bool RuneToolKindBudget { get; set; }

        /// <summary>How far past the rarity's prefix and suffix limits a roll may go (the Serpent's <c>overflow</c>); 0 otherwise.</summary>
        public int LimitOverflow { get; set; }

        /// <summary>
        /// The inscription ids an essence guarantees (the Rune Table's Ascension): only these may be drawn. Null = a
        /// plain roll.
        /// </summary>
        public System.Collections.Generic.ISet<string>? Favoured { get; set; }

        public Random Random { get; set; } = RollRandom.Create();

        /// <summary>The rules to roll under; the running rules unless a caller pins a snapshot.</summary>
        public RuleSet Rules { get; set; } = ActiveRules.Current;

        /// <summary>A context for an item: its class (<see cref="ItemClasses"/>), its item level (<see cref="ItemTier"/>), an optional rune floor.</summary>
        public static RollContext For(ItemDrop.ItemData item, int tierFloor = 0)
        {
            return new RollContext
            {
                Class = ItemClasses.Classify(item),
                Level = ItemTier.Of(item),
                TierFloor = tierFloor,
            };
        }
    }
}
