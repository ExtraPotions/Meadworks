// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Kindle.Repair
{
    /// <summary>What decides whether one worn item can be repaired at the station the player stands at.</summary>
    public struct RepairFacts
    {
        public bool CanBeRepaired;
        /// <summary>The no-cost cheat repairs anything repairable, station or not.</summary>
        public bool NoCost;
        /// <summary>Name of the station in use; null when the player stands at none.</summary>
        public string? Station;
        public int StationLevel;
        public bool HasRecipe;
        public string? CraftingStation;
        public string? RepairStation;
        public int MinStationLevel;
        public int ItemWorldLevel;
        public int GameWorldLevel;
    }

    /// <summary>
    /// The game's repair rule (behavior of <c>InventoryGui.CanRepair</c>, Valheim 1.0.17): an item made or repaired at
    /// this station type, or one from a lower world level, is repaired when the station's level (counted up to 4)
    /// reaches the recipe's minimum level. Repair All repairs exactly the items this rule accepts.
    /// </summary>
    public static class RepairRule
    {
        public const int LevelCap = 4;

        public static bool Qualifies(in RepairFacts item)
        {
            if (!item.CanBeRepaired) return false;
            if (item.NoCost) return true;
            if (item.Station == null || !item.HasRecipe) return false;
            if (item.CraftingStation == null && item.RepairStation == null) return false;
            bool here = item.RepairStation == item.Station || item.CraftingStation == item.Station
                || item.ItemWorldLevel < item.GameWorldLevel;
            return here && System.Math.Min(item.StationLevel, LevelCap) >= item.MinStationLevel;
        }
    }
}
