// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Rolling
{
    /// <summary>
    /// The rarity's prefix and suffix limits during one roll (classes-and-tiers.md section 6): how many of each kind the
    /// item holds (dormant inscriptions count by their definition; orphaned ones count for nothing) and how many it may
    /// hold, the Serpent's overflow added. The rarity is the builder's: a fresh roll and a promotion set it first, so the
    /// new rarity's limits apply. An unknown rarity id limits nothing.
    /// </summary>
    internal sealed class AffixLimits
    {
        private readonly int _maxPrefixes;
        private readonly int _maxSuffixes;
        private int _prefixes;
        private int _suffixes;

        private AffixLimits(int maxPrefixes, int maxSuffixes)
        {
            _maxPrefixes = maxPrefixes;
            _maxSuffixes = maxSuffixes;
        }

        public static AffixLimits For(ItemStateBuilder builder, RollContext context)
        {
            EconomyRules economy = context.Rules.Economy;
            RarityDef? rarity = builder.RarityId == null ? economy.BaseRarity : economy.Rarity(builder.RarityId);
            int overflow = System.Math.Max(context.LimitOverflow, 0);
            int prefixes = rarity?.MaxPrefixes ?? int.MaxValue / 2;
            int suffixes = rarity?.MaxSuffixes ?? int.MaxValue / 2;
            if (rarity != null && context.RuneToolKindBudget)
            {
                // Distinct choices of each kind; an exclusion group counts once, since only one of it can roll.
                var prefixChoices = new System.Collections.Generic.HashSet<string>();
                var suffixChoices = new System.Collections.Generic.HashSet<string>();
                foreach (var entry in context.Rules.Affixes.Pool(context.Class.ClassId))
                    (entry.Def.Kind == AffixKind.Prefix ? prefixChoices : suffixChoices).Add(GroupOf(entry.Def));
                bool prefixPool = prefixChoices.Count > 0, suffixPool = suffixChoices.Count > 0;
                int budget = (int)System.Math.Min(System.Math.Max(rarity.MaxAffixes, 0),
                    (long)System.Math.Max(prefixes, 0) + System.Math.Max(suffixes, 0));
                if (prefixPool && !suffixPool && prefixes > 0) { prefixes = budget; suffixes = 0; }
                else if (suffixPool && !prefixPool && suffixes > 0) { suffixes = budget; prefixes = 0; }
                // Both kinds present but one too thin for the rarity (a pickaxe has one prefix, an Epic wants two): the
                // slots that kind cannot fill move to the other kind, within the rarity's total.
                else if (prefixPool && suffixPool && prefixChoices.Count < prefixes && suffixes > 0 && suffixChoices.Count >= suffixes)
                {
                    prefixes = prefixChoices.Count;
                    suffixes = System.Math.Max(0, budget - prefixes);
                }
                else if (prefixPool && suffixPool && suffixChoices.Count < suffixes && prefixes > 0 && prefixChoices.Count >= prefixes)
                {
                    suffixes = suffixChoices.Count;
                    prefixes = System.Math.Max(0, budget - suffixes);
                }
            }
            // Overflow is added after reallocation, as it is for every other Serpent roll.
            AffixLimits limits = rarity == null
                ? new AffixLimits(prefixes, suffixes)
                : new AffixLimits(prefixes + overflow, suffixes + overflow);
            foreach (AffixRoll roll in builder.Affixes)
            {
                limits.Count(context.Rules.Affixes.Get(roll.Id));
            }
            return limits;
        }

        private static string GroupOf(AffixDef def) => def.ExclusionGroup != null ? "group:" + def.ExclusionGroup : "id:" + def.Id;

        /// <summary>Whether one more inscription of this kind keeps the item within its limits.</summary>
        public bool Allows(AffixKind kind) =>
            kind == AffixKind.Prefix ? _prefixes < _maxPrefixes : _suffixes < _maxSuffixes;

        /// <summary>Counts an inscription now on the item.</summary>
        public void Count(AffixDef? def)
        {
            if (def == null)
            {
                return;
            }
            if (def.Kind == AffixKind.Prefix)
            {
                _prefixes++;
            }
            else
            {
                _suffixes++;
            }
        }
    }
}
