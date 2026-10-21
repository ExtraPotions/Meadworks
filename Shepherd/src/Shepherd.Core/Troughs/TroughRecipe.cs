// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Companions/FeederRecipe.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Unity-free parse with an item lookup; shared by the three trough recipes.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtraPotions.Shepherd.Troughs
{
    /// <summary>One "Name:Amount" recipe entry.</summary>
    public readonly struct RecipeEntry
    {
        public RecipeEntry(string item, int amount) { Item = item; Amount = amount; }
        public string Item { get; }
        public int Amount { get; }
    }

    /// <summary>
    /// A trough recipe ("Wood:10, LeatherScraps:4"): item prefab names with amounts. Unknown items and bad amounts are
    /// skipped; an empty or unusable recipe falls back to <see cref="Fallback"/> (GrindstoneSkills rule).
    /// </summary>
    public static class TroughRecipe
    {
        public const string Fallback = "Wood:10";

        public static IReadOnlyList<RecipeEntry> Parse(string? text, Func<string, bool> knownItem)
        {
            if (knownItem == null) throw new ArgumentNullException(nameof(knownItem));
            var entries = Read(text, knownItem);
            return entries.Count > 0 ? entries : Read(Fallback, _ => true);
        }

        private static List<RecipeEntry> Read(string? text, Func<string, bool> knownItem)
        {
            var entries = new List<RecipeEntry>();
            foreach (var part in (text ?? "").Split(','))
            {
                var fields = part.Split(':');
                if (fields.Length != 2) continue;
                if (!int.TryParse(fields[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) || amount < 1) continue;
                var item = fields[0].Trim();
                if (item.Length == 0 || !knownItem(item)) continue;
                entries.Add(new RecipeEntry(item, amount));
            }
            return entries;
        }
    }
}
