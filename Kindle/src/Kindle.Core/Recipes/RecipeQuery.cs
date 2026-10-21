// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from OpenKeep 3.0.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, OpenKeep/OpenKeep/src/Recipes/RecipeQuery.cs.
// OpenKeep - a Valheim mod. Copyright (C) 2026 Gerald Glasgow. This program is free software: you can redistribute
// it and/or modify it under the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Portions Copyright (c) 2026 Ab5oluteZer0, MIT (CraftingSearch 1.0.2 at 038d154dc47f5d3f23da78da0acfa0af35240aaa,
// RecipeList.cs, accent-insensitive matching). Full MIT notice in THIRD_PARTY_NOTICES/CraftingSearch-MIT.txt.
//
// Changed by expDARE, 2026: namespace Kindle; matches a Unity-free RecipeFacts (the plugin supplies localised names
// and whether the player is at an upgrader); words and texts are folded by RecipeText (CraftingSearch accent
// folding, optional); the word lists are exposed read-only.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>
    /// The search text as a filter. A word must appear, without regard to case (and, by default, accents), in the
    /// item's name in the game's language or in its prefab name; @word in the same names of one of the materials the
    /// crafting panel lists for the recipe at the listed quality and this station (<see cref="RecipeNeeds"/>); a
    /// leading - turns either kind into a word that must not match. Every word must hold. Blank text matches all.
    /// </summary>
    public sealed class RecipeQuery
    {
        public static readonly RecipeQuery All = new RecipeQuery(true);

        private readonly List<string> names = new List<string>();
        private readonly List<string> materials = new List<string>();
        private readonly List<string> notNames = new List<string>();
        private readonly List<string> notMaterials = new List<string>();

        private RecipeQuery(bool ignoreAccents)
        {
            IgnoreAccents = ignoreAccents;
        }

        public bool IgnoreAccents { get; }

        public bool Empty => names.Count + materials.Count + notNames.Count + notMaterials.Count == 0;

        public IReadOnlyList<string> Names => names;
        public IReadOnlyList<string> Materials => materials;
        public IReadOnlyList<string> NotNames => notNames;
        public IReadOnlyList<string> NotMaterials => notMaterials;

        public static RecipeQuery Parse(string? text, bool ignoreAccents = true)
        {
            RecipeQuery query = new RecipeQuery(ignoreAccents);
            if (string.IsNullOrEmpty(text))
                return query;
            foreach (string raw in text!.Split(' ', '\t'))
                query.Add(raw.Trim());
            return query;
        }

        private void Add(string word)
        {
            bool not = word.StartsWith("-");
            if (not)
                word = word.Substring(1);
            bool material = word.StartsWith("@");
            if (material)
                word = word.Substring(1);
            word = RecipeText.Fold(word, IgnoreAccents);
            if (word.Length == 0)
                return;
            List<string> into = material ? (not ? notMaterials : materials) : (not ? notNames : names);
            into.Add(word);
        }

        /// <summary>The row at its listed quality; <paramref name="atUpgrader"/>: the player stands at an upgrader station.</summary>
        public bool Matches(RecipeFacts? recipe, bool atUpgrader)
        {
            if (Empty)
                return true;
            if (recipe == null)
                return false;
            string name = recipe.SearchText(IgnoreAccents);
            if (!AllIn(names, name) || AnyIn(notNames, name))
                return false;
            if (materials.Count == 0 && notMaterials.Count == 0)
                return true;
            List<string> held = MaterialTexts(recipe, atUpgrader);
            foreach (string word in materials)
            {
                if (!held.Exists(text => text.Contains(word)))
                    return false;
            }
            return !notMaterials.Exists(word => held.Exists(text => text.Contains(word)));
        }

        private static bool AllIn(List<string> words, string text) => words.TrueForAll(text.Contains);

        private static bool AnyIn(List<string> words, string text) => words.Exists(text.Contains);

        /// <summary>The materials the crafting panel would list for the recipe at this station (<see cref="RecipeNeeds"/>).</summary>
        private List<string> MaterialTexts(RecipeFacts recipe, bool atUpgrader)
        {
            List<string> result = new List<string>();
            foreach (RecipeMaterial material in recipe.Materials)
            {
                if (RecipeNeeds.Counts(material, atUpgrader))
                    result.Add(RecipeText.ItemText(material.Name, material.PrefabName, IgnoreAccents));
            }
            return result;
        }
    }
}
