// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace Mantle.Tackle
{
    /// <summary>
    /// What counts as fishing bait: ammo of the fishing rod's own ammo type. The game's baits (FishingBait, FishingBaitForest and
    /// the rest) are equipable ammo (<c>ItemType.Ammo</c>), read from the game on 2026-10-09; non-equipable ammo of the rod's type
    /// counts too. Before the item database has the rod there is no type to compare, so only non-equipable ammo counts (the
    /// earlier fallback), and arrows and bolts are never taken for bait.
    /// </summary>
    public static class BaitRule
    {
        public static bool Matches(bool equipableAmmo, bool nonEquipableAmmo, string ammoType, string rodAmmoType)
        {
            if (!equipableAmmo && !nonEquipableAmmo)
                return false;
            if (string.IsNullOrEmpty(rodAmmoType))
                return nonEquipableAmmo;
            return ammoType == rodAmmoType;
        }
    }
}
