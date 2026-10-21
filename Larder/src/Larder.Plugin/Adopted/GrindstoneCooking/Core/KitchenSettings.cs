// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Core/KitchenSettings.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using BepInEx.Configuration;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Section 4: the kitchen perks. Perks grow linearly from nothing at Cooking level 0 to the configured value at
    /// level 100. Synced.
    /// </summary>
    public static class KitchenSettings
    {
        public const string Section = "7 - Cooking";

        public static CookingSetting<float> CookingSpeed { get; private set; }
        public static CookingSetting<float> ExtraBurnTime { get; private set; }
        public static CookingSetting<float> FermentingSpeed { get; private set; }
        public static CookingSetting<float> ExtraFoodChance { get; private set; }
        public static CookingSetting<int> ExtraFoodAmount { get; private set; }
        public static CookingSetting<float> IngredientSaveChance { get; private set; }

        public static void Initialize(ConfigFile config)
        {
            CookingSpeed = CookingSettings.Bind(config, Section, "Cooking Speed At Level 100", 50f,
                "Percent faster cooking on cooking stations and the oven, from the level of the player who put the food on.",
                acceptableValues: Settings.UpTo(500f));
            ExtraBurnTime = CookingSettings.Bind(config, Section, "Extra Burn Time At Level 100", 100f,
                "Extra time before a finished dish burns, in percent of its cook time. The game burns a dish at twice its cook time; 100 moves that to three times.",
                acceptableValues: Settings.UpTo(1000f));
            FermentingSpeed = CookingSettings.Bind(config, Section, "Fermenting Speed At Level 100", 30f,
                "Percent faster fermenting, from the level of the player who put the mead base in.", acceptableValues: Settings.UpTo(500f));
            ExtraFoodChance = CookingSettings.Bind(config, Section, "Extra Food Chance At Level 100", 50f,
                "Chance in percent of extra food from a kitchen (cooking stations, oven, cauldron, mead cauldron, prep table). " +
                "Replaces the game's own bonus chance at kitchens. Grows from 0 at level 0.", acceptableValues: Settings.UpTo(100f));
            ExtraFoodAmount = CookingSettings.Bind(config, Section, "Extra Food Amount", 1,
                "How many extra dishes a bonus gives.", acceptableValues: new AcceptableValueRange<int>(1, 10));
            IngredientSaveChance = CookingSettings.Bind(config, Section, "Ingredient Save Chance At Level 100", 10f,
                "Chance in percent that crafting at a kitchen gives back one of the ingredients it used.", acceptableValues: Settings.UpTo(100f));
        }
    }
}
