// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Bestiary.Catalog
{
    /// <summary>What a station-bound recipe does while it is being filled.</summary>
    public enum RecipeStationOutcome
    {
        /// <summary>Bestiary is active and the station exists: the recipe is on.</summary>
        Enabled,
        /// <summary>Bestiary is off (switched off, not ready, or this is not the product's session): the recipe is off and nothing is wrong.</summary>
        Off,
        /// <summary>Bestiary is active but no station was found: the recipe is off and the log says so.</summary>
        NoStation,
    }

    public static class RecipeStationRule
    {
        /// <summary>A missing station is only a problem while the product is active; switched off, the recipe is off by design.</summary>
        public static RecipeStationOutcome Decide(bool active, bool stationFound)
        {
            if (!active)
            {
                return RecipeStationOutcome.Off;
            }
            return stationFound ? RecipeStationOutcome.Enabled : RecipeStationOutcome.NoStation;
        }
    }
}
