// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateGrantServer.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later

using ExtraPotions.Threshold.Access;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The server's half of <see cref="SeaGateGrant"/>: whether ships may jump from one gate. The server holds
    /// every pillar ZDO, so it alone can find a distant destination, and it is the one party every client trusts to
    /// judge access - the same reasoning as for portal trips. The destination is the gate the helmsman picked;
    /// access is that gate's, read from its anchor (which carries the portal mode and owner keys) for the asking
    /// player, by the portals' own rule. Both gates are looked up in the server's index of pillars
    /// (<see cref="SeaGateScan"/>), never by searching the world.</summary>
    internal static class SeaGateGrantServer
    {
        internal static void OnRequest(long sender, long sourceId, long destId, ZDOID shipId)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || !Answerable)
                return;
            string denial = Evaluate(sender, sourceId, destId, shipId, out ZDO destAnchor, out ZDO destPartner);
            if (denial != null)
                SeaGateGrant.SendDenial(sender, sourceId, destId, denial);
            else
                SeaGateGrant.SendGrant(sender, sourceId, destId, destAnchor, destPartner);
        }

        /// <summary>The index holds every pillar of the world, so a gate it does not know is gone, or sea gates are off and
        /// every request is refused anyway. In the second or two after the world loaded, while the index is still being
        /// filled, a request is left unanswered rather than wrongly refused: the ship's owner asks again every 2 s
        /// (<see cref="SeaGateGrant.Request"/>).</summary>
        private static bool Answerable =>
            SeaGateScan.Seeded || !SeaGateConfig.On;

        /// <summary>Null when the jump is granted, otherwise the denial token to send back.</summary>
        private static string Evaluate(long sender, long sourceId, long destId, ZDOID shipId, out ZDO destAnchor, out ZDO destPartner)
        {
            destAnchor = null;
            destPartner = null;
            if (!SeaGateConfig.On)
                return SeaGateGrant.DeniedClosed;
            if (GateAccess.Blocked(out string blockedReason))
                return blockedReason;
            if (!SeaGateIndex.TryFindOnServer(sourceId, out _, out _))
                return SeaGateGrant.DeniedClosed;
            if (destId == sourceId)
                return SeaGateWords.DeniedGone;
            // A gate with no side deep enough to exit on is never paired, but a ship must never be sent to one.
            if (!SeaGateIndex.TryFindOnServer(destId, out destAnchor, out destPartner) || SeaGateFields.GetSides(destAnchor) == 0)
                return SeaGateWords.DeniedGone;
            Asker(sender, shipId, out long playerId, out bool isAdmin);
            return AccessDenial(playerId, isAdmin, destAnchor);
        }

        /// <summary>The player access is judged for: the ship's helmsman (the ship's own <c>user</c> key, which its
        /// helm writes) when the asking machine owns that ship, else the asking player. A ship near the world centre can
        /// be owned by a dedicated server, which has no player of its own; a ship owned by someone on the dock is
        /// steered by someone else.</summary>
        private static void Asker(long sender, ZDOID shipId, out long playerId, out bool isAdmin)
        {
            playerId = SenderIdentity.PlayerId(sender);
            isAdmin = SenderIdentity.IsAdmin(sender);
            ZDO ship = shipId.IsNone() || ZDOMan.instance == null ? null : ZDOMan.instance.GetZDO(shipId);
            long helmsman = ship != null && ship.GetOwner() == sender ? ship.GetLong(ZDOVars.s_user, 0L) : 0L;
            if (helmsman == 0L || helmsman == playerId)
                return;
            playerId = helmsman;
            isAdmin = IsAdminPlayer(helmsman);
        }

        private static bool IsAdminPlayer(long playerId)
        {
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (peer != null && SenderIdentity.PeerPlayerId(peer) == playerId && peer.m_socket != null)
                    return ZNet.instance.IsAdmin(peer.m_socket.GetHostName());
            }
            return false;
        }

        /// <summary>Null when the asking player may sail to the destination; otherwise why not, by the same logic as
        /// the portals' denial reason (an Admin gate says so, anything else is the owner's).</summary>
        private static string AccessDenial(long playerId, bool isAdmin, ZDO destAnchor)
        {
            if (GateAccess.MayTarget(destAnchor, playerId, isAdmin))
                return null;
            return GateAccess.GetMode(destAnchor) == AccessMode.Admin ? SeaGateGrant.DeniedAdmin : SeaGateGrant.DeniedPrivate;
        }
    }
}
