// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/CrewReport.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>A crew client's word to the ship's owner (<see cref="SeaGateFields.LandedRpc"/>: jump id, player id),
    /// counted once per player whichever machine owns the ship (<see cref="ShipJump.OnLanded"/>). After a landing, on
    /// deck or ashore, it goes to the owner this machine knows; a report that reaches a machine that has just handed the
    /// ship on is dropped there, so it is sent again once a second until the ship's ZDO shows it counted, the jump has
    /// ended, or a few seconds have passed (the ship then stops waiting at its own time). A crew member who will not
    /// come (not aboard on this machine, dead, already in a teleport) answers the captain at once, so the ship does not
    /// wait for them.</summary>
    internal static class CrewReport
    {
        private const float RetrySeconds = 1f;
        private const float GiveUpSeconds = 12f;

        private static ZDOID ship = ZDOID.None;
        private static int jump;
        private static long playerId;
        private static float nextTry;
        private static float until;

        /// <summary>After a landing: report now, and again until it is counted.</summary>
        internal static void Landed(ZDOID shipId, int jumpId, long player)
        {
            ship = shipId;
            jump = jumpId;
            playerId = player;
            until = Time.time + GiveUpSeconds;
            nextTry = Time.time + RetrySeconds;
            SendToOwner();
        }

        /// <summary>Not coming: once, straight back to the captain who sent the order.</summary>
        internal static void Decline(long captain, JumpOrder order, Player player)
        {
            if (player == null || order == null || order.Ship.IsNone() || ZRoutedRpc.instance == null)
                return;
            ZRoutedRpc.instance.InvokeRoutedRPC(captain, order.Ship, SeaGateFields.LandedRpc, order.JumpId, player.GetPlayerID());
        }

        internal static void Clear() => ship = ZDOID.None;

        /// <summary>Every physics step of the local player outside a jump; works only while a report is pending.</summary>
        internal static void Tick()
        {
            if (ship.IsNone() || Time.time < nextTry)
                return;
            nextTry = Time.time + RetrySeconds;
            if (Time.time > until || Done(CrewShip.Zdo(ship)))
            {
                Clear();
                return;
            }
            SendToOwner();
        }

        /// <summary>Nothing left to report: the ship's ZDO shows this landing counted, or that jump is over. Without the
        /// ZDO here it keeps trying until the time is up.</summary>
        private static bool Done(ZDO zdo)
        {
            if (zdo == null)
                return false;
            return !SeaGateFields.IsJumping(zdo) || SeaGateFields.GetJump(zdo) != jump || ShipJump.IsCounted(zdo, jump, playerId);
        }

        /// <summary>By the ship's ZDO to its owner, as <c>ZNetView.InvokeRPC</c> does: a routed RPC aimed at the ZDO
        /// reaches the owner's instance, no local copy needed. Without a known owner nothing is sent (a target of 0
        /// would reach every machine).</summary>
        private static void SendToOwner()
        {
            ZDO zdo = CrewShip.Zdo(ship);
            if (zdo == null || zdo.GetOwner() == 0L || ZRoutedRpc.instance == null)
                return;
            ZRoutedRpc.instance.InvokeRoutedRPC(zdo.GetOwner(), ship, SeaGateFields.LandedRpc, jump, playerId);
        }
    }
}
