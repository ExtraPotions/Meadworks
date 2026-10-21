// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Slinger
{
    /// <summary>
    /// The stone a slinger fires: a copy of the Greydwarf's thrown rock at half its size (still a fist-sized stone,
    /// small enough for the pouch), falling at half the rock's rate, with a narrower hit so it can be sidestepped. It
    /// flies a shallow arc that <see cref="SlingerAim"/> works out for each shot. Its hit and damage are the game's
    /// projectile's; the damage comes from the shot (<see cref="SlingerShot"/>).
    /// </summary>
    public static class SlingerStone
    {
        public const float Size = 0.5f;      // of the thrown rock's (whose visual is about 17 x 18 x 29 cm)
        public const float Gravity = 5f;     // the thrown rock falls at 10

        public static GameObject Build(GameObject rock)
        {
            GameObject stone = PrefabBench.Copy(rock, SlingerPrefabs.Stone);
            var projectile = stone.GetComponent<Projectile>();
            projectile.m_gravity = Gravity;
            projectile.m_ttl = 4f;
            projectile.m_rayRadius = 0.3f;
            if (projectile.m_visual != null)
            {
                projectile.m_visual.transform.localScale *= Size;
            }
            return stone;
        }

        /// <summary>The stone's look, for the one waiting in the pouch.</summary>
        public static Transform? Visual(GameObject stone) => stone.GetComponent<Projectile>()?.m_visual?.transform;
    }
}
