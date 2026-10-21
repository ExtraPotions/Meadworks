// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Magpie.Config
{
    /// <summary>
    /// A comma-separated list of prefab names (Ignore Items, Merge Ignore Items, Excluded Items): entries trimmed, empty
    /// entries dropped, compared case-insensitively. Same rule as SwiftPickup <c>IgnoreItems</c> and FloatingItems
    /// <c>ExcludedItems</c> (behavior).
    /// </summary>
    public sealed class NameList
    {
        public static readonly NameList Empty = new NameList(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        private readonly HashSet<string> names;

        private NameList(HashSet<string> names) => this.names = names;

        public int Count => names.Count;

        public IEnumerable<string> Names => names;

        public bool Contains(string? name)
        {
            var trimmed = (name ?? "").Trim();
            return trimmed.Length > 0 && names.Contains(trimmed);
        }

        public static NameList Parse(string? text)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (text ?? "").Split(','))
            {
                var name = part.Trim();
                if (name.Length > 0) set.Add(name);
            }
            return set.Count == 0 ? Empty : new NameList(set);
        }
    }
}
