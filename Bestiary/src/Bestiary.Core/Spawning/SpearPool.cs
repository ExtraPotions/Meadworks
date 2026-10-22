// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Bestiary.Spawning
{
    /// <summary>Spear item prefabs for the Fuling Spearman copy. The shared Goblin prefab is not in this list.</summary>
    public static class SpearPool
    {
        private static readonly string[] Order = { "GoblinSpear", "SpearFlint", "SpearWood" };

        public static IReadOnlyList<string> Candidates => Order;

        /// <summary>One spear: the first candidate <paramref name="present"/> accepts. Empty when none are present.</summary>
        public static string[] For(Func<string, bool>? present)
        {
            if (present == null) return Array.Empty<string>();
            for (int i = 0; i < Order.Length; i++)
            {
                var name = Order[i];
                if (present(name)) return new[] { name };
            }
            return Array.Empty<string>();
        }
    }
}
