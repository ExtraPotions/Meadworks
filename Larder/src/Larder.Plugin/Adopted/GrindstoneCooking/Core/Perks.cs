// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Core/Perks.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>The kitchen perks at a Cooking level, each growing linearly from nothing at 0 to section 4's value at 100.</summary>
    public static class Perks
    {
        /// <summary>How much faster food cooks: 1 is the game's speed, 1.5 is 50% faster.</summary>
        public static float CookingSpeed(float level) => 1f + Share(KitchenSettings.CookingSpeed.Value, level);

        /// <summary>Extra time before a done dish burns, as a share of its cook time: 1 moves burning from 2x to 3x.</summary>
        public static float ExtraBurnTime(float level) => Share(KitchenSettings.ExtraBurnTime.Value, level);

        /// <summary>How much faster a mead ferments: 1 is the game's speed.</summary>
        public static float FermentingSpeed(float level) => 1f + Share(KitchenSettings.FermentingSpeed.Value, level);

        /// <summary>The chance, 0..1, of extra food from a kitchen.</summary>
        public static float ExtraFoodChance(float level) => Mathf.Clamp01(Share(KitchenSettings.ExtraFoodChance.Value, level));

        public static int ExtraFoodAmount => Mathf.Max(1, KitchenSettings.ExtraFoodAmount.Value);

        /// <summary>The chance, 0..1, that kitchen crafting gives back one ingredient.</summary>
        public static float IngredientSaveChance(float level) => Mathf.Clamp01(Share(KitchenSettings.IngredientSaveChance.Value, level));

        private static float Share(float percentAt100, float level) => Mathf.Max(0f, percentAt100) / 100f * CookLevel.Factor(level);
    }
}
