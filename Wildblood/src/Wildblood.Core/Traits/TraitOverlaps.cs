// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Mutations and modifiers that do the same thing. A creature carries at most one half of each pair: the mutation rolls
// first, so it wins and the modifier is left out of the modifier roll; saved creatures that already carry both keep the
// mutation and lose the modifier.
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Traits
{
    public static class TraitOverlaps
    {
        /// <summary>Pairs that count as one effect; the last three are the same element or body-type by user decision.</summary>
        public static IReadOnlyList<(Mutation Mutation, Modifier Modifier)> Pairs { get; } = new[]
        {
            (Mutation.Leeching, Modifier.Vampiric),      // both heal from damage dealt
            (Mutation.Warding, Modifier.Reflection),     // both return part of incoming damage to the attacker
            (Mutation.Plated, Modifier.Armored),         // both cut incoming damage
            (Mutation.Mad, Modifier.Swift),              // Mad moves 1.6x
            (Mutation.Mad, Modifier.AttackSpeed),        // Mad attacks 1.5x faster
            (Mutation.Juggernaut, Modifier.Unflinching), // both ignore stagger
            (Mutation.Flamebound, Modifier.Fire),        // fire trail and fire on hit
            (Mutation.Frostbound, Modifier.Frost),       // frost aura/trail and frost on hit
            (Mutation.Juggernaut, Modifier.Crushing),    // the Juggernaut name and force theme
        };

        /// <summary>True when a creature wearing these mutations may not carry the modifier.</summary>
        public static bool Excludes(int mutations, Modifier modifier)
        {
            foreach (var (mutation, paired) in Pairs)
                if (paired == modifier && (mutations & Mutations.Bit(mutation)) != 0) return true;
            return false;
        }

        /// <summary>The modifiers left after dropping each one whose paired mutation is worn, in their original order.</summary>
        public static IReadOnlyList<Modifier> Without(int mutations, IEnumerable<Modifier> modifiers)
        {
            return modifiers.Where(m => !Excludes(mutations, m)).ToList();
        }
    }
}
