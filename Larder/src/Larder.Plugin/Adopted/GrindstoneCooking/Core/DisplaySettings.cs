// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Core/DisplaySettings.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
// Changed by expDARE, 2026: Larder carries no item stars, so the egg star icon setting is not adopted; the original
// default-off Fermenter Timer is the one local display preference.
using BepInEx.Configuration;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>Each player's own display preferences. Not synced.</summary>
    public static class DisplaySettings
    {
        public static CookingSetting<bool> FermenterTimer { get; private set; }

        public static void Initialize(ConfigFile config)
        {
            FermenterTimer = CookingSettings.Bind(config, "6 - Kitchen", "Fermenter Timer", false,
                "Shows remaining brew time on an accessible, sheltered fermenter while the Cooking module is active. Your local display preference; follows the current Kitchen duration and Cooking speed.", scope: ExtraPotions.Meadworks.Crucible.ConfigScope.Local);
        }
    }
}
