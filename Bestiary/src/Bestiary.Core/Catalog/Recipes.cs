// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later: EliteCreaturesPack 0.7.2 at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (Arsenal/ArsenalRecipes.cs Cost, Headsman/Greataxe/GreataxeRecipe.cs Cost).
// Changed by expDARE, 2026: Unity-free parse into cost entries with the rejected entries returned for the log.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Bestiary.Catalog
{
    /// <summary>One requirement: item prefab, amount, amount per upgrade.</summary>
    public readonly struct CostEntry : IEquatable<CostEntry>
    {
        public CostEntry(string item, int amount, int perLevel)
        {
            Item = item;
            Amount = amount;
            PerLevel = perLevel;
        }

        public string Item { get; }
        public int Amount { get; }
        public int PerLevel { get; }

        public bool Equals(CostEntry other) => Item == other.Item && Amount == other.Amount && PerLevel == other.PerLevel;
        public override bool Equals(object? obj) => obj is CostEntry other && Equals(other);
        public override int GetHashCode() => (Item ?? "").GetHashCode() ^ Amount * 397 ^ PerLevel;
        public override string ToString() => Item + ":" + Amount + ":" + PerLevel;
    }

    public sealed class ParsedCost
    {
        public ParsedCost(IReadOnlyList<CostEntry> entries, IReadOnlyList<string> rejected)
        {
            Entries = entries;
            Rejected = rejected;
        }

        public IReadOnlyList<CostEntry> Entries { get; }

        /// <summary>Entries that are not <c>item:amount[:per upgrade]</c> with a known item, trimmed; logged and left out.</summary>
        public IReadOnlyList<string> Rejected { get; }
    }

    /// <summary>ECP's cost text: <c>"ECP_Spine:1, BoneFragments:10:5"</c>.</summary>
    public static class RecipeText
    {
        public static ParsedCost Parse(string? text, Func<string, bool> knownItem)
        {
            var entries = new List<CostEntry>();
            var rejected = new List<string>();
            foreach (var entry in (text ?? "").Split(','))
            {
                var trimmed = entry.Trim();
                var parts = trimmed.Split(':');
                var item = parts.Length >= 2 ? parts[0].Trim() : null;
                if (item == null || !knownItem(item) || !int.TryParse(parts[1], out var amount))
                {
                    if (trimmed.Length > 0) rejected.Add(trimmed);
                    continue;
                }
                var perLevel = parts.Length >= 3 && int.TryParse(parts[2], out var per) ? per : 0;
                entries.Add(new CostEntry(item, amount, perLevel));
            }
            return new ParsedCost(entries, rejected);
        }
    }

    public static class Recipes
    {
        /// <summary>
        /// The Executioner's Greataxe: its axehead, <paramref name="spines"/> spines (bone fragments in their place when
        /// the spine does not exist) and <paramref name="bones"/> bone fragments; the same item adds up; none per upgrade.
        /// </summary>
        public static IReadOnlyList<CostEntry> GreataxeCost(int spines, int bones, bool spineAvailable)
        {
            var cost = new List<CostEntry>();
            Add(cost, "ECP_ExecutionerAxehead", 1);
            Add(cost, spineAvailable ? "ECP_Spine" : "BoneFragments", spines);
            Add(cost, "BoneFragments", bones);
            return cost;
        }

        private static void Add(List<CostEntry> cost, string item, int amount)
        {
            if (amount <= 0) return;
            var index = cost.FindIndex(c => c.Item == item);
            if (index >= 0) cost[index] = new CostEntry(item, cost[index].Amount + amount, 0);
            else cost.Add(new CostEntry(item, amount, 0));
        }
    }
}
