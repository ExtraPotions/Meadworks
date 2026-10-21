// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Splash/SplashHits.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The splash hits themselves, on the rock's ZDO owner: one new pickaxe hit per touching chunk, applied with the
    /// game's own chunk damage (MineRock5.DamageArea) through <see cref="SplashMute"/>.
    /// <list type="bullet">
    /// <item><b>The hit</b> (<see cref="Build"/>) carries only pickaxe damage, the chunk's share. It carries the miner as
    /// every pickaxe hit does (m_skill Pickaxes, m_skillLevel, m_attacker), so <see cref="ChunkBreaks"/> gives its breaks
    /// to the miner with <see cref="BreakCause.Splash"/>; the hit's tool tier and item world level, so it passes the
    /// rock's tier check exactly when the hit it came from did; its direction and hit type; and the chunk's centre as
    /// its point (where the game draws effects and drops a chunk's items anyway).</item>
    /// <item><b>Resistances</b> are the rock's own: DamageArea applies m_damageModifiers to the hit (pickaxe damage is
    /// normal on every rock).</item>
    /// <item><b>Order</b> is by area index. Once the rock is gone (its last chunk broke: m_allDestroyed, or its ZDO is
    /// no longer valid) nothing more is applied.</item>
    /// </list>
    /// </summary>
    public static class SplashHits
    {
        /// <summary>
        /// Damages every chunk in <paramref name="touching"/> by <paramref name="share"/>; true when any of them broke.
        /// <paramref name="hit"/> is the owner's copy of the hit that splashed.
        /// </summary>
        public static bool Apply(Rock rock, List<int> touching, HitData hit, float share)
        {
            MineRock5 chunks = rock.Chunks5;
            bool broke = false;
            foreach (int area in touching)
            {
                if (Gone(rock))
                    break;
                if (SplashMute.DamageArea(chunks, area, Build(rock, area, hit, share)))
                    broke = true;
            }
            return broke;
        }

        /// <summary>The rock was destroyed on this machine (its last chunk broke) and takes no more damage.</summary>
        public static bool Gone(Rock rock) => rock.Chunks5 == null || rock.Chunks5.m_allDestroyed || !rock.IsValid;

        /// <summary>A pickaxe hit of <paramref name="share"/> on chunk <paramref name="area"/>, from the miner of <paramref name="hit"/>.</summary>
        public static HitData Build(Rock rock, int area, HitData hit, float share)
        {
            var splash = new HitData
            {
                m_point = RockChunks.Centre(rock, area),
                m_dir = hit.m_dir,
                m_attacker = hit.m_attacker,
                m_skill = PickSkill.Skill,
                m_skillLevel = hit.m_skillLevel,
                m_toolTier = hit.m_toolTier,
                m_itemWorldLevel = hit.m_itemWorldLevel,
                m_hitType = hit.m_hitType,
            };
            splash.m_damage.m_pickaxe = share;
            return splash;
        }
    }
}
