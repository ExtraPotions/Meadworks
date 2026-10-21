// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Traits/Mutation.cs and Traits/MutationCatalog.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; stable short ids for the trait blob; ordered list and mask helpers;
// the four 3.22 mutations appended (behavior only: 3.22 source is not at the pin).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Traits
{
    /// <summary>
    /// The twenty-three mutations (design section 1.1 B; Flamebound, Piercing, Howling and Binding from EliteCreaturesReborn 3.22), in the order EliteCreaturesReborn lists them. That order is the
    /// name-word order and the bit index of the legacy <c>ecr:&lt;mask&gt;</c> form, so it never changes.
    /// </summary>
    public enum Mutation
    {
        Mad = 0,
        Bloated = 1,
        Cloaked = 2,
        Splintering = 3,
        Leeching = 4,
        Warding = 5,
        Plated = 6,
        Miasmic = 7,
        Devouring = 8,
        Thieving = 9,
        Gilded = 10,
        Blinking = 11,
        Relentless = 12,
        Juggernaut = 13,
        Screecher = 14,
        Frostbound = 15,
        Mudbound = 16,
        Corrodent = 17,
        Cloning = 18,
        Flamebound = 19,
        Piercing = 20,
        Howling = 21,
        Binding = 22,
    }

    public static class Mutations
    {
        /// <summary>Every mutation in declaration (name-word) order.</summary>
        public static IReadOnlyList<Mutation> InOrder { get; } = Array.AsReadOnly((Mutation[])Enum.GetValues(typeof(Mutation)));

        /// <summary>The bit of a mutation in a mask (EliteCreaturesReborn packing).</summary>
        public static int Bit(Mutation mutation) => 1 << (int)mutation;

        /// <summary>A mask with exactly these mutations.</summary>
        public static int Mask(IEnumerable<Mutation> mutations) => mutations.Aggregate(0, (mask, m) => mask | Bit(m));

        /// <summary>The mutations of a mask in name-word order; bits beyond the catalog are ignored.</summary>
        public static IReadOnlyList<Mutation> Of(int mask) => InOrder.Where(m => (mask & Bit(m)) != 0).ToList();

        /// <summary>Every defined bit.</summary>
        public static int AllBits => (1 << InOrder.Count) - 1;

        /// <summary>The blob id of a mutation: its lowercase name (stable, design 9.1).</summary>
        public static string Id(Mutation mutation) => mutation.ToString().ToLowerInvariant();

        public static bool TryParseId(string? id, out Mutation mutation)
        {
            foreach (var m in InOrder)
                if (string.Equals(Id(m), (id ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    mutation = m;
                    return true;
                }
            mutation = default;
            return false;
        }
    }
}
