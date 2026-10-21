// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/JumpBegin.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The ship owner starts a jump from a ship stopped in the gate (<see cref="JumpChoice"/>): the destination
    /// pose, the whole jump written to the ship's ZDO at once (so whoever owns the ship next carries it on), the ship
    /// frozen where it is, and a <see cref="JumpOrder"/> to every crew member's peer. A player's peer is the owner of the player's ZDO (<c>Character.GetOwner</c>): each client owns
    /// its own player, and that uid is the one <c>ZRoutedRpc</c> routes by (<c>ZNet</c> sets the routed id to
    /// <c>ZDOMan.GetSessionID()</c>), so the owner's own player is reached the same way.</summary>
    internal static class JumpBegin
    {
        // The game fades its teleport screen in over 1 s; a remote crew member starts a moment after the owner. The
        // ship leaves only once every crew member's screen is fully dark, so nobody sees it vanish.
        private const float MoveDelaySeconds = 1.5f;

        // The crew waits at least CrewMinWaitSeconds from its own teleport start (a moment after this), then lands
        // ashore and reports; the ship waits longer, so a held crew member is never left behind by a ship sailing off.
        private const float CrewMinWaitSeconds = 5f;
        private const float ShipExtraWaitSeconds = 5f;

        private static float ShipWaitSeconds(float crewWait) => Mathf.Max(crewWait, CrewMinWaitSeconds) + ShipExtraWaitSeconds;

        private static readonly List<long> crew = new List<long>();

        /// <summary>False, with nothing changed, when the destination has no side to leave by. The speed is the one the
        /// ship had when it stopped, given back on release.</summary>
        internal static bool TryBegin(Ship ship, LoadedGate gate, JumpGrant grant, int entrySide, float speed)
        {
            if (!JumpPose.TryDestination(ship, gate.Geometry, grant.DestGeometry, entrySide, grant.DestSides,
                    out Vector3 pos, out Quaternion rot))
                return false;
            ZDO zdo = ship.m_nview.GetZDO();
            CollectCrew(ship);
            JumpOrder order = BuildOrder(ship, zdo, grant, pos, rot);
            WriteJump(zdo, order, speed, crew.Count);
            ShipFreeze.Hold(ship);
            foreach (long peer in crew)
                CrewJump.Send(peer, order);
            return true;
        }

        private static JumpOrder BuildOrder(Ship ship, ZDO zdo, JumpGrant grant, Vector3 pos, Quaternion rot)
        {
            return new JumpOrder
            {
                JumpId = NewJumpId(zdo),
                Ship = zdo.m_uid,
                SourcePos = ship.transform.position,
                SourceRot = ship.transform.rotation,
                DestPos = pos,
                DestRot = rot,
                DestAnchor = grant.DestAnchor,
                DestPartner = grant.DestPartner,
                WaitSeconds = SeaGateConfig.CrewWaitSeconds.Value,
            };
        }

        /// <summary>Every crew member's peer, once each: <c>Ship.m_players</c> holds every player in the ship's trigger,
        /// remote ones included.</summary>
        private static void CollectCrew(Ship ship)
        {
            crew.Clear();
            foreach (Player player in ship.m_players)
            {
                long peer = player != null ? player.GetOwner() : 0L;
                if (peer != 0L && !crew.Contains(peer))
                    crew.Add(peer);
            }
        }

        private static int NewJumpId(ZDO zdo)
        {
            int previous = SeaGateFields.GetJump(zdo);
            int id = Random.Range(1, int.MaxValue);
            return id != previous ? id : (id == int.MaxValue - 1 ? 1 : id + 1);
        }

        /// <summary>The state goes last, so no machine reads a frozen ship whose destination is not written yet.</summary>
        private static void WriteJump(ZDO zdo, JumpOrder order, float speed, int crewCount)
        {
            zdo.Set(SeaGateFields.JumpKey, order.JumpId);
            zdo.Set(SeaGateFields.SpeedKey, speed);
            zdo.Set(SeaGateFields.CrewKey, crewCount);
            zdo.Set(SeaGateFields.LandedKey, 0);
            zdo.Set(SeaGateFields.MoveAtKey, SeaGateFields.After(MoveDelaySeconds));
            zdo.Set(SeaGateFields.UntilKey, SeaGateFields.After(ShipWaitSeconds(order.WaitSeconds)));
            zdo.Set(SeaGateFields.DestPosKey, order.DestPos);
            zdo.Set(SeaGateFields.DestRotKey, order.DestRot);
            zdo.Set(SeaGateFields.DestAnchorKey, order.DestAnchor);
            zdo.Set(SeaGateFields.DestPartnerKey, order.DestPartner);
            zdo.Set(SeaGateFields.StateKey, (int)JumpState.Frozen);
        }
    }
}
