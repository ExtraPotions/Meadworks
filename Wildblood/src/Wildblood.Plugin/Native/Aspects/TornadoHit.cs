// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Whether a formed Nightfall tornado is tearing at this client's own player, judged here against the tornadoes
    /// as this player sees them, so stepping clear of a funnel on their own screen is clear: each client judges only
    /// its own player, and every player is judged on exactly one machine, their own. Inside means the funnel - its
    /// width at the height of the player's middle - touches their body, from a little below its foot to its top, so a
    /// player on a roof or a deck over it is caught too. Every <see cref="TickSeconds"/> seconds a player inside takes
    /// that share of the wave's `damage` a second, as lightning: scaled by the world's combat difficulty as any enemy
    /// hit is, then reduced by their lightning resistance and anything that shields them from hits (a protection
    /// staff's bubble), but not by armour, which would shrug off a few points a tick at any stage of the game. No
    /// stagger: it is a grinding hazard, not a blow. One funnel at a time, however many overlap; a dodge roll's
    /// invincible moment lets a player through unhurt, as for any dodgeable hit. Players only.
    /// </summary>
    internal static class TornadoHit
    {
        /// <summary>Seconds between damage ticks: four a second.</summary>
        public const float TickSeconds = 0.25f;

        /// <summary>How far below a funnel's foot a player is still in it.</summary>
        private const float Below = 1.5f;

        /// <summary>The height of a player's middle above their feet, where the funnel's width is measured.</summary>
        private const float Middle = 0.9f;

        public static void Judge(Character boss, List<Vector3> feet, TornadoShape shape, float damage)
        {
            Player player = Player.m_localPlayer;
            if (player == null || damage <= 0f || !Exposed(player))
            {
                return;
            }
            foreach (Vector3 foot in feet)
            {
                if (Inside(player, foot, shape))
                {
                    Tear(player, boss, damage);
                    return;
                }
            }
        }

        // Dead, teleporting, in a cutscene, a ghost, a debug flyer, or inside a dodge's invincible frames: untouched.
        private static bool Exposed(Player player) =>
            !player.IsDead() && !player.IsTeleporting() && !player.InCutscene() && !player.InGhostMode()
            && !player.IsDebugFlying() && !player.IsDodgeInvincible();

        private static bool Inside(Player player, Vector3 foot, TornadoShape shape)
        {
            Vector3 at = player.transform.position;
            float rise = at.y - foot.y;
            if (rise < -Below || rise > shape.Height)
            {
                return false;
            }
            float width = shape.RadiusAt((rise + Middle) / shape.Height);
            return StormTargets.FlatDistance(at, foot) <= width + player.GetRadius();
        }

        // On the player's own client, which owns them, so the damage is applied here directly.
        private static void Tear(Player player, Character boss, float damage)
        {
            HitData hit = Gust(player, boss, damage);
            player.GetSEMan().OnDamaged(hit, boss); // a protection bubble swallows it whole, as it does any hit
            hit.ApplyResistance(player.GetDamageModifiers(), out HitData.DamageModifier modifier);
            if (hit.GetTotalDamage() > 0f)
            {
                player.ApplyDamage(hit, showDamageText: true, triggerEffects: false, modifier);
            }
        }

        private static HitData Gust(Player player, Character boss, float damage)
        {
            HitData hit = new HitData();
            hit.m_damage.m_lightning = damage;
            hit.ApplyModifier(Game.m_enemyDamageRate); // the world's combat difficulty, as for any enemy hit
            hit.m_point = player.GetCenterPoint();
            hit.m_dir = Vector3.up;
            hit.m_hitType = HitData.HitType.EnemyHit;
            hit.m_staggerMultiplier = 0f; // lightning counts toward stagger; a tick of wind must not stun-lock
            hit.SetAttacker(boss);
            return hit;
        }
    }
}
