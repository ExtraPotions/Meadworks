// Keel - ExtraPotions Meadworks ships and sailing.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>The player behind a routed ship call: a connected peer's id, or this machine's player when it routed the call to itself.</summary>
    internal static class ShipCallGate
    {
        internal static long PlayerId(long sender)
        {
            var net = ZNet.instance;
            if (net == null) return 0L;
            var peer = net.GetPeer(sender);
            if (peer != null)
            {
                if (peer.m_playerID != 0L) return peer.m_playerID;
                if (!peer.m_characterID.IsNone() && ZDOMan.instance != null)
                {
                    var character = ZDOMan.instance.GetZDO(peer.m_characterID);
                    if (character != null) return character.GetLong(ZDOVars.s_playerID, 0L);
                }
                return 0L;
            }
            if (ZRoutedRpc.instance != null && sender == ZRoutedRpc.instance.m_id && Player.m_localPlayer != null)
                return Player.m_localPlayer.GetPlayerID();
            return 0L;
        }

        internal static bool Aboard(Ship ship, long playerId)
        {
            if (ship == null || playerId == 0L || ship.m_players == null) return false;
            for (var i = 0; i < ship.m_players.Count; i++)
            {
                var player = ship.m_players[i];
                if (player != null && player.GetPlayerID() == playerId) return true;
            }
            return false;
        }

        internal static bool Controlling(Ship ship, long playerId)
        {
            var controls = ship != null ? ship.m_shipControlls : null;
            return playerId != 0L && controls != null && controls.GetUser() == playerId;
        }
    }
}
