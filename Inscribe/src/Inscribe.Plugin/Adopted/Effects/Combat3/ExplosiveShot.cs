// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// <c>explosive_shot</c> (Bursting Shot): a projectile from this bow or crossbow bursts where it lands; every other
    /// enemy within 3 m of the impact takes X% of the shot's damage (the projectile's own damage block, as launched).
    /// The projectile's hit is resolved on its owner, the shooter's client (Projectile.OnHit, <see cref="ProjectileHitPatch"/>):
    /// there the burst is found and dealt as the player's own hits (<see cref="SecondaryHits"/>), each routed by the game
    /// to its target's owner, and the game's networked explosion effect is spawned at the impact so every client nearby
    /// sees it. A bounce is not an impact; the enemy hit directly is not hit twice.
    /// </summary>
    internal static class ExplosiveShot
    {
        private const float Radius = 3f;

        private static readonly NetVisual Blast = new NetVisual(
            "fx_clusterbombstaff_splinter_hit", "fx_siegebomb_explosion", "vfx_FireballHit");

        /// <summary>The projectile has just hit something (its owner's client).</summary>
        public static void OnImpact(Projectile projectile, Collider? collider, Vector3 point)
        {
            Player? player = Player.m_localPlayer;
            if (player == null || !ReferenceEquals(projectile.m_owner, player) || !ItemEffects.Enabled)
            {
                return;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(projectile.m_weapon);
            float share = sums != null ? sums.Get(EffectKind.ExplosiveShot) : 0f;
            if (share > 0f)
            {
                Burst(player, projectile, point, DirectTarget(collider), share);
            }
        }

        private static void Burst(Player player, Projectile projectile, Vector3 point, Character? direct, float share)
        {
            HitData.DamageTypes damage = projectile.m_damage;
            damage.Modify(share);
            foreach (Character target in SecondaryHits.Near(player, point, Radius, int.MaxValue, direct))
            {
                HitData hit = SecondaryHits.NewHit(player, target, projectile.m_skill, point);
                hit.m_damage = damage;
                SecondaryHits.Deal(target, hit);
            }
            Blast.Spawn(point);
        }

        private static Character? DirectTarget(Collider? collider)
        {
            GameObject? hit = collider != null ? Projectile.FindHitObject(collider) : null;
            return hit != null ? hit.GetComponent<Character>() : null;
        }
    }
}
