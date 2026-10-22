// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace Mantle
{
    /// <summary>
    /// Which of the plugin's Harmony patch classes are applied (<see cref="Plugin"/> asks for every type). Of the crafting
    /// module only the recipes (<c>Mantle.Crafting.CraftRecipes</c>) and the in-place upgrades of a worn backpack or
    /// tacklebox (<c>Mantle.Crafting.InPlaceUpgrade</c>, parity choice #83) are patches; the Inscribe link patches only the craft that carries a magic pack's inscriptions over
    /// (<c>Mantle.Elite.MagicCarryOver</c>). The crafting panel (<c>Mantle.Panels.CraftingPanelPatch</c>) runs, and the panel
    /// skin comes from the Crucible.UI kit, which patches itself. A namespace matches exactly or through its nested types.
    /// </summary>
    public static class PatchAdmission
    {
        private static readonly string[] Namespaces =
        {
            "Mantle.Layout", "Mantle.Worn", "Mantle.Backpacks", "Mantle.Slots",
            "Mantle.Consume", "Mantle.Ring", "Mantle.Tackle", "Mantle.Accessories", "Mantle.Panels", "Mantle.Elite", "Mantle.Trinkets",
        };

        public static bool Allows(string fullName)
        {
            string name = fullName ?? "";
            foreach (string space in Namespaces)
            {
                if (name.StartsWith(space + "."))
                    return true;
            }
            return name.StartsWith("Mantle.Core.PlayerTick") || name.StartsWith("Mantle.Core.LanguageSetupPatch") || name.StartsWith("Mantle.Crafting.CraftRecipes")
                || name.StartsWith("Mantle.Crafting.InPlaceUpgrade");
        }
    }
}
