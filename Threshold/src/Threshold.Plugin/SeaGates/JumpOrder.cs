// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/JumpOrder.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>What the ship's owner tells each crew member when a jump begins (<see cref="CrewJump.JumpRpc"/>).
    /// Source pose: where the ship was frozen. Destination pose: where it will be placed (it may still be slid clear
    /// on arrival, so the crew lands against the live ship, not this pose). Destination pillars: for the dry-ground
    /// fallback. Wait seconds: how long the crew waits for the ship to settle before taking the fallback.</summary>
    public sealed class JumpOrder
    {
        public int JumpId;
        public ZDOID Ship;
        public Vector3 SourcePos;
        public Quaternion SourceRot;
        public Vector3 DestPos;
        public Quaternion DestRot;
        public Vector3 DestAnchor;
        public Vector3 DestPartner;
        public float WaitSeconds;

        public ZPackage Write()
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(JumpId);
            pkg.Write(Ship);
            pkg.Write(SourcePos);
            pkg.Write(SourceRot);
            pkg.Write(DestPos);
            pkg.Write(DestRot);
            pkg.Write(DestAnchor);
            pkg.Write(DestPartner);
            pkg.Write(WaitSeconds);
            return pkg;
        }

        public static JumpOrder Read(ZPackage pkg)
        {
            return new JumpOrder
            {
                JumpId = pkg.ReadInt(),
                Ship = pkg.ReadZDOID(),
                SourcePos = pkg.ReadVector3(),
                SourceRot = pkg.ReadQuaternion(),
                DestPos = pkg.ReadVector3(),
                DestRot = pkg.ReadQuaternion(),
                DestAnchor = pkg.ReadVector3(),
                DestPartner = pkg.ReadVector3(),
                WaitSeconds = pkg.ReadSingle(),
            };
        }
    }
}
