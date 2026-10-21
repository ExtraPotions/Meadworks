// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Core/FishSkill.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The game's own Fishing skill (global::Skills.SkillType.Fishing, 104). The game reads it only while reeling (FishingFloat):
    /// line speed from 2 to 6 m/s, and the stamina reeling and holding a fish cost, down to a fifth at 100. Levels are
    /// client-side like every skill; an angler's level reaches the owners of the fish near their float on the float's
    /// ZDO (<see cref="Angler"/>). The fishing hat's +20 counts, as it does in the game's own level read.
    /// </summary>
    public static class FishSkill
    {
        public const global::Skills.SkillType Skill = global::Skills.SkillType.Fishing;
        public const float MaxLevel = 100f;

        /// <summary>The master switch. Off, every Fishing feature stands aside and the game plays as vanilla.</summary>
        public static bool Active => FishingSettings.Enabled.Value;

        /// <summary>The local player's Fishing level, 0 when there is no local player.</summary>
        public static float Local() => Of(Player.m_localPlayer);

        /// <summary>A player's Fishing level on their own client (equipment included); 0 for no player.</summary>
        public static float Of(Player player) => player == null ? 0f : ExtraPotions.Meadworks.Crucible.MeadworksSkills.EffectiveLevel(player, Skill);

        /// <summary>A level as a share of level 100, clamped to 0..1: the scale every perk grows on.</summary>
        public static float Factor(float level) => Mathf.Clamp01(level / MaxLevel);

        /// <summary>A perk's share at a level: <paramref name="percentAt100"/> percent at level 100, linear from 0.</summary>
        public static float Share(float percentAt100, float level) => Mathf.Max(0f, percentAt100) / 100f * Factor(level);

        /// <summary>A value that grows linearly from <paramref name="at0"/> at level 0 to <paramref name="at100"/> at level 100.</summary>
        public static float Between(float at0, float at100, float level) => Mathf.Lerp(at0, at100, Factor(level));

        /// <summary>Whether a level reached a milestone setting; a milestone above 100 is off.</summary>
        public static bool Reached(float level, float milestone) => milestone <= MaxLevel && level >= milestone;

        /// <summary>A percent setting as a share, never below 0.</summary>
        public static float Percent(float value) => Mathf.Max(0f, value) / 100f;
    }
}
