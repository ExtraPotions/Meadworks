// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/WorldRates.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the override rule became a pure function; refreshing the game's rates
// stays in the plugin.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Exertion
{
    /// <summary>
    /// World rate overrides (design D6): food rate, stamina rate, move stamina rate, stamina regen rate. An override
    /// above 0 replaces the world's value after every world rate update; 0 keeps the world's own.
    /// </summary>
    public static class WorldRates
    {
        public static float Effective(float world, float configured) => configured > 0f ? configured : world;
    }
}
