// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. "Arrows fly to the crosshair" idea from public parity notes (behavior only); no reference source used.
using System.Numerics;

namespace ExtraPotions.Edge.Archery
{
    /// <summary>
    /// Arrows To Crosshair math. The game launches a player's projectile along the camera's look direction from a point
    /// beside the shoulder, so it lands beside the point under the crosshair. This aims it from that spawn point at the
    /// point under the crosshair instead: the first surface along the camera's center ray ahead of the spawn point, or a
    /// far point on that ray when nothing is hit. Gravity and speed are left to the game.
    /// </summary>
    public static class CrosshairAim
    {
        /// <summary>Distance along the center ray used when the ray hits nothing.</summary>
        public const float FarDistance = 1000f;

        /// <summary>A hit must lie this far beyond the spawn point's depth on the ray to be the aim point.</summary>
        public const float MinAhead = 0.5f;

        /// <summary>The spawn point's depth along the camera's center ray.</summary>
        public static float SpawnDepth(Vector3 spawn, Vector3 cameraPosition, Vector3 cameraForward) =>
            Vector3.Dot(spawn - cameraPosition, Normalized(cameraForward));

        /// <summary>Whether a center-ray hit at <paramref name="hitDistance"/> is ahead of the spawn point (not the player, not behind it).</summary>
        public static bool Usable(float hitDistance, float spawnDepth) =>
            !float.IsNaN(hitDistance) && hitDistance > spawnDepth + MinAhead;

        /// <summary>
        /// The launch direction from <paramref name="spawn"/> toward the crosshair point at <paramref name="aimDistance"/>
        /// along the center ray. Falls back to the camera's forward when the point is not ahead of the spawn point.
        /// </summary>
        public static Vector3 Direction(Vector3 spawn, Vector3 cameraPosition, Vector3 cameraForward, float aimDistance)
        {
            var forward = Normalized(cameraForward);
            if (forward == Vector3.Zero) return forward;
            if (float.IsNaN(aimDistance) || float.IsInfinity(aimDistance) || aimDistance <= 0f) aimDistance = FarDistance;
            var toTarget = cameraPosition + forward * aimDistance - spawn;
            var length = toTarget.Length();
            if (length < MinAhead || Vector3.Dot(toTarget, forward) <= 0f) return forward;
            return toTarget / length;
        }

        private static Vector3 Normalized(Vector3 v)
        {
            var length = v.Length();
            return length > 1e-6f && !float.IsNaN(length) ? v / length : Vector3.Zero;
        }
    }
}
