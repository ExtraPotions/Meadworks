// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/CrewSpot.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>A crew member's place on a ship in the ship's own frame: the feet position and the facing, measured on
    /// deck when the jump begins and carried to wherever the ship stands now. Rotation only, no scale (the ship's own
    /// pose is what <see cref="JumpOrder.DestPos"/> and the ZDO hold), so the same numbers give the same spot on the live
    /// ship, on its ZDO pose and on the order's destination pose. The facing keeps only the yaw: a player stands upright
    /// even if the ship was rolling when it froze.</summary>
    public readonly struct CrewSpot
    {
        public readonly Vector3 Local;
        public readonly Quaternion LocalRot;

        private CrewSpot(Vector3 local, Quaternion localRot)
        {
            Local = local;
            LocalRot = localRot;
        }

        public static CrewSpot Measure(Vector3 shipPos, Quaternion shipRot, Vector3 worldPos, Quaternion worldRot)
        {
            Quaternion inverse = Quaternion.Inverse(shipRot);
            return new CrewSpot(inverse * (worldPos - shipPos), inverse * worldRot);
        }

        public Vector3 PositionOn(Vector3 shipPos, Quaternion shipRot) => shipPos + shipRot * Local;

        public Quaternion FacingOn(Quaternion shipRot) => Upright(shipRot * LocalRot);

        /// <summary>The yaw of a rotation, level.</summary>
        public static Quaternion Upright(Quaternion rot) => Facing(rot * Vector3.forward);

        /// <summary>A level rotation looking along a direction; straight ahead (world forward) when it has no
        /// horizontal part.</summary>
        public static Quaternion Facing(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return Quaternion.identity;
            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    /// <summary>The jumping ship as this machine knows it: its ZDO (kept even where the ship is not loaded) and its live
    /// instance, if this machine has one.</summary>
    internal static class CrewShip
    {
        internal static ZDO Zdo(ZDOID id) => id.IsNone() || ZDOMan.instance == null ? null : ZDOMan.instance.GetZDO(id);

        internal static Ship Find(ZDOID id) => Find(Zdo(id));

        internal static Ship Find(ZDO zdo)
        {
            if (zdo == null || ZNetScene.instance == null)
                return null;
            ZNetView view = ZNetScene.instance.FindInstance(zdo);
            return view != null && view.IsValid() ? view.GetComponent<Ship>() : null;
        }

        /// <summary>The ZDO shows this jump with the ship already placed at the destination (moved or settled).</summary>
        internal static bool Moved(ZDO zdo, int jumpId)
        {
            if (zdo == null || SeaGateFields.GetJump(zdo) != jumpId)
                return false;
            JumpState state = SeaGateFields.GetState(zdo);
            return state == JumpState.Moved || state == JumpState.Settled;
        }

        /// <summary>The ZDO shows this jump settled: the ship's owner has checked the exit clear.</summary>
        internal static bool Settled(ZDO zdo, int jumpId) =>
            zdo != null && SeaGateFields.GetJump(zdo) == jumpId && SeaGateFields.GetState(zdo) == JumpState.Settled;
    }
}
