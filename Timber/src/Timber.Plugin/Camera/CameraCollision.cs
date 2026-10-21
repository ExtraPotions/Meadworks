#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CameraCollision.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
// Cartur portions: GPL-3.0-only; f81977b2553d48a2a79eea62969f86bb9d9fa32e. See docs/source-provenance.json.
using UnityEngine;

namespace Timber.Camera
{
    /// <summary>
    /// Keeps the camera out of the ground: a sphere about the size of the game camera's own collision probe is swept
    /// from where the camera is to where it would go, against the <c>terrain</c> layer (the ground, and the floors,
    /// walls and ceilings of caves built into it) and <c>static_solid</c> (rocks, cliffs, dungeon walls). On a hit it
    /// stops just short and slides along the surface for the rest of the move. Building pieces, trees and creatures are
    /// not in the mask, so the camera passes through walls into a house.
    /// </summary>
    public static class CameraCollision
    {
        private static float Radius => CameraSettings.TerrainClearance.Value;
        private const float Skin = 0.05f;
        private const float MinMove = 1e-4f;

        private static int mask;
        private static Vector3 lastSafe;
        private static bool hasLastSafe;

        private static int Mask => mask != 0 ? mask : (mask = LayerMask.GetMask("terrain", "static_solid"));

        public static Vector3 Move(Vector3 from, Vector3 to)
        {
            Vector3 stop = Sweep(from, to, out Vector3 normal);
            if (normal != Vector3.zero)
            {
                Vector3 slide = Vector3.ProjectOnPlane(to - stop, normal);
                stop = Sweep(stop, stop + slide, out _);
            }
            // Cartur's endpoint/last-safe guard covers starting overlaps and changing terrain.
            if (Physics.CheckSphere(stop, Radius, Mask, QueryTriggerInteraction.Ignore))
                return hasLastSafe && !Physics.CheckSphere(lastSafe, Radius, Mask, QueryTriggerInteraction.Ignore)
                    ? lastSafe : from;
            lastSafe = stop;
            hasLastSafe = true;
            return stop;
        }

        public static void Reset() => hasLastSafe = false;

        /// <summary>As far toward the target as the sphere goes; the normal of what it hit, or zero when nothing.</summary>
        private static Vector3 Sweep(Vector3 from, Vector3 to, out Vector3 normal)
        {
            normal = Vector3.zero;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < MinMove)
                return from;
            Vector3 direction = delta / distance;
            if (!Physics.SphereCast(from, Radius, direction, out RaycastHit hit, distance + Skin, Mask, QueryTriggerInteraction.Ignore))
                return to;
            normal = hit.normal;
            return from + direction * Mathf.Max(0f, hit.distance - Skin);
        }
    }
}
