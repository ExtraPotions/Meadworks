// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Where an aspect puts what it brings into the fight - a twin, phantom copies, summoned creatures: a point on a ring
    /// around the boss, dropped onto the floor beneath it. The ray starts just above the boss's own height, so inside a
    /// dungeon it finds the floor rather than the roof, and under a flying boss it finds the ground.
    /// </summary>
    internal static class SpawnPlace
    {
        /// <summary>Further than this below the boss is not "the floor beneath it" - a chasm, or the sea bed.</summary>
        private const float MaxDrop = 30f;

        public static Vector3 Around(Vector3 centre, float angleDegrees, float radius)
        {
            Vector3 pos = centre + Quaternion.Euler(0f, angleDegrees, 0f) * Vector3.forward * radius;
            return OnFloor(pos, centre.y);
        }

        public static Vector3 Random(Vector3 centre, float minRadius, float maxRadius) =>
            Around(centre, UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(minRadius, maxRadius));

        private static Vector3 OnFloor(Vector3 pos, float height)
        {
            pos.y = height;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(pos, out float floor, 3)
                && height - floor < MaxDrop)
            {
                pos.y = floor + 0.2f;
            }
            return pos;
        }
    }
}
