// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Slinger
{
    /// <summary>
    /// Aims each slingshot stone: the game points a creature's projectile straight at its target's middle and tilts
    /// it by a fixed launch angle, which suits a fast flat shot but drops a slower stone short. Just before the stone
    /// leaves, this sets that shot's launch angle (the attack is the shot's own copy) to the low arc that carries a stone
    /// at the shot's speed under the stone's gravity onto the target's middle, or 45 degrees when the target is out of
    /// reach. The game's spread is added afterwards as usual. It does not lead a moving target.
    /// </summary>
    public static class SlingerAim
    {
        public static void Lob(Attack attack)
        {
            if (attack.m_attackOriginJoint != SlingerShot.Muzzle || attack.m_baseAI == null)
            {
                return;
            }
            Character? target = attack.m_baseAI.GetTargetCreature();
            if (target == null)
            {
                return;
            }
            attack.GetProjectileSpawnPoint(out Vector3 from, out Vector3 _);
            Vector3 span = target.GetCenterPoint() - from;
            float across = new Vector2(span.x, span.z).magnitude;
            float sight = Mathf.Atan2(span.y, across);
            float elevation = Elevation(attack.m_projectileVel, SlingerStone.Gravity, across, span.y);
            attack.m_launchAngle = -(elevation - sight) * Mathf.Rad2Deg;   // the game tilts upwards for negative angles
        }

        /// <summary>
        /// The low-arc elevation, in radians above the horizontal, that carries a stone at `speed` over `across` metres
        /// and `up` metres of rise under `gravity`; 45 degrees when the target is out of reach.
        /// </summary>
        public static float Elevation(float speed, float gravity, float across, float up)
        {
            float v2 = speed * speed;
            float root = v2 * v2 - gravity * (gravity * across * across + 2f * up * v2);
            return root < 0f || across < 0.01f ? Mathf.PI / 4f : Mathf.Atan((v2 - Mathf.Sqrt(root)) / (gravity * across));
        }
    }
}
