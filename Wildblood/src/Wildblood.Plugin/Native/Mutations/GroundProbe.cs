// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The ground under a drop, found on each machine from its own copy of the world: the solid surface below the spot
    /// a creature stood (terrain, rock, a floor), and which way it faces, so a patch lies on the slope it fell on.
    /// </summary>
    internal static class GroundProbe
    {
        private const float Lift = 1.5f;
        private const float Depth = 4f;

        private static int _mask;

        /// <summary>
        /// The surface point and its normal below <paramref name="at"/>; the spot itself, facing up, if none.
        /// </summary>
        public static void Find(Vector3 at, out Vector3 point, out Vector3 normal)
        {
            if (_mask == 0)
            {
                _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            }
            if (Physics.Raycast(at + Vector3.up * Lift, Vector3.down, out RaycastHit hit, Lift + Depth, _mask))
            {
                point = hit.point;
                normal = hit.normal;
                return;
            }
            point = at;
            normal = Vector3.up;
        }
    }
}
