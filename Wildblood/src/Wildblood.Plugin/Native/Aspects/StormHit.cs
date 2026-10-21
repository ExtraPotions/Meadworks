// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Whether the lightning hits this client's own player, judged here against the position this player sees, so a
    /// dodge that clears the circle on their screen is a dodge: each client judges only its own player, and every
    /// player is judged on exactly one machine, their own. Inside means within the circle's radius on the ground plane
    /// and not far above or below it. A roll through the strike is safe too - its invincible frames count, as they do
    /// for any dodgeable hit - but a raised shield is not: the bolt comes from above. A struck player takes `damage`
    /// percent of their own maximum health as lightning, scaled by the world's combat difficulty as any enemy hit is,
    /// then reduced by their lightning resistance and anything that shields them from hits (a protection staff's
    /// bubble), but not by armour, which would reduce a small, fixed share of health to nothing at any stage of the
    /// game; then they stagger, unless they are swimming or seated, or nothing got through. One strike per storm,
    /// however many circles overlap. Creatures and tames are never struck: it is aimed at players.
    /// </summary>
    internal static class StormHit
    {
        /// <summary>How far below and above the circle a player still stands in its column.</summary>
        private const float Below = 2f;
        private const float Above = 4f;

        public static void Judge(Character boss, List<Vector3> centers, float radius, float percent)
        {
            Player player = Player.m_localPlayer;
            if (player == null || percent <= 0f || !Exposed(player))
            {
                return;
            }
            Vector3 at = player.transform.position;
            foreach (Vector3 center in centers)
            {
                if (Inside(at, center, radius))
                {
                    Strike(player, boss, center, percent);
                    return;
                }
            }
        }

        // Dead, teleporting, in a cutscene, a ghost, a debug flyer, or inside a dodge's invincible frames: it misses.
        private static bool Exposed(Player player) =>
            !player.IsDead() && !player.IsTeleporting() && !player.InCutscene() && !player.InGhostMode()
            && !player.IsDebugFlying() && !player.IsDodgeInvincible();

        private static bool Inside(Vector3 at, Vector3 center, float radius)
        {
            float rise = at.y - center.y;
            return rise >= -Below && rise <= Above && StormTargets.FlatDistance(at, center) <= radius;
        }

        // On the struck player's own client, which owns them, so the damage is applied here directly.
        private static void Strike(Player player, Character boss, Vector3 center, float percent)
        {
            HitData hit = Bolt(player, boss, percent);
            player.GetSEMan().OnDamaged(hit, boss); // a protection bubble swallows it whole, as it does any hit
            hit.ApplyResistance(player.GetDamageModifiers(), out HitData.DamageModifier modifier);
            if (hit.GetTotalDamage() <= 0f)
            {
                return; // absorbed, or immune to lightning: no damage and no stagger
            }
            player.ApplyDamage(hit, showDamageText: true, triggerEffects: true, modifier);
            if (!player.IsSwimming() && !player.IsAttached())
            {
                player.Stagger(StormTargets.Flat(player.transform.position - center));
            }
        }

        private static HitData Bolt(Player player, Character boss, float percent)
        {
            HitData hit = new HitData();
            hit.m_damage.m_lightning = player.GetMaxHealth() * percent / 100f;
            hit.ApplyModifier(Game.m_enemyDamageRate); // the world's combat difficulty, as for any enemy hit
            hit.m_point = player.GetCenterPoint();
            hit.m_dir = Vector3.down;
            hit.m_hitType = HitData.HitType.EnemyHit;
            hit.SetAttacker(boss);
            return hit;
        }
    }
}
