// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The slam at the end of the pull, judged on this machine for its own player: a player within `slam radius` of the
    /// boss's body loses `slam damage` percent of their maximum health and staggers. The struck player's own client
    /// decides, because the pull that put them there ran on that client: its copy of their position is the one the pull
    /// produced, where the boss owner's copy trails it by the network delay - and at the end of a pull that delay is
    /// exactly what decides inside or out. The damage is the game's untyped kind, so it is the stated share whatever the
    /// armour, resistances or blocking (a shock through the ground is not caught on a shield) - scaled only by the
    /// world's Combat difficulty, as every enemy hit is - and it is dodgeable: a
    /// roll timed through the slam avoids the damage and the stagger both, by the game's own dodge window. It carries
    /// no attacker, so no star or aspect scaling multiplies it on the way in.
    /// </summary>
    internal static class GraviticSlam
    {
        public static void Strike(Vector3 anchor, float reach, float percent)
        {
            Player player = Player.m_localPlayer;
            if (player == null || percent <= 0f || !Within(player, anchor, reach) || player.IsDodgeInvincible())
            {
                return;
            }
            Vector3 away = Away(player, anchor);
            player.Damage(Hit(player, away, percent)); // to its own owner - this machine - so it lands at once
            if (!player.IsDead() && !player.IsAttached())
            {
                player.Stagger(away); // the game's own stagger: a short stumble, turned to face the boss
            }
        }

        private static bool Within(Player player, Vector3 anchor, float reach) =>
            !player.IsDead() && !player.InGhostMode() && !player.IsDebugFlying() && !player.IsTeleporting()
            && Vector3.Distance(anchor, player.transform.position) <= reach;

        private static Vector3 Away(Player player, Vector3 anchor)
        {
            Vector3 away = player.transform.position - anchor;
            away.y = 0f;
            return away.sqrMagnitude > 0.0001f ? away.normalized : -player.transform.forward;
        }

        private static HitData Hit(Player player, Vector3 away, float percent)
        {
            HitData hit = new HitData();
            // The world's Combat setting scales every enemy hit through its enemy damage rate, applied to typed damage
            // only - so it is applied here by hand, and an easy or hard world scales the slam like any other blow.
            hit.m_damage.m_damage = player.GetMaxHealth() * percent / 100f * Game.m_enemyDamageRate;
            hit.m_point = player.GetCenterPoint();
            hit.m_dir = away;
            hit.m_hitType = HitData.HitType.EnemyHit;
            hit.m_dodgeable = true;
            hit.m_blockable = false;
            hit.m_pushForce = 0f;
            return hit;
        }
    }
}
