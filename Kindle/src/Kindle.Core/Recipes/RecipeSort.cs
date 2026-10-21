// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (c) 2026 Ab5oluteZer0, MIT (CraftingSearch 1.0.2 at 038d154dc47f5d3f23da78da0acfa0af35240aaa,
// RecipeList.cs: SortMode, SortSettings, RecipeList.SortByNameDescending). Full MIT notice in
// THIRD_PARTY_NOTICES/CraftingSearch-MIT.txt.
//
// Changed by expDARE, 2026: namespace Kindle; Polish comments translated to English; SortMode renamed RecipeSort
// (Original to Default); the player key access moved to the plugin, which passes the stored values in and out;
// descending key renamed kindle_sort_desc (craftingsearch_desc kept as the converter source); Previous added for
// Shift + click; the Z-A comparator reads Unity-free RecipeFacts, takes the name culture as a parameter and sorts
// stably.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>
    /// Recipe list sort modes. Default, A-Z, Type and Weight are the game's own methods, stored in the player key
    /// <c>sortcraft</c> (the same key the console command <c>sortcraft</c> sets, so the button and the console always
    /// agree). The game has no Z-A: it is the name method plus Kindle's own key beside it.
    /// </summary>
    public enum RecipeSort
    {
        Default,
        NameAscending,
        NameDescending,
        Type,
        Weight,
    }

    public static class RecipeSorts
    {
        /// <summary>The game's player key for the crafting sort method (<c>InventoryGui.SortMethod</c> name).</summary>
        public const string GameKey = "sortcraft";

        /// <summary>Kindle's player key marking the name method as Z-A.</summary>
        public const string DescendingKey = "kindle_sort_desc";

        /// <summary>CraftingSearch's key for the same flag; read by the converter only.</summary>
        public const string CraftingSearchDescendingKey = "craftingsearch_desc";

        private static readonly RecipeSort[] cycle =
            { RecipeSort.Default, RecipeSort.NameAscending, RecipeSort.NameDescending, RecipeSort.Type, RecipeSort.Weight };

        /// <summary>The button's order: click goes forward, Shift + click back.</summary>
        public static IReadOnlyList<RecipeSort> Cycle => cycle;

        public static RecipeSort Next(RecipeSort mode) => cycle[(Index(mode) + 1) % cycle.Length];

        public static RecipeSort Previous(RecipeSort mode) => cycle[(Index(mode) + cycle.Length - 1) % cycle.Length];

        private static int Index(RecipeSort mode) => Math.Max(0, Array.IndexOf(cycle, mode));

        /// <summary>The mode from the stored <c>sortcraft</c> value (any case) and Kindle's descending flag; anything unknown is Default.</summary>
        public static RecipeSort FromStored(string? gameMethod, bool descending)
        {
            if (string.IsNullOrEmpty(gameMethod))
                return RecipeSort.Default;
            if (string.Equals(gameMethod, "Name", StringComparison.OrdinalIgnoreCase))
                return descending ? RecipeSort.NameDescending : RecipeSort.NameAscending;
            if (string.Equals(gameMethod, "Type", StringComparison.OrdinalIgnoreCase))
                return RecipeSort.Type;
            if (string.Equals(gameMethod, "Weight", StringComparison.OrdinalIgnoreCase))
                return RecipeSort.Weight;
            return RecipeSort.Default;
        }

        /// <summary>The values to store: the <c>sortcraft</c> method (null: remove the key) and whether to set the descending flag.</summary>
        public static (string? Game, bool Descending) ToStored(RecipeSort mode)
        {
            switch (mode)
            {
                case RecipeSort.NameAscending:
                    return ("Name", false);
                case RecipeSort.NameDescending:
                    return ("Name", true);
                case RecipeSort.Type:
                    return ("Type", false);
                case RecipeSort.Weight:
                    return ("Weight", false);
                default:
                    return (null, false);
            }
        }

        /// <summary>English source text of the sort button's tooltip topic.</summary>
        public static string Label(RecipeSort mode)
        {
            switch (mode)
            {
                case RecipeSort.NameAscending:
                    return "Sort: A-Z";
                case RecipeSort.NameDescending:
                    return "Sort: Z-A";
                case RecipeSort.Type:
                    return "Sort: Type";
                case RecipeSort.Weight:
                    return "Sort: Weight";
                default:
                    return "Sort: Default";
            }
        }

        /// <summary>
        /// The game's name sort with the name reversed: craftable first, then the recipe's list sort weight, then the
        /// name descending (in <paramref name="culture"/>, the current culture when null, as the game compares names),
        /// then the upgrade item's quality descending. A missing row sorts last.
        /// </summary>
        public static int CompareDescending(RecipeFacts? a, RecipeFacts? b, CultureInfo? culture = null)
        {
            if (a == null || b == null)
                return a == null ? (b == null ? 0 : 1) : -1;
            int result = b.CanCraft.CompareTo(a.CanCraft);
            if (result == 0)
                result = a.SortWeight.CompareTo(b.SortWeight);
            if (result == 0)
                result = (culture ?? CultureInfo.CurrentCulture).CompareInfo.Compare(b.Name, a.Name, CompareOptions.None);
            if (result == 0)
                result = b.ItemQuality.CompareTo(a.ItemQuality);
            return result;
        }

        /// <summary>The rows in Z-A order; stable, so equal rows keep the game's order.</summary>
        public static List<RecipeFacts> SortDescending(IEnumerable<RecipeFacts> rows, CultureInfo? culture = null)
        {
            CultureInfo used = culture ?? CultureInfo.CurrentCulture;
            return rows.OrderBy(row => row, Comparer<RecipeFacts>.Create((a, b) => CompareDescending(a, b, used))).ToList();
        }
    }
}
