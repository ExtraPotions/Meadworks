// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 7c710ed119b547783812e8171c86f31ce1f7ea50: ValheimModLibs/RecipeCounts/AnyQuality.cs
// Changed by expDARE, 2026: the count rule only. The game patch stays in the plugin.
using System.Collections.Generic;

namespace ExtraPotions.Larder.Cooking
{
    /// <summary>
    /// An egg stores its hen's stars as item quality (a 2-star hen lays quality 3) while its maximum quality stays 1.
    /// The game asks only for quality 1, so those eggs never counted. Paying already takes any quality.
    /// </summary>
    public static class RecipeQuality
    {
        public struct Stack
        {
            public string Name;
            public int Quality;
            public int Amount;

            public Stack(string name, int quality, int amount)
            {
                Name = name;
                Quality = quality;
                Amount = amount;
            }
        }

        /// <summary>True while a recipe that needs every ingredient is being counted.</summary>
        public static bool TracksEveryIngredient(bool hasRecipe, bool requireOnlyOneIngredient) =>
            hasRecipe && !requireOnlyOneIngredient;

        /// <summary>
        /// The quality-1 count of a single-quality item includes every quality. A recipe that needs only one
        /// ingredient, and any item that can be crafted at quality 2 or higher, keeps the quality that was asked for.
        /// </summary>
        public static bool CountEveryQuality(bool trackingEveryIngredient, int requestedQuality, int maxQuality) =>
            trackingEveryIngredient && requestedQuality == 1 && maxQuality <= 1;

        public static int Count(IEnumerable<Stack> stacks, string name, int requestedQuality, int maxQuality, bool trackingEveryIngredient)
        {
            if (name == null || stacks == null) return 0;
            bool every = CountEveryQuality(trackingEveryIngredient, requestedQuality, maxQuality);
            int total = 0;
            foreach (Stack stack in stacks)
            {
                if (stack.Name != name || stack.Amount <= 0) continue;
                if (!every && requestedQuality >= 0 && stack.Quality != requestedQuality) continue;
                total += stack.Amount;
            }
            return total;
        }
    }
}
