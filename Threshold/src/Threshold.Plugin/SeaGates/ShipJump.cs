// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/ShipJump.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Threshold.SeaGates;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The ship owner's side of a jump: crossing (<see cref="JumpCrossing"/>), the stop while the helmsman
    /// picks the destination (<see cref="JumpChoice"/>), begin (<see cref="JumpBegin"/>), move, settle
    /// (<see cref="JumpSettle"/>) and release. It runs on whichever machine owns
    /// each ship's ZDO and has the ship loaded; every step reads the jump from the ship's ZDO, so an owner that changes
    /// mid-jump (<c>Ship.UpdateOwner</c> hands a ship to a player aboard; the server hands a ZDO to a peer near it when
    /// its owner is far away) carries on from where the last one stopped. Moving the ship's ZDO to the destination
    /// takes it out of the source area, so the source machines drop their copy of the ship (<c>ZNetScene.RemoveObjects</c>;
    /// a ship's ZDO is persistent and survives that) and the owner recreates it once its own player reaches the
    /// destination. Nothing lets a ship go before this machine has the water under it loaded.</summary>
    public static class ShipJump
    {
        private const float PruneSeconds = 5f;

        // A landed player is set a few cm above the deck; the ship gets its speed back only once they stand on it,
        // or a ship at full sail would slide out from under them. Counted from the latest landing in the ship's ZDO,
        // so it holds through an ownership change and when the wait is over too.
        private const float LandGraceSeconds = 1f;

        private static float prunedAt;
        private static bool tickFailed;

        /// <summary>Every frame on every machine (called by <see cref="SeaGates"/>). Jumps already under way always
        /// go on to their release, whatever the settings say; only new ones need sea gates switched on, and a ship
        /// stopped to choose sails on when they are switched off.</summary>
        public static void Tick()
        {
            if (ZNet.instance == null || ZNetScene.instance == null)
                return;
            Prune();
            JumpSettle.TrackReference();
            bool mayStart = SeaGateConfig.On;
            List<Ship> ships = JumpShips.Live;
            for (int i = 0; i < ships.Count; i++)
                TickGuarded(ships[i], mayStart);
        }

        /// <summary>Each ship on its own: one ship that throws (a modded ship missing a part the game expects) must not
        /// stop the ships after it in the list, or a frozen one among them would never be released.</summary>
        private static void TickGuarded(Ship ship, bool mayStart)
        {
            try
            {
                TickShip(ship, mayStart);
            }
            catch (Exception e)
            {
                if (!tickFailed)
                    SeaGateLog.LogError($"Sea gate jump failed for {(ship != null ? ship.name : "a ship")}, the other ships go on (logged once): {e}");
                tickFailed = true;
            }
        }

        private static void TickShip(Ship ship, bool mayStart)
        {
            ZNetView view = ship.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner())
                return;
            ZDO zdo = view.GetZDO();
            switch (SeaGateFields.GetState(zdo))
            {
                case JumpState.None:
                    if (mayStart)
                        JumpCrossing.Check(ship);
                    break;
                case JumpState.Choosing:
                    JumpChoice.Tick(ship, zdo, mayStart);
                    break;
                case JumpState.Frozen:
                    TryMove(ship, zdo);
                    break;
                case JumpState.Moved:
                    JumpSettle.TrySettle(ship, zdo);
                    break;
                case JumpState.Settled:
                    TryRelease(ship, zdo);
                    break;
            }
        }

        /// <summary>A second after the freeze, when every crew client has measured its deck spot against the ship
        /// standing still, the ship goes to the destination pose. Still frozen: the destination may not be loaded.</summary>
        private static void TryMove(Ship ship, ZDO zdo)
        {
            if (SeaGateFields.Now < zdo.GetLong(SeaGateFields.MoveAtKey, 0L))
                return;
            ShipFreeze.Hold(ship);
            Vector3 pos = zdo.GetVec3(SeaGateFields.DestPosKey, ship.transform.position);
            Quaternion rot = zdo.GetQuaternion(SeaGateFields.DestRotKey, ship.transform.rotation);
            ShipFreeze.Place(ship, pos, rot);
            zdo.Set(SeaGateFields.StateKey, (int)JumpState.Moved);
        }

        /// <summary>When every crew member has landed, or the wait is over; never within a second of the latest landing,
        /// and only where this machine has the area and its water loaded: otherwise the ship stays frozen and ownership
        /// moves on to a peer nearer to it. The safe time is written before the state, so the ship is never unfrozen
        /// without it.</summary>
        private static void TryRelease(Ship ship, ZDO zdo)
        {
            bool waitOver = SeaGateFields.Now > zdo.GetLong(SeaGateFields.UntilKey, 0L);
            if (!waitOver && !AllLanded(zdo))
                return;
            if (LandedRecently(zdo) || !JumpSettle.ReadyToRelease(ship))
                return;
            zdo.Set(SeaGateFields.SafeKey, SeaGateFields.After(SeaGateConfig.ProtectionSeconds.Value));
            zdo.Set(SeaGateFields.StateKey, (int)JumpState.None);
            ShipFreeze.Release(ship, zdo.GetFloat(SeaGateFields.SpeedKey, 0f));
        }

        private static bool AllLanded(ZDO zdo) =>
            zdo.GetInt(SeaGateFields.LandedKey, 0) >= zdo.GetInt(SeaGateFields.CrewKey, 0);

        /// <summary>Either way round: a landing counted by the previous owner may read a moment ahead of this machine's
        /// clock, while a time far ahead (a clock set back) must not hold the ship.</summary>
        private static bool LandedRecently(ZDO zdo) =>
            Math.Abs(SeaGateFields.Now - zdo.GetLong(SeaGateFields.LandedAtKey, 0L)) < (long)(LandGraceSeconds * TimeSpan.TicksPerSecond);

        /// <summary>Owner side of <see cref="SeaGateFields.LandedRpc"/>: a crew member landed, was set ashore, or will not
        /// come. Counts once per player and only for the jump under way, by a record in the ship's ZDO, so a report sent
        /// again (<see cref="CrewReport"/>) or reaching the next owner after a handover never counts twice. A report
        /// that reaches a machine that is no longer the owner is dropped; the crew client sends it again. The sender
        /// must own that player, or be the server: another client cannot mark the rest of the crew as landed.</summary>
        public static void OnLanded(Ship ship, long sender, int jumpId, long playerId)
        {
            ZNetView view = ship != null ? ship.m_nview : null;
            if (view == null || !view.IsValid() || !view.IsOwner() || jumpId == 0)
                return;
            Player crew = Player.GetPlayer(playerId);
            long owner = crew != null ? crew.GetOwner() : 0L;
            if (!SeaGateAuthority.AcceptsLanding(SenderIdentity.IsFromServer(sender), sender, SenderIdentity.PlayerId(sender), playerId, owner))
                return;
            ZDO zdo = view.GetZDO();
            int record = LandedRecord(playerId);
            if (!SeaGateFields.IsJumping(zdo) || SeaGateFields.GetJump(zdo) != jumpId || zdo.GetInt(record, 0) == jumpId)
                return;
            zdo.Set(record, jumpId);
            zdo.Set(SeaGateFields.LandedKey, zdo.GetInt(SeaGateFields.LandedKey, 0) + 1);
            zdo.Set(SeaGateFields.LandedAtKey, SeaGateFields.Now);
        }

        /// <summary>The ship's ZDO shows this player's landing counted for this jump.</summary>
        public static bool IsCounted(ZDO ship, int jumpId, long playerId) =>
            ship != null && jumpId != 0 && ship.GetInt(LandedRecord(playerId), 0) == jumpId;

        private static int LandedRecord(long playerId) => (SeaGateFields.LandedPlayerPrefix + playerId).GetStableHashCode();

        private static void Prune()
        {
            if (Time.time - prunedAt < PruneSeconds)
                return;
            prunedAt = Time.time;
            ShipFreeze.Prune();
            JumpCrossing.Prune();
        }
    }
}
