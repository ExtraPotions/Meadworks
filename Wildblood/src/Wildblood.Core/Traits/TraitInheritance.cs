// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Breeding/Inheritance.cs (trait part) at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; IDice; stars left to Shepherd (design 9.1); the mother's modifiers carried,
// filtered by the newborn's rules (CreatureManager lifecycle preservation); blob form for the capability.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Traits
{
    /// <summary>
    /// Inheritance for capability <c>meadworks.creatures.traits</c> v1 (Shepherd section 8.2): two plain parents give plain
    /// young; otherwise one mutation drawn from the union of the parents' mutations at <c>mutationChance</c>, skipping
    /// disabled ones and ones the newborn's body bars; never a new roll. The newborn's level is Shepherd's and is not changed.
    /// </summary>
    public static class TraitInheritance
    {
        public static TraitSet Inherit(TraitSet mother, TraitSet? sire, float mutationChance, int enabled, int barred,
            Func<Modifier, bool> modifierAllowed, IDice dice)
        {
            if (mother == null) throw new ArgumentNullException(nameof(mother));
            if (modifierAllowed == null) throw new ArgumentNullException(nameof(modifierAllowed));
            var pool = (mother.Mutations | (sire?.Mutations ?? 0)) & ~barred;
            var candidates = new List<Mutation>();
            foreach (var mutation in Mutations.Of(pool))
                if ((enabled & Mutations.Bit(mutation)) != 0) candidates.Add(mutation);
            var mask = 0;
            if (candidates.Count > 0 && dice.Percent(mutationChance))
                mask = Mutations.Bit(candidates[dice.Range(0, candidates.Count)]);
            return new TraitSet(mask, mother.Modifiers.Where(modifierAllowed), mother.Biome);
        }

        /// <summary>The capability's blob form; an unreadable parent counts as plain.</summary>
        public static string Inherit(string mother, string sire, float mutationChance, int enabled, int barred,
            Func<Modifier, bool> modifierAllowed, IDice dice)
        {
            TraitBlob.TryDecode(mother, out var m);
            TraitBlob.TryDecode(sire, out var s);
            var young = Inherit(m, s, mutationChance, enabled, barred, modifierAllowed, dice);
            return young.Mutations == 0 && young.Modifiers.Count == 0 ? "" : TraitBlob.Encode(young);
        }
    }
}
