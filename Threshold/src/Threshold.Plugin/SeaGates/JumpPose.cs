// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/JumpPose.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>A ship's float box as the game itself reads it in <c>Ship.CustomFixedUpdate</c>: the
    /// <c>m_floatCollider</c>'s transform position, its forward, and <c>size.z / 2</c> to each end (the collider's own
    /// centre offset and scale are ignored there too).</summary>
    internal readonly struct FloatBox
    {
        public readonly Vector3 Centre;
        public readonly Vector3 Forward;
        public readonly float HalfLength;
        public readonly float HalfWidth;

        private FloatBox(Vector3 centre, Vector3 forward, float halfLength, float halfWidth)
        {
            Centre = centre;
            Forward = forward;
            HalfLength = halfLength;
            HalfWidth = halfWidth;
        }

        public Vector3 Bow => Centre + Forward * HalfLength;
        public Vector3 Stern => Centre - Forward * HalfLength;

        public static bool TryOf(Ship ship, out FloatBox box)
        {
            BoxCollider collider = ship != null ? ship.m_floatCollider : null;
            if (collider == null)
            {
                box = default;
                return false;
            }
            Transform t = collider.transform;
            box = new FloatBox(t.position, t.forward, collider.size.z * 0.5f, collider.size.x * 0.5f);
            return true;
        }
    }

    /// <summary>Where a ship comes out of the destination gate: its pose relative to the source gate carried over. The
    /// exit side is the side it is heading into (opposite its entry side); if the destination cannot be left on that
    /// side, the whole local pose is turned 180 degrees about the gate's up axis, so the ship comes out of the other
    /// side heading away from the surface. Across the span: the float box centre's own offset, kept 2 m inside the
    /// destination's pillars. Out of the surface: half the float box plus 4 m. Height: the same above sea level. Only
    /// the yaw is carried over, so a ship caught rolling on a wave is placed level.</summary>
    internal static class JumpPose
    {
        private const float ExitClearance = 4f;
        private const float SpanMargin = 2f;
        private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

        /// <summary>False when the destination has no side to leave by.</summary>
        public static bool TryDestination(Ship ship, GateGeometry source, GateGeometry dest, int entrySide, int destSides,
            out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            int exitSide = GateGeometry.Opposite(entrySide);
            bool turn = (destSides & exitSide) == 0;
            if ((turn && (destSides & entrySide) == 0) || !FloatBox.TryOf(ship, out FloatBox box))
                return false;
            Vector3 local = LocalCentre(source, dest, box, exitSide);
            Quaternion localRot = source.ToLocal(Level(ship.transform));
            if (turn)
            {
                local = new Vector3(-local.x, local.y, -local.z);
                localRot = HalfTurn * localRot;
            }
            rot = dest.ToWorld(localRot);
            pos = dest.ToWorld(local) - rot * ship.transform.InverseTransformPoint(box.Centre);
            return true;
        }

        /// <summary>The float box centre in the destination frame, before any turn.</summary>
        private static Vector3 LocalCentre(GateGeometry source, GateGeometry dest, FloatBox box, int exitSide)
        {
            Vector3 centre = source.ToLocal(box.Centre);
            float half = Mathf.Max(0f, dest.Width * 0.5f - SpanMargin);
            float outward = box.HalfLength + ExitClearance;
            float z = exitSide == SeaGateFields.SideFront ? outward : -outward;
            return new Vector3(Mathf.Clamp(centre.x, -half, half), centre.y, z);
        }

        /// <summary>The ship's heading with pitch and roll taken out.</summary>
        private static Quaternion Level(Transform ship)
        {
            Vector3 forward = ship.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(ship.up, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
