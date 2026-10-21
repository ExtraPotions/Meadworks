// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>The floor under a point, for what the Executioner puts on the ground (the slam's chunks, the shatter).</summary>
    public static class HeadsmanGround
    {
        private static readonly int Floors = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");

        /// <summary>The floor under a point (the crypt's, or the ground's), or the point itself when there is none close.</summary>
        public static Vector3 Floor(Vector3 point) =>
            Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f, Floors) ? hit.point : point;
    }
}
