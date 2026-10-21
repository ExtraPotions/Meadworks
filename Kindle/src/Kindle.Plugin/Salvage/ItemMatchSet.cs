// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries.
using System.Collections.Generic;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>A list of vocabulary entries; an item matches when any entry does. Blank entries are dropped,
    /// entries with a <see cref="ItemMatcher.Problem"/> are kept (they match nothing) so callers can report them.</summary>
    public sealed class ItemMatchSet
    {
        private readonly List<ItemMatcher> matchers;

        private ItemMatchSet(List<ItemMatcher> matchers)
        {
            this.matchers = matchers;
        }

        public static ItemMatchSet Empty { get; } = new ItemMatchSet(new List<ItemMatcher>());

        public bool IsEmpty => matchers.Count == 0;

        public IReadOnlyList<ItemMatcher> Matchers => matchers;

        public static ItemMatchSet Parse(IEnumerable<string> entries, ItemGroups groups)
        {
            List<ItemMatcher> parsed = new List<ItemMatcher>();
            if (entries != null)
            {
                foreach (string entry in entries)
                {
                    if (!string.IsNullOrWhiteSpace(entry))
                        parsed.Add(ItemMatcher.Parse(entry, groups));
                }
            }
            return parsed.Count == 0 ? Empty : new ItemMatchSet(parsed);
        }

        /// <summary>An empty set returns at once, without building the item's prefab name (a string per call).</summary>
        public bool Matches(ItemDrop.ItemData item)
        {
            if (matchers.Count == 0)
                return false;
            return item != null && item.m_shared != null && Matches(ItemNames.PrefabName(item), item.m_shared);
        }

        public bool Matches(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            foreach (ItemMatcher matcher in matchers)
            {
                if (matcher.Matches(prefabName, shared))
                    return true;
            }
            return false;
        }

        public override string ToString() => string.Join(", ", matchers);
    }
}
