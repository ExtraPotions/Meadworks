// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/WoodTarget.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A piece of wood a swing hit, as the swinging client sees it: its prefab, the tool tier it needs and its base
    /// health (before the world level bonus). Standing trees, logs and tree-type Destructibles each carry both fields.
    /// </summary>
    public sealed class WoodTarget
    {
        private WoodTarget(string prefab, int tier, float health)
        {
            Prefab = prefab;
            Tier = Mathf.Max(0, tier);
            Health = Mathf.Max(0f, health);
        }

        public string Prefab { get; }
        public int Tier { get; }
        public float Health { get; }

        /// <summary>The target a hit landed on; null for anything that is not wood.</summary>
        public static WoodTarget Of(Component target)
        {
            switch (target)
            {
                case TreeBase tree:
                    return new WoodTarget(WoodSkill.PrefabName(tree), tree.m_minToolTier, tree.m_health);
                case TreeLog log:
                    return new WoodTarget(WoodSkill.PrefabName(log), log.m_minToolTier, log.m_health);
                case Destructible stump when WoodSkill.IsWood(stump):
                    return new WoodTarget(WoodSkill.PrefabName(stump), stump.m_minToolTier, stump.m_health);
                default:
                    return null;
            }
        }

        /// <summary>Harder means a higher tool tier, then more health.</summary>
        public bool HarderThan(WoodTarget other) =>
            other == null || Tier > other.Tier || (Tier == other.Tier && Health > other.Health);
    }
}
