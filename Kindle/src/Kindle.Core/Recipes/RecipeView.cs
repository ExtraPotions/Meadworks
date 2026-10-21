// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from OpenKeep 3.0.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, OpenKeep/OpenKeep/src/Recipes/RecipeView.cs.
// OpenKeep - a Valheim mod. Copyright (C) 2026 Gerald Glasgow. This program is free software: you can redistribute
// it and/or modify it under the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Kindle; RecipeViews added (the view button's next and previous, config value
// parsing with fallback to List, grid test) so the plugin and the converter share one rule.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>How the crafting panel lays out its recipes: the game's list, a tighter list, or icon tiles 5, 4 or 3 to a row.</summary>
    public enum RecipeView
    {
        List,
        CompactList,
        SmallGrid,
        MediumGrid,
        LargeGrid,
    }

    public static class RecipeViews
    {
        private static readonly RecipeView[] cycle =
            { RecipeView.List, RecipeView.CompactList, RecipeView.SmallGrid, RecipeView.MediumGrid, RecipeView.LargeGrid };

        /// <summary>The view button's order: click goes forward, Shift + click back.</summary>
        public static IReadOnlyList<RecipeView> Cycle => cycle;

        public static RecipeView Next(RecipeView view) => cycle[(Index(view) + 1) % cycle.Length];

        public static RecipeView Previous(RecipeView view) => cycle[(Index(view) + cycle.Length - 1) % cycle.Length];

        private static int Index(RecipeView view) => Math.Max(0, Array.IndexOf(cycle, view));

        /// <summary>A view by name in any case; anything else (including numbers) is List.</summary>
        public static RecipeView Parse(string? text)
        {
            foreach (RecipeView view in cycle)
            {
                if (string.Equals(view.ToString(), text?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return view;
            }
            return RecipeView.List;
        }

        public static bool IsGrid(RecipeView view) => view == RecipeView.SmallGrid || view == RecipeView.MediumGrid || view == RecipeView.LargeGrid;
    }
}
