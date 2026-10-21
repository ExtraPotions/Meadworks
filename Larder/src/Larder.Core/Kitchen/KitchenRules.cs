// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/Cooking.cs, FeastMaster/FeastMaster/FeastMasterCore/Fermenting.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the per-call swaps of recipe times, the burn switch, the fermentation
// duration and the tap yield became pure value rules (the swap and restore stay in the plugin, L07).
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Kitchen
{
    /// <summary>
    /// Kitchen station values (design E1 to E4) that the Cooking perks compose with (design 1.3). These are the pure
    /// rules only; L07 owns their configuration and patches.
    /// </summary>
    public static class KitchenRules
    {
        /// <summary>Recipe time T: the station entry for the raw item (or the recipe's own time) times Cook Time Multiplier.</summary>
        public static float RecipeTime(float? stationEntry, float ownTime, float multiplier)
        {
            return (stationEntry ?? ownTime) * LarderMath.NonNegative(multiplier);
        }

        /// <summary>A slot can burn only where the station can overcook and Food Can Burn is on.</summary>
        public static bool CanBurn(bool stationCanOvercook, bool foodCanBurn) => stationCanOvercook && foodCanBurn;

        /// <summary>Fermentation duration D: Fermentation Time when above 0, else the barrel's own. Applies to barrels already brewing.</summary>
        public static float FermentationDuration(float barrelOwn, float configured) => configured > 0f ? configured : barrelOwn;

        /// <summary>Items a tap yields: Batch Yield when above 0, else the recipe's own.</summary>
        public static int BatchYield(int recipeOwn, int configured) => configured > 0 ? configured : recipeOwn;
    }
}
