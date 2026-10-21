// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// The local player's own extra hits (Thor's Chain arcs, Bursting Shot's blast): found and built on the attacker's
    /// client, dealt through Character.Damage so the game routes each to the target's owner, who applies resistances,
    /// armor and death as for any hit. While one is dealt the on-hit effects skip it (<see cref="Active"/>), so an arc
    /// never crits, chains or bursts again.
    /// </summary>
    internal static class SecondaryHits
    {
        private static readonly List<Character> Scratch = new List<Character>();
        private static readonly List<Character> Found = new List<Character>();

        public static bool Active { get; private set; }

        /// <summary>
        /// Up to <paramref name="max"/> live foes of <paramref name="attacker"/> within <paramref name="radius"/> of the
        /// point, nearest first, never <paramref name="exclude"/>. The list is reused: read it before the next call.
        /// </summary>
        public static List<Character> Near(Character attacker, Vector3 point, float radius, int max, Character? exclude)
        {
            Found.Clear();
            Scratch.Clear();
            Character.GetCharactersInRange(point, radius, Scratch);
            while (Found.Count < max)
            {
                Character? next = Closest(attacker, point, exclude);
                if (next == null)
                {
                    break;
                }
                Found.Add(next);
            }
            return Found;
        }

        private static Character? Closest(Character attacker, Vector3 point, Character? exclude)
        {
            Character? best = null;
            float bestDistance = float.MaxValue;
            foreach (Character c in Scratch)
            {
                if (!IsTarget(attacker, c, exclude) || Found.Contains(c))
                {
                    continue;
                }
                float distance = (c.transform.position - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = c;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static bool IsTarget(Character attacker, Character c, Character? exclude) =>
            c != null && !ReferenceEquals(c, exclude) && !ReferenceEquals(c, attacker) && !c.IsDead() && IsFoe(attacker, c);

        /// <summary>The game's own rule for a player's projectile: an enemy, or a neutral that can be aggravated.</summary>
        public static bool IsFoe(Character attacker, Character target)
        {
            BaseAI? ai = target.GetBaseAI();
            return BaseAI.IsEnemy(attacker, target) || (ai != null && ai.IsAggravatable());
        }

        /// <summary>A bare hit from the player on the target, coming from <paramref name="from"/>; the caller sets the damage.</summary>
        public static HitData NewHit(Player player, Character target, Skills.SkillType skill, Vector3 from)
        {
            HitData hit = new HitData();
            hit.m_point = target.GetCenterPoint();
            Vector3 dir = hit.m_point - from;
            dir.y = 0f;
            hit.m_dir = dir.sqrMagnitude > 0.001f ? dir.normalized : player.transform.forward;
            hit.m_skill = skill;
            hit.m_ranged = true;
            hit.m_hitType = HitData.HitType.PlayerHit;
            hit.SetAttacker(player);
            return hit;
        }

        public static void Deal(Character target, HitData hit)
        {
            bool was = Active;
            Active = true;
            try
            {
                target.Damage(hit);
            }
            finally
            {
                Active = was;
            }
        }
    }

    /// <summary>
    /// One of the game's networked effect prefabs (a ZNetView with TimedDestruction, registered in ZNetScene), spawned
    /// on this client: the game creates its ZDO, so every client near it sees it, and its owner destroys it when its
    /// time is up. The first name the running game knows is used.
    /// </summary>
    internal sealed class NetVisual
    {
        private readonly string[] _names;
        private GameObject? _prefab;

        public NetVisual(params string[] names)
        {
            _names = names;
        }

        public void Spawn(Vector3 at)
        {
            GameObject? prefab = Prefab();
            if (prefab != null)
            {
                Object.Instantiate(prefab, at, Quaternion.identity);
            }
        }

        private GameObject? Prefab()
        {
            if (_prefab != null || ZNetScene.instance == null)
            {
                return _prefab;
            }
            foreach (string name in _names)
            {
                _prefab = ZNetScene.instance.GetPrefab(name);
                if (_prefab != null)
                {
                    break;
                }
            }
            return _prefab;
        }
    }
}
