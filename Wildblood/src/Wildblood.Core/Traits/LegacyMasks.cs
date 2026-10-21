// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0, adapted from CreatureModifierManager.cs (ModifierMask bit
// order of the ZDO key CreatureManager_ModifierMask64) at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Changed by expDARE, 2026: the bit order as a converter table into Wildblood modifiers and merged mutations (design 11.2).
// SPDX-License-Identifier: GPL-3.0-only
using System.Collections.Generic;
using ExtraPotions.Wildblood.Stars;

namespace ExtraPotions.Wildblood.Traits
{
    /// <summary>Converts saved creature masks of the source mods (design 11.2, B16) into Wildblood traits.</summary>
    public static class LegacyMasks
    {
        /// <summary>CreatureManager's ModifierMask bits 0 to 31; Blink and Corrosive become mutations, Knockback ("Juggernaut") Crushing.</summary>
        private static readonly (Modifier? Modifier, Mutation? Mutation)[] CreatureManagerBits =
        {
            (Modifier.Armored, null), (Modifier.Enraged, null), (Modifier.Deathward, null), (Modifier.Swift, null),
            (Modifier.Regenerating, null), (Modifier.Vampiric, null), (Modifier.Fire, null), (Modifier.Frost, null),
            (Modifier.Lightning, null), (Modifier.ToxicDeath, null), (Modifier.ArmorPiercing, null), (Modifier.Staggering, null),
            (Modifier.AttackSpeed, null), (Modifier.Exposed, null), (Modifier.Weakened, null), (Modifier.Withered, null),
            (Modifier.Reflection, null), (Modifier.Vortex, null), (Modifier.Crippling, null), (Modifier.Disruptive, null),
            (Modifier.Adaptive, null), (Modifier.Omen, null), (Modifier.Reaping, null), (Modifier.Spirit, null),
            (null, Mutation.Blinking), (Modifier.Crushing, null), (Modifier.Unflinching, null), (Modifier.AdrenalineDrain, null),
            (null, Mutation.Corrodent), (Modifier.Undodgeable, null), (Modifier.Chameleon, null), (Modifier.Blamer, null),
        };

        /// <summary>The source engine mask for canonical modifiers; merged mutations never activate a second engine.</summary>
        public static long ToCreatureManagerModifiers(IEnumerable<Modifier> modifiers)
        {
            var selected = new HashSet<Modifier>(modifiers);
            long mask = 0;
            for (var bit = 0; bit < CreatureManagerBits.Length; bit++)
                if (CreatureManagerBits[bit].Modifier is Modifier modifier && selected.Contains(modifier)) mask |= 1L << bit;
            return mask;
        }

        /// <summary>The traits a CreatureManager mask carries; bits above 31 are ignored.</summary>
        public static TraitSet FromCreatureManager(long mask, Biome biome = Biome.Other)
        {
            var modifiers = new List<Modifier>();
            var mutations = 0;
            for (var bit = 0; bit < CreatureManagerBits.Length; bit++)
            {
                if ((mask & (1L << bit)) == 0) continue;
                var (modifier, mutation) = CreatureManagerBits[bit];
                if (modifier.HasValue) modifiers.Add(modifier.Value);
                if (mutation.HasValue) mutations |= Mutations.Bit(mutation.Value);
            }
            return new TraitSet(mutations, modifiers, biome);
        }
    }
}
