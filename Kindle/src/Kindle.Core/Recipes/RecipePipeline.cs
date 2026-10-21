// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from OpenKeep 3.0.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, OpenKeep/OpenKeep/src/Recipes/RecipeFilter.cs.
// OpenKeep - a Valheim mod. Copyright (C) 2026 Gerald Glasgow. This program is free software: you can redistribute
// it and/or modify it under the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Portions Copyright (c) 2026 Ab5oluteZer0, MIT (CraftingSearch 1.0.2 at 038d154dc47f5d3f23da78da0acfa0af35240aaa,
// RecipeList.cs, RecipeList.Apply: filter, then Z-A). Full MIT notice in THIRD_PARTY_NOTICES/CraftingSearch-MIT.txt.
//
// Changed by expDARE, 2026: namespace Kindle; one Unity-free pipeline over any row type, in the fixed Kindle order
// (filter, Z-A, favourites first); destroying dropped rows, layout and decoration stay in the plugin (the dropped
// callback, then its own layout and decoration steps); settings and favourites arrive as a RecipeListState; the
// filter also keeps only the picked recipe categories (behaviour of OpenKeep 4.1.0's category buttons).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>Everything the pipeline reads for one rebuild of the crafting list.</summary>
    public sealed class RecipeListState
    {
        public RecipeListState(RecipeQuery query, FavouriteRecipes favourites, RecipeSort sort, bool favouritesEnabled,
            bool favouritesFirst, bool atUpgrader, CultureInfo? nameCulture = null, RecipeCategory categories = RecipeCategory.None)
        {
            Query = query ?? throw new ArgumentNullException(nameof(query));
            Favourites = favourites ?? throw new ArgumentNullException(nameof(favourites));
            Sort = sort;
            FavouritesEnabled = favouritesEnabled;
            FavouritesFirst = favouritesFirst;
            AtUpgrader = atUpgrader;
            NameCulture = nameCulture;
            Categories = categories;
        }

        public RecipeQuery Query { get; }
        public FavouriteRecipes Favourites { get; }
        public RecipeSort Sort { get; }

        /// <summary>The Favourites setting: off hides stars, favourites only and favourites first.</summary>
        public bool FavouritesEnabled { get; }

        /// <summary>The Favorites First setting.</summary>
        public bool FavouritesFirst { get; }

        /// <summary>The player stands at an upgrader station (which materials @ words see).</summary>
        public bool AtUpgrader { get; }

        /// <summary>Culture for Z-A name order; null is the current culture, as the game compares names.</summary>
        public CultureInfo? NameCulture { get; }

        /// <summary>The picked category buttons; none lists every recipe (<see cref="RecipeCategories.Shows"/>).</summary>
        public RecipeCategory Categories { get; }
    }

    /// <summary>What a pipeline run did.</summary>
    public readonly struct PipelineResult
    {
        public PipelineResult(int dropped, bool reordered)
        {
            Dropped = dropped;
            Reordered = reordered;
        }

        /// <summary>Rows removed by the filter.</summary>
        public int Dropped { get; }

        /// <summary>Z-A or favourites first changed the order of the remaining rows.</summary>
        public bool Reordered { get; }
    }

    /// <summary>
    /// After the game has built and sorted the recipe list (Craft or Upgrade tab, <c>UpdateRecipeList</c> postfix), in
    /// a fixed order: 1. filter (favourites only, the picked categories and the query; each dropped row is reported so the plugin destroys it
    /// before the frame renders), 2. Z-A when that sort mode is on, 3. favourites first (a stable partition, so each
    /// group keeps the current order). The plugin then lays the rows out for the view and decorates them. The game picks
    /// the selection afterwards from what is left, so crafting only ever sees listed recipes.
    /// </summary>
    public static class RecipePipeline
    {
        public static PipelineResult Apply<T>(IList<T> rows, Func<T, RecipeFacts?> describe, RecipeListState state, Action<T>? dropped)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));
            if (describe == null)
                throw new ArgumentNullException(nameof(describe));
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            int drops = Filter(rows, describe, state, dropped);
            bool reordered = false;
            if (state.Sort == RecipeSort.NameDescending)
            {
                CultureInfo culture = state.NameCulture ?? CultureInfo.CurrentCulture;
                reordered |= Reorder(rows, describe, Comparer<RecipeFacts?>.Create((a, b) => RecipeSorts.CompareDescending(a, b, culture)));
            }
            if (state.FavouritesEnabled && state.FavouritesFirst)
            {
                FavouriteRecipes favourites = state.Favourites;
                reordered |= Reorder(rows, describe, Comparer<RecipeFacts?>.Create((a, b) => Rank(favourites, a).CompareTo(Rank(favourites, b))));
            }
            return new PipelineResult(drops, reordered);
        }

        private static int Filter<T>(IList<T> rows, Func<T, RecipeFacts?> describe, RecipeListState state, Action<T>? dropped)
        {
            bool only = state.Favourites.OnlyFavourites(state.FavouritesEnabled);
            if (state.Query.Empty && !only && state.Categories == RecipeCategory.None)
                return 0;
            int drops = 0;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                T row = rows[i];
                RecipeFacts? facts = describe(row);
                if ((only && !state.Favourites.Contains(facts?.Key)) || !RecipeCategories.Shows(state.Categories, facts?.Category ?? RecipeCategory.None)
                    || !state.Query.Matches(facts, state.AtUpgrader))
                {
                    dropped?.Invoke(row);
                    rows.RemoveAt(i);
                    drops++;
                }
            }
            return drops;
        }

        private static int Rank(FavouriteRecipes favourites, RecipeFacts? facts) => favourites.Contains(facts?.Key) ? 0 : 1;

        /// <summary>Stable sort of the rows by their facts; true when any row moved.</summary>
        private static bool Reorder<T>(IList<T> rows, Func<T, RecipeFacts?> describe, IComparer<RecipeFacts?> comparer)
        {
            if (rows.Count < 2)
                return false;
            List<T> before = rows.ToList();
            List<RecipeFacts?> facts = before.Select(describe).ToList();
            int[] order = Enumerable.Range(0, before.Count).OrderBy(i => facts[i], comparer).ToArray();
            bool moved = false;
            for (int i = 0; i < order.Length; i++)
            {
                if (order[i] != i)
                    moved = true;
                rows[i] = before[order[i]];
            }
            return moved;
        }
    }
}
