// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/JumpCrossing.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The ship owner, a ship with players aboard and no jump under way, against every gate loaded here, in the
    /// gate's own frame (no physics triggers). Crossing: an end of the float box is a quarter of the ship's length
    /// through the surface, inside the span, while the box's centre is still on the near side and the ship is moving
    /// across, so the ship is seen sailing into the portal. A crossing either stops the ship in the gate for its
    /// helmsman to pick a destination on the map (<see cref="JumpChoice"/>) or is refused - no helmsman, portal travel
    /// blocked, restricted cargo - and then the ship simply sails on and everyone aboard sees why, once: that ship and
    /// gate get no new attempt until the ship has left the 30 m zone or its centre has crossed. A ship whose helmsman
    /// closed the map sails on the same way, remembered in its ZDO (<see cref="SeaGateFields.SailOnKey"/>) so a new
    /// owner does not stop it again before then. Moving across matters: once the centre is through, the stern is on the far side of
    /// it and would otherwise read as a crossing the other way.</summary>
    internal static class JumpCrossing
    {
        private const float ZoneMetres = 30f;
        private const float SpanSlackMetres = 10f;
        private const float MaxHeightMetres = 10f;
        private const float MinCrossingSpeed = 0.2f;
        private const float ThroughFraction = 0.25f;  // of the ship's length, before the jump starts

        // (ship, gate id) -> the side the ship's centre was on when a crossing there was refused.
        private static readonly Dictionary<(Ship, long), int> refused = new Dictionary<(Ship, long), int>();
        private static readonly List<(Ship, long)> dead = new List<(Ship, long)>();

        internal static void Check(Ship ship)
        {
            if (ship.m_players.Count == 0 || !FloatBox.TryOf(ship, out FloatBox box))
                return;
            bool helm = ship.HaveControllingPlayer();
            foreach (LoadedGate gate in SeaGateRegistry.Gates)
            {
                if (gate.IsAlive && CheckGate(ship, gate, box, helm))
                    return;
            }
        }

        /// <summary>True when the ship stopped in this gate.</summary>
        private static bool CheckGate(Ship ship, LoadedGate gate, FloatBox box, bool helm)
        {
            (Ship, long) key = (ship, gate.Id);
            Vector3 centre = gate.Geometry.ToLocal(box.Centre);
            if (!InZone(gate.Geometry, centre))
            {
                refused.Remove(key);
                ForgetSailOn(ship, gate.Id);
                return false;
            }
            int side = GateGeometry.SideOf(centre.z);
            if ((refused.TryGetValue(key, out int refusedSide) && refusedSide == side) || SailsOn(ship, gate.Id, side))
                return false;
            refused.Remove(key);
            if (!Crossing(ship, gate.Geometry, box, side))
                return false;
            string reason = Attempt(ship, gate, side, helm);
            if (reason == null)
                return true;
            refused[key] = side;
            JumpChoice.TellCrew(ship, 0L, reason);
            return false;
        }

        private static bool InZone(GateGeometry gate, Vector3 local)
        {
            return Mathf.Abs(local.z) <= ZoneMetres && Mathf.Abs(local.y) <= MaxHeightMetres &&
                   gate.WithinSpan(local.x, -SpanSlackMetres);
        }

        /// <summary>An end of the float box a quarter of the ship's length through the surface inside the span, the ship
        /// moving towards the far side.</summary>
        private static bool Crossing(Ship ship, GateGeometry gate, FloatBox box, int centreSide)
        {
            float through = box.HalfLength * 2f * ThroughFraction;
            if (!EndAcross(gate, box.Bow, centreSide, through) && !EndAcross(gate, box.Stern, centreSide, through))
                return false;
            Vector3 velocity = ship.m_body != null ? ship.m_body.linearVelocity : Vector3.zero;
            return Vector3.Dot(velocity, gate.OutOf(GateGeometry.Opposite(centreSide))) > MinCrossingSpeed;
        }

        private static bool EndAcross(GateGeometry gate, Vector3 end, int centreSide, float through)
        {
            Vector3 local = gate.ToLocal(end);
            return GateGeometry.SideOf(local.z) != centreSide && Mathf.Abs(local.z) >= through && gate.WithinSpan(local.x);
        }

        /// <summary>Null when the ship stopped for its helmsman to choose, otherwise the reason token to show the crew.</summary>
        private static string Attempt(Ship ship, LoadedGate gate, int entrySide, bool helm)
        {
            if (!helm)
                return SeaGateWords.DeniedNoHelmsman;
            if (GateAccess.Blocked(out string blocked))
                return blocked;
            if (CargoCheck.Blocked(ship, out string cargo))
                return cargo ?? SeaGateWords.DeniedCargo;
            JumpChoice.Stop(ship, gate, entrySide);
            return null;
        }

        /// <summary>The helmsman closed the map in this gate and the ship's centre is still on the side it came from.</summary>
        private static bool SailsOn(Ship ship, long gateId, int side)
        {
            ZDO zdo = ship.m_nview.GetZDO();
            return zdo.GetLong(SeaGateFields.SailOnKey, 0L) == gateId && zdo.GetInt(SeaGateFields.SailOnSideKey, 0) == side;
        }

        private static void ForgetSailOn(Ship ship, long gateId)
        {
            ZDO zdo = ship.m_nview.GetZDO();
            if (zdo.GetLong(SeaGateFields.SailOnKey, 0L) == gateId)
                zdo.Set(SeaGateFields.SailOnKey, 0L);
        }

        /// <summary>Drops the memory of ships that are gone.</summary>
        internal static void Prune()
        {
            dead.Clear();
            foreach ((Ship, long) key in refused.Keys)
            {
                if (key.Item1 == null)
                    dead.Add(key);
            }
            foreach ((Ship, long) key in dead)
                refused.Remove(key);
        }
    }
}
