// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// One laid patch of a ground trail as this machine holds it: where it lies on the ground, how far it reaches, the
    /// local moment it is gone (the moment its drawing has faded out), and what it does to a player standing in it. A
    /// patch outlives the creature that laid it: it is this machine's own record, not the creature's.
    /// </summary>
    internal struct GroundPatch
    {
        /// <summary>
        /// How far above or below the patch a player's feet may be and still stand in it: not on a bridge over it.
        /// </summary>
        private const float Reach = 1.5f;

        public Vector3 Point;
        public float RadiusSq;
        public float Until;
        public float Slow;
        public float Grip;

        /// <summary>Fire damage a second to a player standing in it; 0 off burning ground.</summary>
        public float Burn;

        /// <summary>True when feet at <paramref name="feet"/> stand inside the patch.</summary>
        public bool Covers(Vector3 feet)
        {
            float dx = feet.x - Point.x;
            float dz = feet.z - Point.z;
            return dx * dx + dz * dz <= RadiusSq && Mathf.Abs(feet.y - Point.y) <= Reach;
        }
    }
}
