// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/CrewJump.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Threshold.SeaGates;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>A crew member's client receiving a <see cref="JumpOrder"/>: leave the helm or seat, measure the deck
    /// spot against this client's own copy of the ship (the one the player stands on, so lag doesn't matter), and start
    /// the game's own teleport, which <see cref="CrewHold"/> then holds and ends. Game facts this relies on (verified in
    /// the decompiled assembly): <c>Player.TeleportTo</c> refuses while already teleporting or while
    /// <c>m_teleportCooldown &lt; 2</c> (reset to 0 by every teleport), so the cooldown is cleared first;
    /// <c>Ship.IsPlayerInBoat</c> reads the ship's trigger list, which every machine keeps for its own copy.</summary>
    public static class CrewJump
    {
        public const string JumpRpc = "threshold.SeaGateJump";

        // The instance registered on, as in TeleportGate: every session builds a fresh ZRoutedRpc with empty tables.
        private static ZRoutedRpc registeredOn;
        private static int lastJump;

        public static void EnsureRegistered()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || registeredOn == rpc)
                return;
            registeredOn = rpc;
            lastJump = 0;
            rpc.Register<ZPackage>(JumpRpc, OnJump);
        }

        /// <summary>Ship owner: send the order to one crew member's peer (this machine's own uid for the local player).
        /// A target equal to this machine's own routed id is handled at once, inside this call:
        /// <c>ZRoutedRpc.InvokeRoutedRPC</c> then calls <c>HandleRoutedRPC</c> directly and routes nothing. A remote
        /// target goes through the server, which forwards it to that peer.</summary>
        public static void Send(long peerUid, JumpOrder order)
        {
            if (order == null || ZRoutedRpc.instance == null)
                return;
            EnsureRegistered();
            ZRoutedRpc.instance.InvokeRoutedRPC(peerUid, JumpRpc, order.Write());
        }

        /// <summary>Guarded: a local order runs inside the owner's own jump tick, and one crew member's failure must not
        /// stop the orders to the others.</summary>
        private static void OnJump(long sender, ZPackage pkg)
        {
            try
            {
                Receive(sender, pkg);
            }
            catch (Exception e)
            {
                SeaGateLog.LogError($"Sea gate jump order failed: {e}");
            }
        }

        /// <summary>Followed whatever this machine's settings say: the captain began the jump under the server's
        /// settings, and a crew member who stays behind is left in the water where the ship was. One who will not come
        /// (not aboard this machine's copy of the ship, standing on another ship, dead, already in a teleport) answers the
        /// captain at once, so the ship does not wait for them.</summary>
        private static void Receive(long sender, ZPackage pkg)
        {
            if (pkg == null)
                return;
            JumpOrder order = JumpOrder.Read(pkg);
            if (order.JumpId == 0 || order.JumpId == lastJump)
                return;
            Player player = Player.m_localPlayer;
            Ship ship = CrewShip.Find(order.Ship);
            if (ship != null && !FromCaptain(sender, ship))
                return;
            lastJump = order.JumpId;
            if (ship == null || !CanJump(player) || !Aboard(player, ship))
            {
                CrewReport.Decline(sender, order, player);
                return;
            }
            Begin(player, ship, order, sender);
        }

        private static bool CanJump(Player player)
        {
            if (player == null || CrewHold.Holding || player.IsDead() || player.IsTeleporting())
                return false;
            return player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner();
        }

        /// <summary>In this ship's trigger and not standing on another ship: two ships side by side can share a
        /// player in their triggers, and the one they stand on is theirs.</summary>
        private static bool Aboard(Player player, Ship ship)
        {
            Ship standingOn = player.GetStandingOnShip();
            return ship.IsPlayerInBoat(player) && (standingOn == null || standingOn == ship);
        }

        /// <summary>The ship's owner as this machine sees it, the server, or a player aboard this ship: an ownership
        /// change reaches the owner itself before it reaches the crew, so the order of a captain who just took the
        /// ship may arrive while this machine still names the previous owner.</summary>
        private static bool FromCaptain(long sender, Ship ship)
        {
            ZDO zdo = ship.m_nview != null ? ship.m_nview.GetZDO() : null;
            if ((zdo != null && sender == zdo.GetOwner()) || SenderIdentity.IsFromServer(sender))
                return true;
            foreach (Player crew in ship.m_players)
            {
                if (crew != null && crew.GetOwner() == sender)
                    return true;
            }
            return false;
        }

        private static void Begin(Player player, Ship ship, JumpOrder order, long captain)
        {
            Transform frame = ship.transform;
            Transform self = player.transform;
            CrewSpot stood = CrewSpot.Measure(frame.position, frame.rotation, self.position, self.rotation);
            player.m_teleportCooldown = Mathf.Max(player.m_teleportCooldown, 2f);
            bool teleported = player.TeleportTo(stood.PositionOn(order.DestPos, order.DestRot), stood.FacingOn(order.DestRot), distantTeleport: true);
            CrewTeleportDecision decision = CrewTeleportDecision.AfterAttempt(order.JumpId, teleported);
            if (!decision.ReleaseHelm)
            {
                lastJump = decision.LastJump;
                if (decision.Decline)
                    CrewReport.Decline(captain, order, player);
                SeaGateLog.LogWarning("Sea gate: the game refused the crew teleport; this player stays where they are.");
                return;
            }
            bool hadHelm = CrewHelm.Release(player, ship);
            self = player.transform;
            CrewSpot spot = CrewSpot.Measure(frame.position, frame.rotation, self.position, self.rotation);
            player.m_teleportTargetPos = spot.PositionOn(order.DestPos, order.DestRot);
            player.m_teleportTargetRot = spot.FacingOn(order.DestRot);
            CrewHold.Begin(player, order, spot, hadHelm);
        }
    }
}
