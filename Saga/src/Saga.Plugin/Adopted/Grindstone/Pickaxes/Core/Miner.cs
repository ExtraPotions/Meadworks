// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/Miner.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The player behind a pickaxe hit, read on the rock's owner from the hit itself (<see cref="FromHit"/>). A pickaxe
    /// swing's HitData already carries everything, as the game fills it on the miner's client (Attack.DoMeleeAttack):
    /// m_skill Pickaxes, m_skillLevel the weapon skill's level (so the miner's Pickaxes level, floored, with status
    /// effects), m_attacker the miner's player ZDOID and m_toolTier the pickaxe's tier. No tagging is needed.
    /// </summary>
    public sealed class Miner
    {
        public Miner(long playerId, float level, ZDOID attacker)
        {
            PlayerId = playerId;
            Level = Mathf.Clamp(level, 0f, PickSkill.MaxLevel);
            Attacker = attacker;
        }

        /// <summary>The miner's player ID, read from the attacker's ZDO; 0 when this machine does not have it.</summary>
        public long PlayerId { get; }

        /// <summary>The miner's Pickaxes level when the hit was made.</summary>
        public float Level { get; }

        /// <summary>The ZDOID of the miner's player character (HitData.m_attacker): always known, even when the player ID is not.</summary>
        public ZDOID Attacker { get; }

        /// <summary>The level as a share of level 100, 0..1.</summary>
        public float Factor => PickSkill.Factor(Level);

        /// <summary>The miner's player object, when it is loaded on this machine; null otherwise.</summary>
        public Player LoadedPlayer
        {
            get
            {
                GameObject found = ZNetScene.instance != null && !Attacker.IsNone() ? ZNetScene.instance.FindInstance(Attacker) : null;
                return found != null ? found.GetComponent<Player>() : null;
            }
        }

        /// <summary>The miner carries a cheated damaging item, as the game checks it before marking drops as cheated.</summary>
        public bool Cheated
        {
            get
            {
                Player player = LoadedPlayer;
                return player != null && player.GetInventory() != null && player.GetInventory().CheatedDamagingItemEquipped();
            }
        }

        /// <summary>The miner of a hit; null unless it is a Pickaxes hit.</summary>
        public static Miner FromHit(HitData hit)
        {
            if (hit == null || hit.m_skill != PickSkill.Skill)
                return null;
            return new Miner(PlayerIds.Of(hit.m_attacker), hit.m_skillLevel, hit.m_attacker);
        }
    }
}
