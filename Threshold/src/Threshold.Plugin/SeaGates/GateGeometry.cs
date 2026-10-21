// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/GateGeometry.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The frame of one gate, from its two pillar positions (anchor first). Right runs from the anchor to the
    /// partner along the ground, Forward is the horizontal normal of the portal surface (the front side), the origin
    /// is the midpoint between the pillars at sea level. Local coordinates: x across the span, y up from sea level,
    /// z out of the surface (positive on the front side). Pillars never move, so this is the same on every machine.</summary>
    public readonly struct GateGeometry
    {
        public readonly Vector3 A;
        public readonly Vector3 B;
        public readonly Vector3 Mid;
        public readonly Vector3 Right;
        public readonly Vector3 Forward;
        public readonly float Width;

        public GateGeometry(Vector3 anchorPos, Vector3 partnerPos)
        {
            A = anchorPos;
            B = partnerPos;
            Vector3 flat = partnerPos - anchorPos;
            flat.y = 0f;
            Width = flat.magnitude;
            Right = Width > 0.01f ? flat / Width : Vector3.right;
            Forward = new Vector3(-Right.z, 0f, Right.x);
            Vector3 mid = (anchorPos + partnerPos) * 0.5f;
            Mid = new Vector3(mid.x, SeaGateFields.WaterLevel, mid.z);
        }

        public Quaternion Frame => Quaternion.LookRotation(Forward, Vector3.up);

        public Vector3 ToLocal(Vector3 world)
        {
            Vector3 d = world - Mid;
            return new Vector3(Vector3.Dot(d, Right), d.y, Vector3.Dot(d, Forward));
        }

        public Vector3 ToWorld(Vector3 local) => Mid + Right * local.x + Vector3.up * local.y + Forward * local.z;

        public Quaternion ToLocal(Quaternion world) => Quaternion.Inverse(Frame) * world;

        public Quaternion ToWorld(Quaternion local) => Frame * local;

        /// <summary>Whether a local x lies between the pillars, at least <paramref name="margin"/> inside them.</summary>
        public bool WithinSpan(float localX, float margin = 0f) => Mathf.Abs(localX) <= Width * 0.5f - margin;

        /// <summary>The side a local z lies on.</summary>
        public static int SideOf(float localZ) => localZ >= 0f ? SeaGateFields.SideFront : SeaGateFields.SideBack;

        public static int Opposite(int side) => side == SeaGateFields.SideFront ? SeaGateFields.SideBack : SeaGateFields.SideFront;

        /// <summary>The world direction pointing out of the surface on a side.</summary>
        public Vector3 OutOf(int side) => side == SeaGateFields.SideFront ? Forward : -Forward;
    }
}
