// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/FoodDegradation.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the strength takes the time left and burn time as numbers (no
// Player.Food), folds the "disable degradation" switch in, and the recompute returns faded values instead of
// writing the food; the install test became Changes.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Larder.Foods;

namespace ExtraPotions.Larder.Eating
{
    /// <summary>
    /// Food degradation (design B1): the game fades each food's health, stamina and eitr as full value x (remaining
    /// fraction ^ 0.3). Larder recomputes them with the configured curve, or keeps them full when degradation is off;
    /// the HUD food bars read the same values.
    /// </summary>
    public static class FoodDegradation
    {
        public const float GameCurve = 0.3f;

        /// <summary>The food's strength 0..1: 1 when degradation is off or the curve is 0, else remaining ^ curve.</summary>
        public static float Strength(float timeLeft, float burnTime, float curve, bool disabled)
        {
            if (disabled)
                return 1f;
            curve = LarderMath.NonNegative(curve);
            if (curve <= 0f)
                return 1f;
            float remaining = burnTime > 0f ? LarderMath.Clamp01(timeLeft / burnTime) : 0f;
            return (float)Math.Pow(remaining, curve);
        }

        /// <summary>Whether the settings differ from the game's fading (the patch group is needed).</summary>
        public static bool Changes(bool disabled, float curve) => disabled || !LarderMath.Approximately(LarderMath.NonNegative(curve), GameCurve);

        /// <summary>The faded health, stamina and eitr at a strength; duration and regen are not faded.</summary>
        public static FoodStats Faded(FoodStats full, float strength)
        {
            return new FoodStats(full.Health * strength, full.Stamina * strength, full.Eitr * strength, full.Duration, full.HealthRegen);
        }
    }
}
