// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Finds/FindEntry.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>One find of Saga.Finds.yml: what the callout calls it, how often it is picked, what drops.</summary>
    public sealed class FindEntry
    {
        public FindEntry(string name, float weight, List<FindItem> items)
        {
            Name = name;
            Weight = weight;
            Items = items;
        }

        /// <summary>Shown to everybody near the tree as "Found &lt;name&gt;!".</summary>
        public string Name { get; }

        /// <summary>The find's share of its table: picked with weight / (sum of the table's weights); 0 never.</summary>
        public float Weight { get; }

        public List<FindItem> Items { get; }
    }

    /// <summary>One item of a find: the item prefab's name and how many drop, rolled from Min to Max.</summary>
    public sealed class FindItem
    {
        public FindItem(string prefab, int min, int max)
        {
            Prefab = prefab;
            Min = min;
            Max = max;
        }

        /// <summary>The item's prefab name as the game knows it (Feathers, Honey, QueenBee), resolved when it drops.</summary>
        public string Prefab { get; }

        public int Min { get; }
        public int Max { get; }
    }
}
