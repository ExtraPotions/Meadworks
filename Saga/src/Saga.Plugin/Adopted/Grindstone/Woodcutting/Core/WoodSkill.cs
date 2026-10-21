// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/WoodSkill.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The game's own Woodcutting skill (global::Skills.SkillType.WoodCutting, 13). Axes and battleaxes train Axes, but their
    /// attacks switch to Woodcutting for tree-type targets (Attack.m_specialHitSkill / m_specialHitType): standing
    /// trees, logs and every Destructible whose type is Tree (stumps, saplings, small trees, bushes, branches). Levels
    /// are client-side like every skill; a woodcutter's level reaches other machines only inside the hits and log ZDOs
    /// that need it (<see cref="WoodHit"/>).
    /// </summary>
    public static class WoodSkill
    {
        public const global::Skills.SkillType Skill = global::Skills.SkillType.WoodCutting;
        public const float MaxLevel = 100f;

        /// <summary>The master switch. Off, every Woodcutting feature stands aside and the game plays as vanilla.</summary>
        public static bool Active => WoodcuttingSettings.Enabled.Value;

        /// <summary>The local player's Woodcutting level, 0 when there is no local player.</summary>
        public static float Local()
        {
            Player player = Player.m_localPlayer;
            return player == null ? 0f : ExtraPotions.Meadworks.Crucible.MeadworksSkills.EffectiveLevel(player, Skill);
        }

        /// <summary>A level as a share of level 100, clamped to 0..1: the scale every perk grows on.</summary>
        public static float Factor(float level) => Mathf.Clamp01(level / MaxLevel);

        /// <summary>A perk's share at a level: <paramref name="percentAt100"/> percent at level 100, linear from 0.</summary>
        public static float Share(float percentAt100, float level) => Mathf.Max(0f, percentAt100) / 100f * Factor(level);

        /// <summary>Whether a hit target is wood: a tree, a log, or a Destructible of type Tree.</summary>
        public static bool IsWood(IDestructible target) =>
            target != null && (target.GetDestructibleType() & DestructibleType.Tree) != DestructibleType.None;

        /// <summary>The prefab name of a spawned object (its name without "(Clone)").</summary>
        public static string PrefabName(Component component) =>
            component == null ? "" : Utils.GetPrefabName(component.gameObject);
    }
}
