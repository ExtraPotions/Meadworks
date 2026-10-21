// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (c) 2026 Ab5oluteZer0, MIT (CraftingSearch 1.0.2 at 038d154dc47f5d3f23da78da0acfa0af35240aaa,
// RecipeList.cs, RecipeList.Filter). Full MIT notice in THIRD_PARTY_NOTICES/CraftingSearch-MIT.txt.
// Adapted from OpenKeep 3.0.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, OpenKeep/OpenKeep/src/Recipes/RecipeQuery.cs
// (RecipeQuery.ItemText). OpenKeep - a Valheim mod. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: CraftingSearch's accent-insensitive compare (CompareOptions.IgnoreCase plus
// IgnoreNonSpace) is done once per text as a fold (invariant lower case, canonical decomposition, combining marks
// removed) so OpenKeep's per-word ordinal Contains keeps working and the result does not depend on the runtime's
// collation tables; folding is optional (Kindle setting Ignore Accents).
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text;

namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>Search text: invariant lower case, optionally without accents (<c>Żelazo</c> folds to <c>zelazo</c>).</summary>
    public static class RecipeText
    {
        public static string Fold(string? text, bool ignoreAccents)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            string lower = text!.ToLowerInvariant();
            if (!ignoreAccents)
                return lower;
            string decomposed = lower.Normalize(NormalizationForm.FormD);
            StringBuilder kept = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    kept.Append(c);
            }
            return kept.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>The item's name in the game's language and its prefab name, folded, on two lines so no word spans both.</summary>
        public static string ItemText(string? name, string? prefabName, bool ignoreAccents)
            => Fold(name, ignoreAccents) + "\n" + Fold(prefabName, ignoreAccents);
    }
}
