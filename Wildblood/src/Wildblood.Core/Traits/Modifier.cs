// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original catalog of the secondary traits (design 1.1 C and the merges of 1.3). The names and the four groups
// follow CreatureManager 1.2.5's modifier list (names only; no code). Blink and Corrosive are merged into the Blinking
// and Corrodent mutations; CreatureManager's Juggernaut is renamed Crushing.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Traits
{
    public enum ModifierGroup { Offense, Defense, Affliction, Special }

    /// <summary>
    /// The thirty modifiers. Values are stable blob ids' order and never renumbered; append new ones at the end.
    /// </summary>
    public enum Modifier
    {
        // Offense (C1)
        Enraged, Fire, Frost, Lightning, Spirit, ArmorPiercing, Staggering, Undodgeable,
        // Defense (C2)
        Armored, Deathward, Regenerating, Reflection, Vortex, Adaptive, Unflinching, Chameleon,
        // Affliction (C3)
        Exposed, Weakened, Withered, Crippling, Disruptive, AdrenalineDrain, ToxicDeath,
        // Special (C4)
        Swift, AttackSpeed, Vampiric, Reaping, Omen, Crushing, Blamer,
    }

    /// <summary>Design 4.4 and 4.5: how many modifiers (one per group) a creature or boss may carry.</summary>
    public enum ModifierCap { Max4, Max3, Max2, Max1, Off }

    public static class Modifiers
    {
        public static IReadOnlyList<Modifier> InOrder { get; } = Array.AsReadOnly((Modifier[])Enum.GetValues(typeof(Modifier)));

        public static ModifierGroup GroupOf(Modifier modifier)
        {
            if (modifier <= Modifier.Undodgeable) return ModifierGroup.Offense;
            if (modifier <= Modifier.Chameleon) return ModifierGroup.Defense;
            if (modifier <= Modifier.ToxicDeath) return ModifierGroup.Affliction;
            return ModifierGroup.Special;
        }

        public static IReadOnlyList<Modifier> InGroup(ModifierGroup group) => InOrder.Where(m => GroupOf(m) == group).ToList();

        /// <summary>The number of modifiers a cap allows.</summary>
        public static int Count(ModifierCap cap)
        {
            switch (cap)
            {
                case ModifierCap.Max4: return 4;
                case ModifierCap.Max3: return 3;
                case ModifierCap.Max2: return 2;
                case ModifierCap.Max1: return 1;
                default: return 0;
            }
        }

        /// <summary>The blob id: lowercase name (stable, design 9.1).</summary>
        public static string Id(Modifier modifier) => modifier.ToString().ToLowerInvariant();

        public static bool TryParseId(string? id, out Modifier modifier)
        {
            foreach (var m in InOrder)
                if (string.Equals(Id(m), (id ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    modifier = m;
                    return true;
                }
            modifier = default;
            return false;
        }

        /// <summary>
        /// A CreatureManager modifier name to Wildblood (design 11.2 converter): Blink and Corrosive become mutations
        /// (null modifier, the mutation in <paramref name="merged"/>), Juggernaut becomes Crushing, spaces are ignored.
        /// </summary>
        public static Modifier? FromCreatureManager(string? name, out Mutation? merged)
        {
            merged = null;
            var key = (name ?? "").Replace(" ", "").Trim();
            if (key.Equals("Blink", StringComparison.OrdinalIgnoreCase)) { merged = Mutation.Blinking; return null; }
            if (key.Equals("Corrosive", StringComparison.OrdinalIgnoreCase)) { merged = Mutation.Corrodent; return null; }
            if (key.Equals("Juggernaut", StringComparison.OrdinalIgnoreCase)) return Modifier.Crushing;
            foreach (var m in InOrder)
                if (m.ToString().Equals(key, StringComparison.OrdinalIgnoreCase)) return m;
            return null;
        }
    }
}
