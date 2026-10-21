// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Core/CookLevel.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// The game's own Cooking skill (Skills.SkillType.Cooking, 105). Levels are client-side like every skill: a
    /// cook's level reaches other machines only inside the RPCs that need it.
    /// </summary>
    public static class CookLevel
    {
        public const Skills.SkillType Skill = Skills.SkillType.Cooking;
        public const float MaxLevel = 100f;

        /// <summary>The local player's Cooking level, 0 when there is no local player.</summary>
        public static float Local()
        {
            Player player = Player.m_localPlayer;
            return player == null ? 0f : player.GetSkillLevel(Skill);
        }

        /// <summary>A level as a share of level 100, clamped to 0..1: the scale every perk grows on.</summary>
        public static float Factor(float level) => Mathf.Clamp01(level / MaxLevel);
    }
}
