// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The living players near a point, as this machine holds them (the game's own player list, never a scene search):
    /// their peers, the strongest published gear tier among them, and whether this machine's own player is one of them.
    /// The chest's hover asks it for the heat before a raid is sounded; the raid's owner adds the players that reported
    /// in (<see cref="RaidPresence"/>), since players beyond the area loaded around a machine are not held there.
    /// </summary>
    public static class RaidPlayers
    {
        private static readonly List<long> Scratch = new List<long>();

        /// <summary>One survey's answer.</summary>
        public readonly struct Survey
        {
            public Survey(int count, int strongestTier, bool localNear)
            {
                Count = count;
                StrongestTier = strongestTier;
                LocalNear = localNear;
            }

            /// <summary>Living players within range.</summary>
            public int Count { get; }

            /// <summary>The highest gear tier among them; 0 with none.</summary>
            public int StrongestTier { get; }

            /// <summary>True when this machine's own player is among them.</summary>
            public bool LocalNear { get; }
        }

        /// <summary>The living players this machine holds within <paramref name="range"/> metres of <paramref name="at"/>.</summary>
        public static Survey Near(Vector3 at, float range)
        {
            int strongest = Collect(at, range, Scratch, out bool local);
            return new Survey(Scratch.Count, strongest, local);
        }

        /// <summary>
        /// Fills <paramref name="peers"/> with the peer of each living player within range (one per player, its own
        /// client) and returns the strongest gear tier among them.
        /// </summary>
        public static int Collect(Vector3 at, float range, List<long> peers, out bool localNear)
        {
            peers.Clear();
            localNear = false;
            int strongest = 0;
            float reach = range * range;
            List<Player> players = Player.GetAllPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                Player p = players[i];
                ZDO? zdo = p != null && !p.IsDead() && p.m_nview != null ? p.m_nview.GetZDO() : null;
                if (zdo == null || (p!.transform.position - at).sqrMagnitude > reach || peers.Contains(zdo.GetOwner()))
                {
                    continue;
                }
                peers.Add(zdo.GetOwner());
                strongest = Mathf.Max(strongest, GearTier.Of(p));
                localNear |= ReferenceEquals(p, Player.m_localPlayer);
            }
            return strongest;
        }
    }
}
