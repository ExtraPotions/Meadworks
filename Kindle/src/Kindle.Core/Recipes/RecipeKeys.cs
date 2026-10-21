// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from OpenKeep 3.0.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, OpenKeep/OpenKeep/src/Recipes/RecipeKeys.cs.
// OpenKeep - a Valheim mod. Copyright (C) 2026 Gerald Glasgow. This program is free software: you can redistribute
// it and/or modify it under the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Kindle; the key rule takes the recipe asset name and the item prefab name as
// strings; the static ObjectDB index became RecipeKeyIndex over any recipe type (the plugin keeps one for the game's
// Recipe, keyed by the ObjectDB instance, and its own per-recipe key cache for Unity names).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>
    /// A recipe's identity in the character's saved lists (favourites, the tracker): the recipe asset's name
    /// (<c>Recipe_SwordIron</c>), or its item's prefab name when the recipe has none, without the lists' separators
    /// (comma and bar).
    /// </summary>
    public static class RecipeKeys
    {
        public static string Of(string? recipeName, string? itemPrefabName)
        {
            string name = !string.IsNullOrEmpty(recipeName) ? recipeName! : itemPrefabName ?? "";
            return name.Replace(",", "").Replace("|", "").Trim();
        }
    }

    /// <summary>
    /// Turns a key back into the loaded recipe. The index is rebuilt whenever the item database or its recipe count
    /// changes, so recipes other mods add later are found too. The first recipe of a key wins, as the game's own
    /// lookups take the first match; a recipe that is no longer usable (its item is gone) is never returned.
    /// </summary>
    public sealed class RecipeKeyIndex<T> where T : class
    {
        private readonly Dictionary<string, T> byKey = new Dictionary<string, T>(StringComparer.Ordinal);
        private readonly Func<T, string?> keyOf;
        private readonly Func<T, bool> usable;
        private object? indexed;
        private int indexedCount = -1;

        public RecipeKeyIndex(Func<T, string?> keyOf, Func<T, bool> usable)
        {
            this.keyOf = keyOf ?? throw new ArgumentNullException(nameof(keyOf));
            this.usable = usable ?? throw new ArgumentNullException(nameof(usable));
        }

        /// <summary>How many times the index was built (diagnostics and tests).</summary>
        public int Rebuilds { get; private set; }

        /// <summary>The loaded recipe with that key in <paramref name="recipes"/> of <paramref name="database"/>, or null.</summary>
        public T? Find(object? database, IReadOnlyList<T>? recipes, string? key)
        {
            if (database == null || recipes == null || string.IsNullOrEmpty(key))
                return null;
            if (!ReferenceEquals(database, indexed) || recipes.Count != indexedCount)
                Index(database, recipes);
            return byKey.TryGetValue(key!, out T recipe) && recipe != null && usable(recipe) ? recipe : null;
        }

        private void Index(object database, IReadOnlyList<T> recipes)
        {
            byKey.Clear();
            indexed = database;
            indexedCount = recipes.Count;
            Rebuilds++;
            foreach (T recipe in recipes)
            {
                if (recipe == null || !usable(recipe))
                    continue;
                string key = keyOf(recipe) ?? "";
                if (key.Length > 0 && !byKey.ContainsKey(key))
                    byKey[key] = recipe;
            }
        }
    }
}
