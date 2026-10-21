// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Whether this world has unlocked one of the game's events, by the game's own rules (<c>RandEventSystem</c>, which
    /// draws its raids the same way): the event is switched on and one the game draws at random, its required global
    /// keys (the bosses killed) are set and its forbidden ones are not. With the player-based raids world setting
    /// (<c>GlobalKeys.PlayerEvents</c>), an event that names player requirements is instead decided per player, as the
    /// game does: it is unlocked when a player at the raid is ready for it. A machine can judge only the players it knows
    /// that of - its own (known items and keys live on the player's client) and, on the server, every connected player,
    /// from the ready list each client sends it.
    /// </summary>
    internal static class RaidUnlock
    {
        /// <summary>True when the world has unlocked the event for a raid at <paramref name="at"/>.</summary>
        public static bool Unlocked(RandomEvent ev, Vector3 at)
        {
            if (!ev.m_enabled || !ev.m_random || ZoneSystem.instance == null)
            {
                return false;
            }
            return ByPlayers(ev) ? PlayerReadyNear(ev, at) : KeysMet(ev);
        }

        // The game's own switch: with player-based raids on, an event that names player requirements is decided per player.
        private static bool ByPlayers(RandomEvent ev) =>
            ZoneSystem.instance.GetGlobalKey(GlobalKeys.PlayerEvents)
            && (ev.m_altRequiredKnownItems.Count > 0 || ev.m_altRequiredNotKnownItems.Count > 0
                || ev.m_altNotRequiredPlayerKeys.Count > 0 || ev.m_altRequiredPlayerKeysAny.Count > 0
                || ev.m_altRequiredPlayerKeysAll.Count > 0);

        private static bool KeysMet(RandomEvent ev)
        {
            foreach (string key in ev.m_requiredGlobalKeys)
            {
                if (!ZoneSystem.instance.GetGlobalKey(key))
                {
                    return false;
                }
            }
            foreach (string key in ev.m_notRequiredGlobalKeys)
            {
                if (ZoneSystem.instance.GetGlobalKey(key))
                {
                    return false;
                }
            }
            return true;
        }

        // This machine's own player, asked as the game asks it; on the server, every connected player at the raid too.
        private static bool PlayerReadyNear(RandomEvent ev, Vector3 at)
        {
            Player local = Player.m_localPlayer;
            if (local != null && !local.IsDead() && Near(local.transform.position, at) && RandEventSystem.instance != null
                && RandEventSystem.instance.PlayerIsReadyForEvent(local, ev))
            {
                return true;
            }
            return ZNet.instance != null && ZNet.instance.IsServer() && PeerReadyNear(ev, at);
        }

        // Each client keeps the events its player is ready for in the data it syncs to the server, as the game reads it.
        private static bool PeerReadyNear(RandomEvent ev, Vector3 at)
        {
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (peer.IsReady() && Near(peer.m_refPos, at)
                    && peer.m_serverSyncedPlayerData.TryGetValue(RandEventSystem.PossibleEventsKey, out string ready)
                    && Array.IndexOf(ready.Split(','), ev.m_name) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Near(Vector3 a, Vector3 b) => Utils.DistanceXZ(a, b) <= RaidTable.RaidRadius;
    }
}
