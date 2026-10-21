// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries.
using System.Collections.Generic;
using ExtraPotions.Kindle.Runtime.Salvage;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// What may be salvaged and at which fraction: the recipe lookup in the object database, the YAML exclusions
    /// and overrides, favourites, items carrying another mod's item data, equipped items, known recipes and
    /// stations. <see cref="Blocker"/> gives the reason an item cannot be salvaged as a "$ok_..." word, or null
    /// when it can.
    /// </summary>
    public static class SalvageRules
    {
        private static readonly Dictionary<string, Recipe> recipeCache = new Dictionary<string, Recipe>();
        private static ObjectDB cacheDb;
        private static int cacheCount = -1;

        public static SalvageModel Current { get; private set; }

        public static void Apply(SalvageModel model) => Current = model;

        /// <summary>
        /// The first enabled recipe of the object database whose result is this item, skipping recipes that need
        /// the item itself, recipes with a single alternative ingredient and recipes without materials.
        /// </summary>
        public static Recipe FindRecipe(ItemDrop.ItemData item)
        {
            ObjectDB db = ObjectDB.instance;
            if (item == null || item.m_shared == null || db == null)
                return null;
            if (cacheDb != db || cacheCount != db.m_recipes.Count)
            {
                recipeCache.Clear();
                cacheDb = db;
                cacheCount = db.m_recipes.Count;
            }
            string name = item.m_shared.m_name;
            if (!recipeCache.TryGetValue(name, out Recipe recipe))
            {
                recipe = ScanRecipes(db, name);
                recipeCache[name] = recipe;
            }
            return recipe;
        }

        private static Recipe ScanRecipes(ObjectDB db, string name)
        {
            foreach (Recipe recipe in db.m_recipes)
            {
                if (recipe == null || recipe.m_item == null || !recipe.m_enabled || recipe.m_requireOnlyOneIngredient)
                    continue;
                if (recipe.m_item.m_itemData.m_shared.m_name != name || recipe.m_resources.Length == 0)
                    continue;
                if (!NeedsItself(recipe, name))
                    return recipe;
            }
            return null;
        }

        private static bool NeedsItself(Recipe recipe, string name)
        {
            foreach (Piece.Requirement requirement in recipe.m_resources)
                if (requirement.m_resItem != null && requirement.m_resItem.m_itemData.m_shared.m_name == name)
                    return true;
            return false;
        }

        public static bool IsExcluded(ItemDrop.ItemData item)
        {
            return Current != null && !Current.Deny.IsEmpty && Current.Deny.Matches(item);
        }

        /// <summary>Favourite item names come from the Stow module's custom data; prefab and shared name both count.</summary>
        public static bool IsFavourite(ItemDrop.ItemData item)
        {
            return SalvageInterop.Favourite(item);
        }

        /// <summary>The return fraction for an item: an exact override, then the first pattern override, then the setting.</summary>
        public static float Fraction(ItemDrop.ItemData item)
        {
            float fraction = SalvageSettings.ReturnFraction.Value;
            if (Current != null)
            {
                FractionOverride pattern = null;
                foreach (FractionOverride entry in Current.Overrides)
                {
                    if (!entry.Matches(item))
                        continue;
                    if (entry.IsExact)
                        return entry.Fraction;
                    pattern = pattern ?? entry;
                }
                if (pattern != null)
                    fraction = pattern.Fraction;
            }
            return fraction < 0f ? 0f : fraction;
        }

        /// <summary>Null when the item can be salvaged by this player, else the "$ok_..." word that says why not.</summary>
        public static string Blocker(Player player, ItemDrop.ItemData item)
        {
            if (!KindleSalvage.Ready || !SalvageSettings.Enabled.Value)
                return SalvageWords.Off;
            if (player == null || player != Player.m_localPlayer || item == null || !player.GetInventory().ContainsItem(item))
                return SalvageWords.Cannot;
            if (item == null || item.m_shared == null)
                return SalvageWords.Cannot;
            if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy || item.m_shared.m_questItem)
                return SalvageWords.Cannot;
            Recipe recipe = FindRecipe(item);
            if (recipe == null)
                return SalvageWords.NoRecipe;
            if (IsFavourite(item))
                return SalvageWords.Favourite;
            if (IsExcluded(item))
                return SalvageWords.Excluded;
            if (ModDataCheck.Carries(item))
                return SalvageWords.ModData;
            return PlayerBlocker(player, item, recipe);
        }

        private static string PlayerBlocker(Player player, ItemDrop.ItemData item, Recipe recipe)
        {
            if (item.m_equipped || (player != null && player.IsItemEquiped(item)))
                return SalvageWords.Equipped;
            if (SalvageSettings.RequireKnownRecipe.Value && player != null && !KnowsRecipe(player, item, recipe))
                return SalvageWords.Unknown;
            if (SalvageSettings.RequireStation.Value && player != null && !StationInRange(player, recipe))
                return SalvageWords.NoStation;
            return null;
        }

        /// <summary>
        /// Require Known Recipe. The game learns a recipe only from known materials and stations, so gear the player found
        /// (Inscribe loot and chest gear, trader stock, drops: no crafter, m_crafterID 0) is often of an unknown recipe and was
        /// hidden from the Salvage tab without a reason. Found gear counts as known; so does every recipe when the game itself
        /// lets the player craft unknown recipes (the AllRecipesUnlocked world key or the no-cost cheat, Player.GetAvailableRecipes).
        /// </summary>
        public static bool KnowsRecipe(Player player, ItemDrop.ItemData item, Recipe recipe)
        {
            if (player.IsRecipeKnown(recipe.m_item.m_itemData.m_shared.m_name) || item.m_crafterID == 0L || player.NoCostCheat())
                return true;
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.AllRecipesUnlocked);
        }

        /// <summary>The recipe's station is the one the player crafts at, or any station of that name within its build range.</summary>
        public static bool StationInRange(Player player, Recipe recipe)
        {
            CraftingStation wanted = recipe.m_craftingStation;
            if (wanted == null)
                return true;
            CraftingStation current = player.GetCurrentCraftingStation();
            if (current != null && current.m_name == wanted.m_name)
                return true;
            return CraftingStation.HaveBuildStationInRange(wanted.m_name, player.transform.position) != null;
        }
    }
}
