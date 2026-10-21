// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GrindstoneSkills 0.14.1:
// GrindstoneSkills/GrindstoneSkills/Core/Perks.cs, GrindstoneSkills/GrindstoneSkills/Core/CookLevel.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; each perk takes its configured percent at level 100 as an argument
// instead of reading a BepInEx entry; the defaults of design 4.8 are constants here; the level factor moved in.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Cooking
{
    /// <summary>
    /// Cooking perks (design F1 to F6) on the game's own Cooking skill: each grows linearly from nothing at level 0 to
    /// its configured value at level 100. The game's own shorter crafting stays.
    /// </summary>
    public static class CookingPerks
    {
        public const float MaxLevel = 100f;

        public const float DefaultCookingSpeed = 50f;
        public const float DefaultExtraBurnTime = 100f;
        public const float DefaultFermentingSpeed = 30f;
        public const float DefaultExtraFoodChance = 50f;
        public const int DefaultExtraFoodAmount = 1;
        public const float DefaultIngredientSaveChance = 10f;

        /// <summary>A level as a share of level 100, clamped to 0..1: the scale every perk grows on.</summary>
        public static float Factor(float level) => LarderMath.Clamp01(level / MaxLevel);

        /// <summary>How much faster food cooks: 1 is the game's speed, 1.5 is 50 % faster.</summary>
        public static float CookingSpeed(float percentAt100, float level) => 1f + Share(percentAt100, level);

        /// <summary>Extra time before a done dish burns, as a share of its cook time: 1 moves burning from 2T to 3T.</summary>
        public static float ExtraBurnTime(float percentAt100, float level) => Share(percentAt100, level);

        /// <summary>How much faster a mead ferments: 1 is the game's speed.</summary>
        public static float FermentingSpeed(float percentAt100, float level) => 1f + Share(percentAt100, level);

        /// <summary>The chance, 0..1, of extra food at a kitchen (never the fermenter); replaces the game's 25 % x skill factor.</summary>
        public static float ExtraFoodChance(float percentAt100, float level) => LarderMath.Clamp01(Share(percentAt100, level));

        /// <summary>Extra food per bonus, at least 1.</summary>
        public static int ExtraFoodAmount(int configured) => configured < 1 ? 1 : configured;

        /// <summary>The chance, 0..1, that a crafted batch at a kitchen gives back one ingredient.</summary>
        public static float IngredientSaveChance(float percentAt100, float level) => LarderMath.Clamp01(Share(percentAt100, level));

        private static float Share(float percentAt100, float level) => LarderMath.NonNegative(percentAt100) / 100f * Factor(level);
    }
}
