// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Who is at the raid, and whether anyone still is (features/raids.md, "Abandoned": no player within 96 m for 60
    /// seconds, a logout or a server restart). On the owner, every tick: the players it holds within range plus those
    /// that reported in (<see cref="RaidHere"/>). Into the host's ZDO go the last time anyone was there (stamped every
    /// few seconds, not every tick), which machine ran the tick, whether its own player was the only one there, and the
    /// server's session. The world clock stops on a dedicated server while nobody is connected, so a minute of absence
    /// cannot catch a logout: instead, a machine taking the raid over from one that is no longer connected, and whose
    /// player was alone at the raid, knows that player logged out; a dedicated server running the raid itself sees its
    /// last player go (<see cref="ServerEmpty"/>). A server that restarted has a new session id.
    /// </summary>
    internal static class RaidPresence
    {
        private static readonly List<long> Peers = new List<long>();

        /// <summary>The owner's count of the players at a raid.</summary>
        public readonly struct Count
        {
            public Count(int players, int strongestTier, bool localNear)
            {
                Players = players;
                StrongestTier = strongestTier;
                LocalNear = localNear;
            }

            public int Players { get; }

            public int StrongestTier { get; }

            public bool LocalNear { get; }

            /// <summary>True when nobody but this machine's own player (or nobody at all) is at the raid.</summary>
            public bool Alone => Players == 0 || (Players == 1 && LocalNear);
        }

        /// <summary>The players at the raid now, seen or heard from.</summary>
        public static Count Of(RaidRunner runner)
        {
            int seen = RaidPlayers.Collect(runner.Position, RaidTable.RaidRadius, Peers, out bool local);
            int heard = runner.Heard.AddFresh(Peers, Time.time);
            return new Count(Peers.Count, Mathf.Max(seen, heard), local);
        }

        /// <summary>
        /// True on a dedicated server that runs the raid itself (it keeps the land around the world's centre loaded, so a
        /// base by the spawn is its) when every player has logged out: its clock stands still from then, so the minute of
        /// absence would never run out, and nobody can take the raid over to see the logout either.
        /// </summary>
        public static bool ServerEmpty()
        {
            ZNet net = ZNet.instance;
            return net != null && net.IsDedicated() && net.GetNrOfPlayers() == 0;
        }

        /// <summary>At the sounding: this machine runs the raid, under this server.</summary>
        public static void Claim(RaidState state, Count near)
        {
            state.Runner = ZNet.GetUID();
            state.Server = ServerSession();
            state.Alone = near.Alone;
        }

        /// <summary>Every owner tick, after the end checks: who ran it, whether alone, and when anyone was last there.</summary>
        public static void Stamp(RaidState state, Count near, long now)
        {
            if (near.Players > 0 && now - state.SeenAt >= RaidState.Ms(RaidTable.SeenStampSeconds))
            {
                state.SeenAt = now;
            }
            state.Runner = ZNet.GetUID();
            state.Alone = near.Alone;
        }

        /// <summary>True when the server is not the one the raid started under: it restarted since.</summary>
        public static bool ServerRestarted(RaidState state)
        {
            long then = state.Server, now = ServerSession();
            return then != 0L && now != 0L && then != now;
        }

        /// <summary>True when this machine takes over from one that has gone, whose player was alone at the raid.</summary>
        public static bool RunnerLoggedOut(RaidState state)
        {
            long previous = state.Runner;
            return previous != 0L && previous != ZNet.GetUID() && state.Alone && !Connected(previous);
        }

        /// <summary>The server's session id as this machine knows it; 0 before it is connected.</summary>
        private static long ServerSession()
        {
            ZNet net = ZNet.instance;
            if (net == null)
            {
                return 0L;
            }
            return net.IsServer() ? ZNet.GetUID() : net.GetServerPeer()?.m_uid ?? 0L;
        }

        // A peer is connected while the server's player list names its character (whose id carries the peer's), or it
        // is the server itself.
        private static bool Connected(long peer)
        {
            if (peer == ServerSession() || ZNet.instance == null)
            {
                return true;
            }
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                if (info.m_characterID.UserID == peer)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
