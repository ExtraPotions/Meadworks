// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from OpenKeep 3.0.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, OpenKeep/OpenKeep/src/Recipes/RecipeNeeds.cs.
// OpenKeep - a Valheim mod. Copyright (C) 2026 Gerald Glasgow. This program is free software: you can redistribute
// it and/or modify it under the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Kindle; the rule reads a Unity-free RecipeMaterial (amount already taken at
// the listed quality); the "player stands at an upgrader" lookup moved to the plugin, which passes it in.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>
    /// Which of a recipe's materials count, by the game's own rule (InventoryGui.SetupRequirementList): at an upgrader
    /// station only the upgrader materials, anywhere else (another station, or by hand) only the others; and only those
    /// with an amount at the quality made.
    /// </summary>
    public static class RecipeNeeds
    {
        public static bool Counts(RecipeMaterial? material, bool atUpgrader)
        {
            return material != null && material.PrefabName.Length > 0 && material.UpgraderResource == atUpgrader
                && material.Amount > 0;
        }
    }
}
