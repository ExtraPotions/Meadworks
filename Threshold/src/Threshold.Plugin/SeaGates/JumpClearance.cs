// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/JumpClearance.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>What a ship's hull meets at a pose at the destination.</summary>
    internal enum Clearance
    {
        Clear,   // nothing in the way (and deep enough, where asked)
        Wait,    // held only by a ship still being placed by a later jump, which slides off the spot itself
        Blocked  // anything else in the way, or too shallow
    }

    /// <summary>Whether a ship's hull fits at a pose: the hull's box (the float box's length and width plus a margin, from
    /// 1 m below sea level to 3 m above) against every other ship and every solid non-terrain collider (layers Default,
    /// static_solid, Default_small, piece and vehicle; triggers, water, terrain, characters and items ignored, the ship's
    /// own colliders skipped), and where asked at least 1.5 m of water under its middle and both ends. Two ships through
    /// the same gate close together see each other frozen at the same planned pose; checked on two machines at once,
    /// each would slide to the same next spot. So the later jump gives way (by <see cref="SeaGateFields.MoveAtKey"/>,
    /// then the jump id: the same answer on every machine): it counts the earlier ship as in the way and slides, while
    /// the earlier one waits for it instead of sliding. Past its own wait a ship waits for nobody, so a ship whose owner
    /// has gone holds no one up.</summary>
    internal static class JumpClearance
    {
        private const float MarginMetres = 0.5f;
        private const float BoxLift = 1f;
        private const float BoxHalfHeight = 2f;
        // Water the hull needs at its settle spot: 1.5 m, or less when the gates are set to pair in shallower water,
        // so a gate that paired always lets a ship settle.
        private const float MaxDepthMetres = 1.5f;

        private static readonly Collider[] hits = new Collider[256];
        private static int solidMask;

        private static int SolidMask => solidMask != 0 ? solidMask
            : solidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "vehicle");

        /// <summary>The ship's hull with its root at a pose. A ship without a float box is never held up.</summary>
        internal static Clearance At(Ship ship, ZDO zdo, Vector3 pos, Quaternion rot, bool needDepth, bool late)
        {
            if (!HullBox(ship, pos, rot, out Vector3 centre, out Vector3 half, out Quaternion boxRot))
                return Clearance.Clear;
            Clearance found = Overlaps(ship, zdo, centre, half, boxRot, late);
            if (found == Clearance.Clear && needDepth && !IsDeep(centre, half, boxRot))
                return Clearance.Blocked;
            return found;
        }

        /// <summary>The hull's box with the ship's root at a pose, measured from the ship as it stands now.</summary>
        private static bool HullBox(Ship ship, Vector3 pos, Quaternion rot, out Vector3 centre, out Vector3 half, out Quaternion boxRot)
        {
            BoxCollider collider = ship.m_floatCollider;
            centre = pos;
            half = Vector3.zero;
            boxRot = rot;
            if (collider == null)
                return false;
            Transform root = ship.transform;
            boxRot = rot * (Quaternion.Inverse(root.rotation) * collider.transform.rotation);
            centre = pos + rot * root.InverseTransformPoint(collider.transform.position);
            centre.y = SeaGateFields.WaterLevel + BoxLift;
            half = new Vector3(collider.size.x * 0.5f + MarginMetres, BoxHalfHeight, collider.size.z * 0.5f + MarginMetres);
            return true;
        }

        private static Clearance Overlaps(Ship ship, ZDO zdo, Vector3 centre, Vector3 half, Quaternion boxRot, bool late)
        {
            int count = Physics.OverlapBoxNonAlloc(centre, half, hits, boxRot, SolidMask, QueryTriggerInteraction.Ignore);
            Clearance result = Clearance.Clear;
            for (int i = 0; i < count; i++)
            {
                Clearance hit = Classify(ship, zdo, hits[i], late);
                if (hit == Clearance.Blocked)
                    return Clearance.Blocked;
                if (hit == Clearance.Wait)
                    result = Clearance.Wait;
            }
            return result;
        }

        /// <summary>One collider in the box: the ship's own, a ship that gives way to this one, or in the way.</summary>
        private static Clearance Classify(Ship ship, ZDO zdo, Collider hit, bool late)
        {
            Rigidbody body = hit.attachedRigidbody;
            if (hit.transform.IsChildOf(ship.transform) || (body != null && body == ship.m_body))
                return Clearance.Clear;
            Ship other = body != null ? body.GetComponent<Ship>() : null;
            ZNetView view = other != null ? other.m_nview : null;
            if (late || view == null || !view.IsValid() || !GivesWay(view.GetZDO(), zdo))
                return Clearance.Blocked;
            return Clearance.Wait;
        }

        /// <summary>The other ship is still being placed (state Moved) by a jump that began after this one.</summary>
        private static bool GivesWay(ZDO other, ZDO self)
        {
            if (SeaGateFields.GetState(other) != JumpState.Moved)
                return false;
            long theirs = other.GetLong(SeaGateFields.MoveAtKey, 0L);
            long mine = self.GetLong(SeaGateFields.MoveAtKey, 0L);
            return theirs > mine || (theirs == mine && SeaGateFields.GetJump(other) > SeaGateFields.GetJump(self));
        }

        private static bool IsDeep(Vector3 centre, Vector3 half, Quaternion boxRot)
        {
            Vector3 along = boxRot * Vector3.forward * (half.z - MarginMetres);
            float floor = SeaGateFields.WaterLevel - Mathf.Min(MaxDepthMetres, SeaGateConfig.MinWaterDepth.Value);
            return GroundBelow(centre, floor) && GroundBelow(centre + along, floor) && GroundBelow(centre - along, floor);
        }

        /// <summary>The terrain under a point lies below a height; no terrain found reads as not deep enough.</summary>
        private static bool GroundBelow(Vector3 point, float floor)
        {
            return ZoneSystem.instance.GetGroundHeight(point, out float height) && height < floor;
        }
    }
}
