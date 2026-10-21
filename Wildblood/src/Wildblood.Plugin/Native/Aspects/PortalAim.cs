// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The direction a portal-carried projectile leaves the far portal in so that it reaches the target. One that flies
    /// straight (the Elder's vines, gravity 0) is aimed straight at it; one that falls (Bonemass's slime: 20 m/s, gravity
    /// 10, no drag) along the lower of the two arcs that land on it, since aimed straight it would fall short of a target
    /// below and beside the portal. The game adds its own spread afterwards, as it does from the hand.
    /// </summary>
    internal static class PortalAim
    {
        /// <summary>The launch direction from <paramref name="from"/> to <paramref name="to"/> at a speed and gravity.</summary>
        public static Vector3 Toward(Vector3 from, Vector3 to, float speed, float gravity)
        {
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float across = flat.magnitude;
            if (gravity <= 0f || speed <= 0f || across < 0.01f)
            {
                return (to - from).normalized;
            }
            float speed2 = speed * speed;
            float rise = to.y - from.y;
            float root = speed2 * speed2 - gravity * (gravity * across * across + 2f * rise * speed2);
            // Out of reach: 45 degrees carries it furthest, so it lands as near the target as the throw allows.
            float angle = root < 0f ? Mathf.PI / 4f : Mathf.Atan((speed2 - Mathf.Sqrt(root)) / (gravity * across));
            return (flat / across * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)).normalized;
        }

        /// <summary>The speed the game gives the attack's projectiles (a creature's attack has no ammo or draw to add).</summary>
        public static float SpeedOf(Attack attack) =>
            attack.m_randomVelocity ? (attack.m_projectileVelMin + attack.m_projectileVel) * 0.5f : attack.m_projectileVel;

        /// <summary>The gravity of the projectile the attack fires; 0 for one without the game's Projectile.</summary>
        public static float GravityOf(Attack attack)
        {
            GameObject? prefab = attack.m_attackProjectile;
            Projectile? projectile = prefab != null ? prefab.GetComponent<Projectile>() : null;
            return projectile != null ? projectile.m_gravity : 0f;
        }
    }
}
