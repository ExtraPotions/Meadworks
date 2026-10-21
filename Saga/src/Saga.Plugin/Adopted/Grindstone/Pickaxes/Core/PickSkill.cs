// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/PickSkill.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The game's own Pickaxes skill (global::Skills.SkillType.Pickaxes, 12). A pickaxe swing trains it once per swing that hits
    /// rock, and its level scales the swing's damage. Levels are client-side like every skill; a miner's level reaches
    /// the rock's owner inside the hit itself (HitData.m_skillLevel is the weapon skill's level, so the Pickaxes level:
    /// <see cref="Miner"/>).
    /// </summary>
    public static class PickSkill
    {
        public const global::Skills.SkillType Skill = global::Skills.SkillType.Pickaxes;
        public const float MaxLevel = 100f;

        /// <summary>The master switch. Off, every Pickaxes feature stands aside and the game plays as vanilla.</summary>
        public static bool Active => PickaxeSettings.Enabled.Value;

        /// <summary>The local player's Pickaxes level, 0 when there is no local player.</summary>
        public static float Local()
        {
            Player player = Player.m_localPlayer;
            return player == null ? 0f : ExtraPotions.Meadworks.Crucible.MeadworksSkills.EffectiveLevel(player, Skill);
        }

        /// <summary>A level as a share of level 100, clamped to 0..1: the scale every perk grows on.</summary>
        public static float Factor(float level) => Mathf.Clamp01(level / MaxLevel);

        /// <summary>A perk's share at a level: <paramref name="percentAt100"/> percent at level 100, linear from 0.</summary>
        public static float Share(float percentAt100, float level) => Mathf.Max(0f, percentAt100) / 100f * Factor(level);

        /// <summary>A value that grows linearly from <paramref name="at0"/> at level 0 to <paramref name="at100"/> at level 100.</summary>
        public static float Between(float at0, float at100, float level) => Mathf.Lerp(at0, at100, Factor(level));

        /// <summary>Whether a level reached a milestone setting; a milestone above 100 is off.</summary>
        public static bool Reached(float level, float milestone) => milestone <= MaxLevel && level >= milestone;
    }
}
