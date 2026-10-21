// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Items;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>Recipe lookups for commands, through the spine's <c>RecipeIndex</c>.</summary>
    internal static class Recipes
    {
        /// <summary>Brings the recipe index up to date, dropping ItemTier's cache with it (<see cref="ItemTier.RefreshRecipes"/>).</summary>
        public static void Refresh() => ItemTier.RefreshRecipes();

        public static IReadOnlyList<Recipe> For(string prefab) => RecipeIndex.For(prefab);
    }
}
