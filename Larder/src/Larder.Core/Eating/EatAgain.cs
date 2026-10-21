// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/GameplayRules.cs (EatAgainPatch).
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the Player.Food.CanEatAgain postfix became a pure rule over the time
// left and burn time; the Eat Anything cheat reads the fraction as 1.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Eating
{
    /// <summary>Eat Again At (design B2): a food can be eaten again below this share of its duration (game 0.5).</summary>
    public static class EatAgain
    {
        public const float GameFraction = 0.5f;

        /// <summary>
        /// Whether a food with <paramref name="timeLeft"/> of <paramref name="burnTime"/> seconds can be eaten again.
        /// The fraction clamps to 0..1; Eat Anything (cheat, design 4.10) reads it as 1.
        /// </summary>
        public static bool CanEatAgain(float timeLeft, float burnTime, float fraction, bool eatAnything)
        {
            float share = eatAnything ? 1f : LarderMath.Clamp01(fraction);
            return timeLeft < burnTime * share;
        }
    }
}
