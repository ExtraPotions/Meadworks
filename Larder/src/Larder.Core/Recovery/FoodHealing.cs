// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/HealthRegen.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the UpdateFood prefix's heal amount became a pure function.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Recovery
{
    /// <summary>
    /// Continuous food healing (design C1): the game heals the active foods' summed health regen once every 10 s; with
    /// the setting on the same amount is spread over every frame (no floating numbers).
    /// </summary>
    public static class FoodHealing
    {
        public const float TickSeconds = 10f;

        /// <summary>Health healed this frame: the per-tick amount (times the health regen multiplier) times dt / 10.</summary>
        public static float PerFrame(float totalFoodRegen, float healthRegenMultiplier, float dt)
        {
            if (dt <= 0f || totalFoodRegen <= 0f)
                return 0f;
            return totalFoodRegen * healthRegenMultiplier * dt / TickSeconds;
        }
    }
}
