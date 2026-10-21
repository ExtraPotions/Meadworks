// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GrindstoneSkills 0.14.1:
// GrindstoneSkills/GrindstoneSkills/Kitchen/Fermenter/FermenterSpeed.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the GetFermentationTime postfix became a pure scaling of the elapsed
// time; the readiness rule composes it with FeastMaster's fermentation duration (design 1.3).
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Cooking
{
    /// <summary>
    /// Fermenting speed (design F4) composed with the fermentation time (E3): the barrel stores only its start time, so
    /// readiness is decided from the elapsed time on every call; an edit reaches barrels already brewing, and a longer
    /// time returns a ready, untapped barrel to brewing.
    /// </summary>
    public static class FermentRules
    {
        /// <summary>The elapsed world seconds scaled by the base adder's fermenting speed; empty (0 or below) unchanged.</summary>
        public static double ScaledElapsed(double elapsed, float speed) => elapsed <= 0.0 ? elapsed : elapsed * speed;

        /// <summary>Ready when elapsed x speed reaches the duration D.</summary>
        public static bool Ready(double elapsed, float duration, float speed) => elapsed > 0.0 && ScaledElapsed(elapsed, speed) >= duration;
    }
}
