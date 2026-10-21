// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Farming/Core/KitchenUses.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// What the kitchens and mills do with items, read from the game's data when <see cref="KitchenCrops"/> discovers:
    /// every ingredient of a kitchen recipe (a CraftingStation whose skill is Cooking) with the best food value among
    /// the dishes it goes into (<see cref="Kitchen.FoodValue"/>, so an intermediate counts as what it becomes), every
    /// input of a kitchen cooking station or a fermenter, and every Smelter conversion (the windmill and spinning wheel,
    /// the smelters and kilns too). Rebuilt on each discovery.
    /// </summary>
    public static class KitchenUses
    {
        public struct Conversion
        {
            public string Mill;
            public ItemDrop From;
            public ItemDrop To;
        }

        private static readonly Dictionary<string, float> ingredients = new Dictionary<string, float>();
        private static readonly List<Conversion> conversions = new List<Conversion>();

        public static List<Conversion> Conversions => conversions;

        /// <summary>Whether a kitchen uses this item prefab as an ingredient or an input.</summary>
        public static bool IsIngredient(string itemPrefab) => itemPrefab != null && ingredients.ContainsKey(itemPrefab);

        /// <summary>The best food value among the kitchen dishes this item prefab goes into; 0 when none.</summary>
        public static float BestDish(string itemPrefab) => itemPrefab != null && ingredients.TryGetValue(itemPrefab, out float value) ? value : 0f;

        public static void Rebuild()
        {
            ingredients.Clear();
            conversions.Clear();
            foreach (Recipe recipe in ObjectDB.instance.m_recipes)
                AddRecipe(recipe);
            foreach (PrefabIndex.Converter converter in PrefabIndex.Scene().Converters)
                AddPrefab(converter);
        }

        private static void AddRecipe(Recipe recipe)
        {
            if (recipe?.m_item == null || recipe.m_resources == null || !Kitchen.IsKitchen(recipe.m_craftingStation))
                return;
            float value = Kitchen.FoodValue(recipe.m_item.m_itemData);
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement?.m_resItem != null)
                    Note(requirement.m_resItem.name, value);
            }
        }

        private static void AddPrefab(PrefabIndex.Converter converter)
        {
            CookingStation station = converter.Station;
            if (Kitchen.IsKitchen(station))
                foreach (CookingStation.ItemConversion conversion in station.m_conversion)
                    Note(conversion.m_from, conversion.m_to);
            Fermenter fermenter = converter.Fermenter;
            if (fermenter != null)
                foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
                    Note(conversion.m_from, conversion.m_to);
            Smelter smelter = converter.Smelter;
            if (smelter?.m_conversion != null)
                foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
                    AddConversion(converter.Prefab.name, conversion.m_from, conversion.m_to);
        }

        private static void Note(ItemDrop from, ItemDrop to)
        {
            if (from != null)
                Note(from.name, to != null ? Kitchen.FoodValue(to.m_itemData) : 0f);
        }

        private static void Note(string itemPrefab, float value)
        {
            ingredients.TryGetValue(itemPrefab, out float best);
            ingredients[itemPrefab] = Mathf.Max(best, value);
        }

        private static void AddConversion(string mill, ItemDrop from, ItemDrop to)
        {
            if (from != null && to != null)
                conversions.Add(new Conversion { Mill = mill, From = from, To = to });
        }
    }
}
