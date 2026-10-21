// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// Whether the kraken's blows catch the player on this machine, judged the moment each lands against what this
    /// machine shows: a tentacle's length where it lies, the beak where it snaps, the ink where it flies. So a player
    /// who sees themselves step or roll clear is clear. A blow that catches them goes through the game's own damage for
    /// that player (with the kraken as the attacker), where a dodge's invincibility, a block and a parry work as always
    /// (and a block or a parry keeps the ink out of their eyes). Other machines judge their own players.
    /// </summary>
    public static class KrakenStrikes
    {
        private const float Body = 0.45f;          // a player's reach sideways from the middle of their body
        private const float Feet = 0.3f, Head = 1.6f;
        private const float BiteReach = 2.1f;      // metres from the beak's tip to the middle of a body
        private const float SlamPush = 60f, SmashPush = 90f, BitePush = 40f, InkPush = 10f;

        /// <summary>
        /// A tentacle has come down in <paramref name="points"/> (the pose it lies in). Returns the player on this machine
        /// if it caught them and they were not rolling clear (a grab then takes them), else null.
        /// </summary>
        public static Player? Slam(Character kraken, Vector3[] points, float scale, StrikeKind kind)
        {
            Player? player = Target();
            if (player == null || !Touches(player, points, scale, out Vector3 nearest))
            {
                return null;
            }
            bool rolling = player.IsDodgeInvincible();
            HitData hit = Hit(kraken, player, nearest, kind == StrikeKind.Slam ? SlamPush : kind == StrikeKind.Smash ? SmashPush : 0f);
            hit.m_damage.m_blunt = KrakenSettings.SlamDamage;
            player.Damage(hit);
            Heard(kraken, player, 0);
            return rolling ? null : player;
        }

        /// <summary>The beak has snapped shut at <paramref name="mouth"/>.</summary>
        public static void Bite(Character kraken, Vector3 mouth, float scale)
        {
            Player? player = Target();
            if (player == null || Vector3.Distance(player.GetCenterPoint(), mouth) > BiteReach * scale)
            {
                return;
            }
            HitData hit = Hit(kraken, player, mouth, BitePush);
            hit.m_damage.m_pierce = KrakenSettings.BiteDamage;
            player.Damage(hit);
            Heard(kraken, player, 1);
        }

        /// <summary>The ink has reached the player on this machine, flying along <paramref name="direction"/>.</summary>
        public static void Ink(Character kraken, Player player, Vector3 direction)
        {
            HitData hit = Hit(kraken, player, player.GetCenterPoint() - direction, InkPush);
            hit.m_dir = direction.normalized;
            hit.m_damage.m_blunt = KrakenSettings.InkDamage;
            hit.m_statusEffectHash = InkStatus.Hash;
            player.Damage(hit);
            Heard(kraken, player, 2);
        }

        // A blow that really landed (not rolled through, not taken on a raised shield) is heard by everyone.
        private static void Heard(Character kraken, Player player, int what)
        {
            if (!player.IsDodgeInvincible() && !player.IsBlocking() && kraken.TryGetComponent(out KrakenAttacks attacks))
            {
                attacks.Struck(player.GetCenterPoint(), what);
            }
        }

        /// <summary>The player on this machine, if alive.</summary>
        public static Player? Target()
        {
            Player? player = Player.m_localPlayer;
            return player != null && !player.IsDead() && !player.IsTeleporting() ? player : null;
        }

        // Any part of the tentacle above the water within reach of the line up the player's body.
        private static bool Touches(Player player, Vector3[] points, float scale, out Vector3 nearest)
        {
            Vector3 feet = player.transform.position + Vector3.up * Feet, head = player.transform.position + Vector3.up * Head;
            float water = KrakenShips.Water(player.transform.position) - 0.5f;
            nearest = points[0];
            for (int i = 0; i < TentacleSpec.Bones; i++)
            {
                if (points[i].y < water && points[i + 1].y < water)
                {
                    continue;
                }
                float reach = TentacleSpec.Radius(TentacleSpec.Share(i)) * scale + Body;
                if (Segments.Distance(feet, head, points[i], points[i + 1], out Vector3 on) <= reach)
                {
                    nearest = on;
                    return true;
                }
            }
            return false;
        }

        // The knock carries the player away from the blow, level; blocking works facing the blow.
        private static HitData Hit(Character kraken, Player player, Vector3 from, float push)
        {
            Vector3 away = player.GetCenterPoint() - from;
            away.y = 0f;
            var hit = new HitData
            {
                m_point = player.GetCenterPoint(), m_dir = away.sqrMagnitude > 1e-4f ? away.normalized : kraken.transform.forward,
                m_pushForce = push, m_dodgeable = true, m_blockable = true, m_hitType = HitData.HitType.EnemyHit,
            };
            hit.SetAttacker(kraken);
            return hit;
        }
    }
}
