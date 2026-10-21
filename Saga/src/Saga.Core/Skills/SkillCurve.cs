// Saga - ExtraPotions Meadworks progression.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills 0.14.1, GPL-3.0-or-later.
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// GrindstoneSkills/GrindstoneSkills/Defense/Core/DefenseSkill.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Saga.Skills; Unity-free (System.Math); the level is passed in instead of read
// from the local player, so every Saga skill (not only Defense) uses the same curve; Lerp added for "At 0 / At 100"
// setting pairs.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Saga.Skills
{
    /// <summary>The scale every Saga perk grows on: linear from level 0 to its value at level 100.</summary>
    public static class SkillCurve
    {
        public const float MaxLevel = 100f;

        /// <summary>A level as a share of level 100, clamped to 0..1.</summary>
        public static float Factor(float level) => float.IsNaN(level) ? 0f : Math.Min(1f, Math.Max(0f, level / MaxLevel));

        /// <summary>A perk's share at a level: <paramref name="percentAt100"/> percent at level 100, linear from 0.</summary>
        public static float Share(float percentAt100, float level) => Math.Max(0f, percentAt100) / 100f * Factor(level);

        /// <summary>Whether a level reached a milestone setting; a milestone above 100 is off.</summary>
        public static bool Reached(float level, int milestone) => milestone <= MaxLevel && level >= milestone;

        /// <summary>The value between an "At 0" and an "At 100" setting at a level.</summary>
        public static float Lerp(float at0, float at100, float level) => at0 + (at100 - at0) * Factor(level);
    }
}
