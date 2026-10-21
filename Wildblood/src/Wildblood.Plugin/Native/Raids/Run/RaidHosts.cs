// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The raid hosts this machine holds, listed as they wake and dropped as they go, so the clock, the HUD line and the
    /// players' presence reports find them without ever searching the scene. Usually none or one; a base with a few
    /// Raiders Chests a few more. The shared clock (<see cref="RaidTicker"/>) ticks them all from here. It also keeps the
    /// prefabs that can host a raid, for <see cref="RaidZdos"/> and the plunderers' targets (<see cref="AddHostPrefab"/>):
    /// the test marker (<see cref="RaidMarker"/>, at the plugin's start) and the Raiders Chest (<see cref="ChestPrefab"/>,
    /// as the scene wakes), on every peer.
    /// </summary>
    public static class RaidHosts
    {
        private static readonly List<RaidRunner> Live = new List<RaidRunner>();
        private static readonly HashSet<int> Prefabs = new HashSet<int>();

        /// <summary>Every live host this machine holds. Read only; never kept across frames.</summary>
        public static IReadOnlyList<RaidRunner> All => Live;

        /// <summary>The prefab hashes that can host a raid.</summary>
        internal static HashSet<int> HostPrefabs => Prefabs;

        /// <summary>Marks a prefab as a raid host (every peer, as its prefab is registered). For the Raiders Chest.</summary>
        public static void AddHostPrefab(string prefabName) => Prefabs.Add(prefabName.GetStableHashCode());

        internal static void Add(RaidRunner runner)
        {
            if (!Live.Contains(runner))
            {
                Live.Add(runner);
            }
        }

        // Swap-remove: the order means nothing. Called from OnDestroy, which Unity runs after the frame's ticks.
        internal static void Remove(RaidRunner runner)
        {
            int at = Live.IndexOf(runner);
            if (at < 0)
            {
                return;
            }
            Live[at] = Live[Live.Count - 1];
            Live.RemoveAt(Live.Count - 1);
        }

        /// <summary>The shared clock's tick: each host runs its raid if this machine owns it.</summary>
        internal static void TickAll()
        {
            long now = NetTime.NowMs();
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                Live[i].Tick(now);
            }
        }

        /// <summary>The nearest host within range whose raid is running; null when none is.</summary>
        public static RaidRunner? NearestRunning(Vector3 at, float range)
        {
            RaidRunner? best = null;
            float bestSqr = range * range;
            for (int i = 0; i < Live.Count; i++)
            {
                RaidRunner host = Live[i];
                float sqr = (host.Position - at).sqrMagnitude;
                if (sqr <= bestSqr && RaidState.IsRunning(host.Zdo))
                {
                    best = host;
                    bestSqr = sqr;
                }
            }
            return best;
        }
    }
}
