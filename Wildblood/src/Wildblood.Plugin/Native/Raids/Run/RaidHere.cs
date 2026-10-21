// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// "My player is here": each client near a running raid it does not run tells the host's owner so every few seconds,
    /// with its player's gear tier. The owner keeps a raid's ZDO while the host is anywhere in the area loaded around it,
    /// which reaches well past the raid's 96 m, so a player fighting on the far side may be one the owner does not hold at
    /// all; without this the raid would count them as gone and be abandoned around them. The reports live only on the
    /// owner, in memory: after a hand-over the new owner has them again within one interval.
    /// </summary>
    internal static class RaidHere
    {
        /// <summary>Seconds between a client's reports.</summary>
        public const float IntervalSeconds = 3f;

        /// <summary>Seconds a report counts for: a little over two intervals, so one lost report changes nothing.</summary>
        private const float FreshSeconds = 7f;

        private static float _nextPing;

        /// <summary>Every client, from the shared clock: reports its player to each running raid it is within.</summary>
        public static void PingDue(float now)
        {
            Player player = Player.m_localPlayer;
            if (now < _nextPing || player == null || player.IsDead())
            {
                return;
            }
            _nextPing = now + IntervalSeconds;
            float reach = RaidTable.RaidRadius * RaidTable.RaidRadius;
            IReadOnlyList<RaidRunner> hosts = RaidHosts.All;
            for (int i = 0; i < hosts.Count; i++)
            {
                RaidRunner host = hosts[i];
                ZDO? zdo = host.Zdo;
                if (zdo != null && zdo.HasOwner() && !zdo.IsOwner()
                    && (host.Position - player.transform.position).sqrMagnitude <= reach && host.State.Running)
                {
                    host.View.InvokeRPC(RaidKeys.HereRpc, GearTier.Of(player));
                }
            }
        }

        /// <summary>One owner's record of the players that reported to it: peer, when, and gear tier.</summary>
        public sealed class Reports
        {
            private readonly Dictionary<long, KeyValuePair<float, int>> _byPeer = new Dictionary<long, KeyValuePair<float, int>>();
            private readonly List<long> _stale = new List<long>();

            public void Note(long peer, int tier, float now) =>
                _byPeer[peer] = new KeyValuePair<float, int>(now, Mathf.Clamp(tier, 0, RaidTable.MaxTier));

            /// <summary>Adds each fresh reporter not already in <paramref name="peers"/>; returns the highest tier reported.</summary>
            public int AddFresh(List<long> peers, float now)
            {
                int strongest = 0;
                _stale.Clear();
                foreach (KeyValuePair<long, KeyValuePair<float, int>> report in _byPeer)
                {
                    if (now - report.Value.Key > FreshSeconds)
                    {
                        _stale.Add(report.Key);
                        continue;
                    }
                    strongest = Mathf.Max(strongest, report.Value.Value);
                    if (!peers.Contains(report.Key))
                    {
                        peers.Add(report.Key);
                    }
                }
                foreach (long peer in _stale)
                {
                    _byPeer.Remove(peer);
                }
                return strongest;
            }
        }
    }
}
