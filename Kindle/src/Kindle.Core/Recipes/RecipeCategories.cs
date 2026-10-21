// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behaviour follows OpenKeep 4.1.0 (Recipe List: category buttons under the search filter the recipes); Meadworks-original code.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>The crafting list's recipe categories, one button each under the search row, in <see cref="RecipeCategories.All"/>'s order.</summary>
    [Flags]
    public enum RecipeCategory
    {
        None = 0,
        Ammo = 1,
        Weapons = 2,
        Bows = 4,
        Armor = 8,
        Shields = 16,
        Tools = 32,
        Food = 64,
        Materials = 128,
    }

    /// <summary>
    /// Which categories the list shows. None picked shows every recipe; a click on a category while none is picked shows
    /// only it, a click on another adds it, a click on a picked one takes it away, and taking the last away shows every
    /// recipe again. A recipe in no category (meads, trinkets and the like) shows only while nothing is picked.
    /// </summary>
    public static class RecipeCategories
    {
        public static IReadOnlyList<RecipeCategory> All { get; } = new[]
        {
            RecipeCategory.Ammo, RecipeCategory.Weapons, RecipeCategory.Bows, RecipeCategory.Armor,
            RecipeCategory.Shields, RecipeCategory.Tools, RecipeCategory.Food, RecipeCategory.Materials,
        };

        public static RecipeCategory Toggle(RecipeCategory picked, RecipeCategory category) => picked ^ category;

        public static bool IsPicked(RecipeCategory picked, RecipeCategory category) => (picked & category) != 0;

        /// <summary>Whether a recipe of <paramref name="category"/> is listed while <paramref name="picked"/> are picked.</summary>
        public static bool Shows(RecipeCategory picked, RecipeCategory category) => picked == RecipeCategory.None || (picked & category) != 0;
    }
}
