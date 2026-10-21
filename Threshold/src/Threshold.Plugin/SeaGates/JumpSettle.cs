// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/JumpSettle.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The settle step, on whoever owns the ship once it has been moved: wait until this machine has the
    /// destination loaded (<c>ZNetScene.IsAreaReady</c>, and a water volume there, since without one
    /// <c>Floating.GetWaterLevel</c> reads -10000 and a released ship falls) and its own copy of the ship sits at the
    /// ZDO pose; then find the first clear and deep pose (<see cref="JumpClearance"/>) from the planned one outward, in
    /// 2 m steps up to 20 m, and settle there. Past the ship's wait, when the crew has been set ashore, the depth rule is
    /// dropped and, failing that, the planned pose is taken, so a ship never stays frozen for good. A machine that has
    /// just arrived waits a few seconds first (<see cref="TrackReference"/>), so the search sees what is there.</summary>
    internal static class JumpSettle
    {
        private const float StepMetres = 2f;
        private const int MaxSteps = 10;
        private const float AtPoseMetres = 0.5f;
        private const float NoWater = -1000f;
        private const float SearchSeconds = 0.1f; // a blocked exit is searched ten times a second, not every frame
        private const float ArrivalSeconds = 3f;  // in the same zone this long before an exit is searched

        private static Vector2s referenceZone;
        private static float referenceSince;
        private static float nextSearch;
        private static int searchFrame = -1;  // the frame a search was due: every ship waiting searches in it

        /// <summary>This machine has a point's area loaded, objects and water.</summary>
        internal static bool Ready(Vector3 point)
        {
            if (ZNetScene.instance == null || ZoneSystem.instance == null || !ZNetScene.instance.IsAreaReady(point))
                return false;
            return HasWater(point);
        }

        /// <summary>The only state a ship may be let go in: its area ready and water loaded under every point the game
        /// floats it by (<c>Ship.CustomFixedUpdate</c> averages the water level at the centre of mass and at the float
        /// box's bow, stern and both sides; one point without a water volume reads -10000 and the ship falls).
        /// <c>IsAreaReady</c> alone does not ensure that: it checks only the centre zone is loaded, and an open-sea zone
        /// next to it may hold no object to wait for.</summary>
        internal static bool ReadyToRelease(Ship ship)
        {
            if (!Ready(ship.transform.position))
                return false;
            if (!FloatBox.TryOf(ship, out FloatBox box))
                return true;
            Vector3 side = Vector3.Cross(Vector3.up, box.Forward).normalized * box.HalfWidth;
            return HasWater(box.Centre) && HasWater(box.Bow) && HasWater(box.Stern) &&
                   HasWater(box.Centre + side) && HasWater(box.Centre - side);
        }

        private static bool HasWater(Vector3 point)
        {
            Vector3 probe = new Vector3(point.x, SeaGateFields.WaterLevel - 1f, point.z);
            return Floating.GetLiquidLevel(probe, 1f, LiquidType.Water) > NoWater;
        }

        /// <summary>Every frame (from <see cref="ShipJump.Tick"/>): when this machine's reference position last moved to
        /// another zone. The server learns a client's reference position only every 2 s (<c>ZNet.SendPeriodicData</c>)
        /// and then streams that area's objects to it, nearest first, while the client builds the terrain and its own
        /// ship on its own. <c>IsAreaReady</c> counts only the objects already received, so a machine that has just
        /// arrived could find the exit clear before the ship parked across it, or a rock, has reached it.</summary>
        internal static void TrackReference()
        {
            Vector2s zone = ZoneSystem.GetZone(ZNet.instance.GetReferencePosition());
            if (zone == referenceZone)
                return;
            referenceZone = zone;
            referenceSince = Time.time;
        }

        /// <summary>Owner, a moved ship (state Moved).</summary>
        internal static void TrySettle(Ship ship, ZDO zdo)
        {
            Vector3 pos = zdo.GetPosition();
            Quaternion rot = zdo.GetRotation();
            if (Time.time - referenceSince < ArrivalSeconds)
                return;
            if ((ship.transform.position - pos).sqrMagnitude > AtPoseMetres * AtPoseMetres || !Ready(pos))
                return;
            if (!SearchDue())
                return;
            bool late = SeaGateFields.Now > zdo.GetLong(SeaGateFields.UntilKey, 0L);
            if (!TryFindPose(ship, zdo, pos, rot, late, out Vector3 found) || !Ready(found))
                return;
            ShipFreeze.Hold(ship);
            if (found != pos)
                ShipFreeze.Place(ship, found, rot);
            zdo.Set(SeaGateFields.StateKey, (int)JumpState.Settled);
        }

        /// <summary>Whether this frame searches: one frame every <see cref="SearchSeconds"/>, in real seconds on every
        /// machine, and every ship that waits to settle searches in that same frame.</summary>
        private static bool SearchDue()
        {
            if (Time.frameCount == searchFrame)
                return true;
            if (Time.time < nextSearch)
                return false;
            nextSearch = Time.time + SearchSeconds;
            searchFrame = Time.frameCount;
            return true;
        }

        /// <summary>Not before a ship that gives way has moved off (<see cref="Clearance.Wait"/>), unless late.</summary>
        private static bool TryFindPose(Ship ship, ZDO zdo, Vector3 pos, Quaternion rot, bool late, out Vector3 found)
        {
            Vector3 outward = ExitDirection(zdo, pos);
            Clearance deep = Search(ship, zdo, pos, rot, outward, true, late, out found);
            if (deep != Clearance.Blocked)
                return deep == Clearance.Clear;
            if (!late)
                return false;
            if (Search(ship, zdo, pos, rot, outward, false, late, out found) != Clearance.Clear)
                found = pos;
            return true;
        }

        /// <summary>Out of the destination surface on the side the ship was placed on.</summary>
        private static Vector3 ExitDirection(ZDO zdo, Vector3 pos)
        {
            Vector3 anchor = zdo.GetVec3(SeaGateFields.DestAnchorKey, pos);
            Vector3 partner = zdo.GetVec3(SeaGateFields.DestPartnerKey, pos);
            GateGeometry gate = new GateGeometry(anchor, partner);
            return gate.OutOf(GateGeometry.SideOf(gate.ToLocal(pos).z));
        }

        /// <summary>From the planned pose outward: the first step that is clear, or one held only by a ship that gives
        /// way (wait there for it); Blocked when every step is.</summary>
        private static Clearance Search(Ship ship, ZDO zdo, Vector3 pos, Quaternion rot, Vector3 outward, bool needDepth,
            bool late, out Vector3 found)
        {
            for (int step = 0; step <= MaxSteps; step++)
            {
                found = pos + outward * (StepMetres * step);
                Clearance clearance = JumpClearance.At(ship, zdo, found, rot, needDepth, late);
                if (clearance != Clearance.Blocked)
                    return clearance;
            }
            found = pos;
            return Clearance.Blocked;
        }
    }
}
