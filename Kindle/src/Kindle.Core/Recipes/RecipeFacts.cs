// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>
    /// One material of a recipe as the crafting panel would list it: the item's name in the game's language, its
    /// prefab name (empty when the item is gone), the amount at the quality the row crafts, and whether it is an
    /// upgrader material (<c>Piece.Requirement.m_upgraderResource</c>).
    /// </summary>
    public sealed class RecipeMaterial
    {
        public RecipeMaterial(string? name, string? prefabName, int amount, bool upgraderResource)
        {
            Name = name ?? "";
            PrefabName = prefabName ?? "";
            Amount = amount;
            UpgraderResource = upgraderResource;
        }

        public string Name { get; }
        public string PrefabName { get; }
        public int Amount { get; }
        public bool UpgraderResource { get; }
    }

    /// <summary>
    /// One row of the crafting list, described without Unity: the recipe key (<see cref="RecipeKeys"/>), the made
    /// item's name in the game's language and prefab name, its materials, whether it can be crafted now, the recipe's
    /// list sort weight (<c>Recipe.m_listSortWeight</c>) and, for an upgrade row, the item's current quality (0 for a
    /// new craft) and its <see cref="RecipeCategory"/>. The plugin builds one per row from <c>InventoryGui.m_availableRecipes</c>.
    /// </summary>
    public sealed class RecipeFacts
    {
        private readonly string?[] searchText = new string?[2];

        public RecipeFacts(string key, string? name, string? prefabName, IReadOnlyList<RecipeMaterial>? materials,
            bool canCraft = true, int sortWeight = 0, int itemQuality = 0, RecipeCategory category = RecipeCategory.None)
        {
            Key = key ?? "";
            Name = name ?? "";
            PrefabName = prefabName ?? "";
            Materials = materials ?? Array.Empty<RecipeMaterial>();
            CanCraft = canCraft;
            SortWeight = sortWeight;
            ItemQuality = itemQuality;
            Category = category;
        }

        public string Key { get; }
        public string Name { get; }
        public string PrefabName { get; }
        public IReadOnlyList<RecipeMaterial> Materials { get; }
        public bool CanCraft { get; }
        public int SortWeight { get; }
        public int ItemQuality { get; }
        public RecipeCategory Category { get; }

        /// <summary>Name and prefab name folded for search, kept per folding mode (rows are matched once per word).</summary>
        internal string SearchText(bool ignoreAccents)
        {
            int slot = ignoreAccents ? 1 : 0;
            return searchText[slot] ??= RecipeText.ItemText(Name, PrefabName, ignoreAccents);
        }

        public override string ToString() => Key;
    }
}
