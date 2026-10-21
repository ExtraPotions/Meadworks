// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/JumpChoice.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The ship owner's side of a stop. Every gate reaches every other: a ship that sails into a gate with
    /// someone at the helm stops dead in it (<see cref="JumpState.Choosing"/>, held still and unharmed by
    /// <see cref="ShipFreeze"/> like a jump) while the helmsman picks the destination on the map
    /// (<see cref="ThresholdSeaGatePicker"/>, on the helmsman's own client). The pick comes to the owner as
    /// <see cref="SeaGateFields.PickRpc"/> on the ship's ZNetView, taken only from the helmsman's machine and only for
    /// the stop under way; the owner asks the server for the jump (<see cref="SeaGateGrant"/>) and starts it on a grant
    /// (<see cref="JumpBegin"/>). A denial is told to the crew and the helmsman picks again. A pick of 0 (the map
    /// closed), nobody at the helm, the gate gone or sea gates switched off let the ship sail on with the speed it
    /// had. The whole stop is in the ship's ZDO, so a new owner carries it on.</summary>
    internal static class JumpChoice
    {
        internal static void Stop(Ship ship, LoadedGate gate, int side)
        {
            ZDO zdo = ship.m_nview.GetZDO();
            zdo.Set(SeaGateFields.SpeedKey, ship.GetSpeed());
            zdo.Set(SeaGateFields.StopKey, NewStopId(zdo));
            zdo.Set(SeaGateFields.StopGateKey, gate.Id);
            zdo.Set(SeaGateFields.StopSideKey, side);
            zdo.Set(SeaGateFields.PickedKey, 0L);
            zdo.Set(SeaGateFields.StateKey, (int)JumpState.Choosing);
            ShipFreeze.Hold(ship);
        }

        /// <summary>Owner, every frame while the ship is stopped (called by <see cref="ShipJump"/>).</summary>
        internal static void Tick(Ship ship, ZDO zdo, bool seaGatesOn)
        {
            LoadedGate gate = SeaGateRegistry.FindGate(zdo.GetLong(SeaGateFields.StopGateKey, 0L));
            if (gate == null || !seaGatesOn || !ship.HaveControllingPlayer())
            {
                SailOn(ship, zdo, null);
                return;
            }
            long picked = zdo.GetLong(SeaGateFields.PickedKey, 0L);
            if (picked != 0L)
                Pursue(ship, zdo, gate, picked);
        }

        private static void Pursue(Ship ship, ZDO zdo, LoadedGate gate, long picked)
        {
            if (SeaGateGrant.TryGet(gate.Id, picked, out JumpGrant grant))
            {
                Begin(ship, zdo, gate, grant);
                return;
            }
            string denial = SeaGateGrant.TakeDenial(gate.Id, picked);
            if (denial == null)
            {
                SeaGateGrant.Request(gate, picked, ship);
                return;
            }
            zdo.Set(SeaGateFields.PickedKey, 0L);
            TellCrew(ship, 0L, denial);
        }

        private static void Begin(Ship ship, ZDO zdo, LoadedGate gate, JumpGrant grant)
        {
            SeaGateGrant.Forget(gate.Id);
            int side = zdo.GetInt(SeaGateFields.StopSideKey, 0);
            if (CargoCheck.Blocked(ship, out string cargo))
                SailOn(ship, zdo, cargo ?? SeaGateWords.DeniedCargo);
            else if (!JumpBegin.TryBegin(ship, gate, grant, side, zdo.GetFloat(SeaGateFields.SpeedKey, 0f)))
                SailOn(ship, zdo, SeaGateWords.DeniedNotReady);
        }

        /// <summary>Back to sailing with the stored speed, sail and rudder; no stop in this gate again until the ship
        /// has left it or crossed it (<see cref="JumpCrossing"/>).</summary>
        private static void SailOn(Ship ship, ZDO zdo, string reasonToken)
        {
            zdo.Set(SeaGateFields.SailOnKey, zdo.GetLong(SeaGateFields.StopGateKey, 0L));
            zdo.Set(SeaGateFields.SailOnSideKey, zdo.GetInt(SeaGateFields.StopSideKey, 0));
            zdo.Set(SeaGateFields.StateKey, (int)JumpState.None);
            ShipFreeze.Release(ship, zdo.GetFloat(SeaGateFields.SpeedKey, 0f));
            if (reasonToken != null)
                TellCrew(ship, 0L, reasonToken);
        }

        /// <summary>Owner side of <see cref="SeaGateFields.PickRpc"/>: the helmsman picked a gate, or 0 to sail on.</summary>
        internal static void OnPick(Ship ship, long sender, int stopId, long gateId)
        {
            ZNetView view = ship != null ? ship.m_nview : null;
            if (view == null || !view.IsValid() || !view.IsOwner())
                return;
            ZDO zdo = view.GetZDO();
            bool thisStop = SeaGateFields.GetState(zdo) == JumpState.Choosing && zdo.GetInt(SeaGateFields.StopKey, 0) == stopId;
            if (!thisStop || !FromHelmsman(ship, sender))
                return;
            if (gateId == 0L)
                SailOn(ship, zdo, null);
            else if (zdo.GetLong(SeaGateFields.PickedKey, 0L) == 0L && gateId != zdo.GetLong(SeaGateFields.StopGateKey, 0L))
                Pick(zdo, gateId);
        }

        private static void Pick(ZDO zdo, long gateId)
        {
            SeaGateGrant.Forget(zdo.GetLong(SeaGateFields.StopGateKey, 0L));
            zdo.Set(SeaGateFields.PickedKey, gateId);
        }

        /// <summary>The sender is the machine that owns the helmsman's character: each client owns its own player,
        /// and the routed sender id is that client's session id.</summary>
        private static bool FromHelmsman(Ship ship, long sender)
        {
            long helmsmanId = HelmsmanId(ship);
            Player helmsman = helmsmanId != 0L ? Player.GetPlayer(helmsmanId) : null;
            return helmsman != null && helmsman.GetOwner() == sender;
        }

        private static long HelmsmanId(Ship ship)
        {
            ShipControlls controls = ship.m_shipControlls;
            return controls != null && controls.HaveValidUser() ? controls.GetUser() : 0L;
        }

        /// <summary>Everyone aboard but <paramref name="skipPlayerId"/>: <c>Player.Message</c> shows it here for the
        /// local player and sends the player's own "Message" RPC to the client that owns any other.</summary>
        internal static void TellCrew(Ship ship, long skipPlayerId, string token)
        {
            foreach (Player player in ship.m_players)
            {
                if (player != null && (skipPlayerId == 0L || player.GetPlayerID() != skipPlayerId))
                    player.Message(MessageHud.MessageType.Center, token);
            }
        }

        private static int NewStopId(ZDO zdo)
        {
            int previous = zdo.GetInt(SeaGateFields.StopKey, 0);
            int id = Random.Range(1, int.MaxValue);
            return id != previous ? id : (id == int.MaxValue - 1 ? 1 : id + 1);
        }
    }
}
