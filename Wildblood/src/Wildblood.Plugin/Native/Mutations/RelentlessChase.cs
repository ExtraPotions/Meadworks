// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// What holding a quarry changes in the game's own monster AI, on the owner, once per target update. Each change is
    /// the smallest one that switches off one way the game lets a hunter give up, so everything else - how it moves,
    /// attacks, flees at low health, fears fire, smashes a wall it cannot get past - stays the game's:
    /// <list type="bullet">
    /// <item>"not sensed for 30 s", and the leash that drops a target a second after losing it once the creature is past
    /// its maximum chase distance from its spawn point, both run on the time since it last sensed its target: held at 0.</item>
    /// <item>"no attack for 60 s" drops a target unless the creature hunts players; "no attack for 30 s while being hurt"
    /// makes it run away. Both run on the time since it last attacked, which is held at 30 s. The 15 s mark that sends a
    /// creature that cannot reach its target after the buildings in the way still passes, so it still breaks in.</item>
    /// <item>A tame drops any target further than its alert range from the player it follows or the spot it guards; a
    /// tame's quarry is put back, so it hunts its enemies down wherever they run (never a friend: a friend is no quarry).</item>
    /// <item>A raider whose raid is over walks off and vanishes as soon as it has no target and is not alert; a hunting one
    /// stays alert, so the moment it spends on a wall or away from a fire never lets the game send it home.</item>
    /// <item>Out of sight and out of earshot the game walks to where it last saw the target and searches there; the quarry
    /// is reported as heard instead, so it keeps homing in on where the quarry is now.</item>
    /// </list>
    /// </summary>
    public static class RelentlessChase
    {
        /// <summary>The ceiling on its time since it last attacked: past the 15 s "cannot reach it" mark, short of 30 and 60.</summary>
        private const float PatienceCap = 30f;

        public static void HoldOn(MonsterAI ai, Character quarry)
        {
            ai.m_timeSinceSensedTargetCreature = 0f;
            ai.m_timeSinceAttacking = Mathf.Min(ai.m_timeSinceAttacking, PatienceCap);
            KeepTamed(ai, quarry);
            KeepAlert(ai);
        }

        /// <summary>The game drops lava-bound targets for creatures that avoid lava; a quarry in lava waits until it leaves.</summary>
        public static bool Targetable(MonsterAI ai, Character quarry) => !(ai.m_skipLavaTargets && quarry.AboveOrInLava());

        /// <summary>The "can hear its target" the game should act on: true while it chases a quarry it neither hears nor sees.</summary>
        public static bool Track(MonsterAI ai, Character quarry, bool heard, bool seen)
        {
            if (heard || seen || ai.m_targetCreature != quarry)
            {
                return heard;
            }
            if (quarry.IsPlayer())
            {
                quarry.OnTargeted(sensed: true, alerted: ai.IsAlerted()); // the player's own stealth indicator: it has them
            }
            return true;
        }

        /// <summary>
        /// The path it takes while hunting. A body the game lets swim, and that water does not hurt, gets the swimming path
        /// of its own size, so it follows its quarry into the water instead of stopping at the shore. Any other body keeps
        /// its own: a non-swimmer on a no-swim path already walks the bottom where the ground allows, and one kept out of
        /// the water by its path would only sink or take water damage there.
        /// </summary>
        public static Pathfinding.AgentType SwimmingPath(Character body, Pathfinding.AgentType path)
        {
            if (!body.m_canSwim || !body.m_tolerateWater)
            {
                return path;
            }
            switch (path)
            {
                case Pathfinding.AgentType.HumanoidNoSwim:
                case Pathfinding.AgentType.HumanoidAvoidWater:
                    return Pathfinding.AgentType.Humanoid; // the same build size, water included
                case Pathfinding.AgentType.HumanoidBigNoSwim:
                    return Pathfinding.AgentType.HumanoidBig;
                default:
                    return path; // every other path already crosses water
            }
        }

        private static void KeepTamed(MonsterAI ai, Character quarry)
        {
            if (!ai.m_character.IsTamed() || ai.m_targetCreature != null || ai.m_targetStatic != null || !Targetable(ai, quarry))
            {
                return;
            }
            ai.m_targetCreature = quarry;
            ai.SetTargetInfo(quarry.GetZDOID()); // so every client's health bar shows it as on the hunt
        }

        private static void KeepAlert(MonsterAI ai)
        {
            if (ai.m_targetCreature == null && !ai.IsAlerted() && ai.IsEventCreature() && !RandEventSystem.HaveActiveEvent())
            {
                ai.SetAlerted(true);
            }
        }
    }
}
