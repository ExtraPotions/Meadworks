// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace Mantle.Accessories
{
    /// <summary>
    /// The built-in registry entries: the six rings and three belts Mantle makes (the user's approved "Biome tiers" set,
    /// 2026-10-09, one modest stat each), and the game's own items it tags into a slot (the Megingjord, which moves to the
    /// belt slot and no longer fits a utility slot). A new ring or belt is one more entry here, a tag one more line, with no
    /// slot code; Mantle.Accessories.yml changes any of them, or tags more items, without a rebuild. The station levels were
    /// not part of the approved table and are a first guess that follows the progression of the ingredients.
    /// </summary>
    public static class AccessoryCatalog
    {
        private const string Workbench = "piece_workbench";
        private const string Forge = "forge";
        private const string GaldrTable = "piece_magetable";

        public static IReadOnlyList<AccessoryEntry> All { get; } = new List<AccessoryEntry>
        {
            Mantles("Mantle_BoneBand", AccessorySlot.Ring, "Bone Band", "A band of polished bone, bound with a strip of leather.", 0.2f,
                Workbench, 1, "BoneFragments:6, LeatherScraps:2", new AccessoryStats(maxStamina: 5f)),
            Mantles("Mantle_CopperRing", AccessorySlot.Ring, "Copper Ring", "A plain copper ring set with a chip of amber.", 0.2f,
                Forge, 1, "Copper:3, Amber:1", new AccessoryStats(maxHealth: 10f)),
            Mantles("Mantle_IronRing", AccessorySlot.Ring, "Iron Ring", "A heavy iron ring, its inside smeared with blood.", 0.3f,
                Forge, 2, "Iron:3, Bloodbag:2", new AccessoryStats(staminaRegenPercent: 10f)),
            Mantles("Mantle_SilverRing", AccessorySlot.Ring, "Silver Ring", "A silver ring as cold as the glands it was cooled in.", 0.2f,
                Forge, 3, "Silver:3, FreezeGland:2", new AccessoryStats(moveSpeedPercent: 5f)),
            Mantles("Mantle_BlackMetalRing", AccessorySlot.Ring, "Black Metal Ring", "A black metal ring stitched through with needle wire.", 0.3f,
                Forge, 4, "BlackMetal:3, Needle:3", new AccessoryStats(attackStaminaPercent: -10f)),
            Mantles("Mantle_EitrRing", AccessorySlot.Ring, "Eitr Ring", "A silver ring that hums with refined eitr.", 0.2f,
                GaldrTable, 1, "Eitr:3, Silver:2", new AccessoryStats(maxEitr: 15f)),
            Mantles("Mantle_LeatherBelt", AccessorySlot.Belt, "Leather Belt", "A sturdy belt of deer hide and leather scraps.", 1f,
                Workbench, 1, "DeerHide:4, LeatherScraps:4", new AccessoryStats(carry: 25f)),
            Mantles("Mantle_TrollHideBelt", AccessorySlot.Belt, "Troll Hide Belt", "A thick troll hide belt with a bronze buckle.", 1.5f,
                Workbench, 2, "TrollHide:3, Bronze:1", new AccessoryStats(carry: 40f)),
            Mantles("Mantle_LoxBelt", AccessorySlot.Belt, "Lox Belt", "A broad belt of lox pelt, reinforced with black metal.", 2f,
                Workbench, 3, "LoxPelt:3, BlackMetal:2", new AccessoryStats(carry: 60f, maxStamina: 5f)),
            // The Megingjord: the game's own belt, tagged into the belt slot, where it is the strongest belt (+150 carry as in the game).
            new AccessoryEntry("BeltStrength", AccessorySlot.Belt, new AccessoryStats(carry: 150f)),
        };

        private static AccessoryEntry Mantles(string id, AccessorySlot slot, string name, string look, float weight, string station, int level, string cost, AccessoryStats stats) =>
            new AccessoryEntry(id, slot, stats, new AccessoryRecipe(station, level, cost), name, look, weight, builtIn: true);
    }
}
