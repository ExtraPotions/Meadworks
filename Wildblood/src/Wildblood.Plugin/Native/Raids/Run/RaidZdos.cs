// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The raid hosts near a point by their ZDOs rather than their loaded objects: on the server every one in the world,
    /// on a client every one the server has sent it (the area around its player). A dedicated server holds no objects at
    /// all, and a host 200 m away may not be loaded on a client, so the 200 m spacing between raids and the 100 m shared
    /// chest cooldown look here. Not for a hot path: it walks every object in the zones around the point (up to 9 by 9
    /// zones for 200 m), filtering by the host prefabs (<see cref="RaidHosts.AddHostPrefab"/>) before reading anything.
    /// </summary>
    public static class RaidZdos
    {
        private static readonly List<ZDO> Sector = new List<ZDO>();
        private static readonly List<ZDO> Found = new List<ZDO>();

        /// <summary>Fills <paramref name="into"/> with the raid hosts' ZDOs within <paramref name="range"/> metres.</summary>
        public static void Near(Vector3 at, float range, List<ZDO> into)
        {
            into.Clear();
            if (ZDOMan.instance == null || RaidHosts.HostPrefabs.Count == 0)
            {
                return;
            }
            int zones = Mathf.CeilToInt(range / ZoneSystem.c_ZoneSize) + 1;
            Sector.Clear();
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(at), new SimulationDistance(zones, 0, classic: true), Sector);
            float reach = range * range;
            foreach (ZDO zdo in Sector)
            {
                if (RaidHosts.HostPrefabs.Contains(zdo.GetPrefab()) && (zdo.GetPosition() - at).sqrMagnitude <= reach
                    && !into.Contains(zdo))
                {
                    into.Add(zdo);
                }
            }
            Sector.Clear();
        }

        /// <summary>The nearest host within range, other than <paramref name="except"/>, whose raid is running; or null.</summary>
        public static ZDO? RunningNear(Vector3 at, float range, ZDOID except)
        {
            Near(at, range, Found);
            ZDO? best = null;
            float bestSqr = float.MaxValue;
            foreach (ZDO zdo in Found)
            {
                float sqr = (zdo.GetPosition() - at).sqrMagnitude;
                if (zdo.m_uid != except && sqr < bestSqr && RaidState.IsRunning(zdo))
                {
                    best = zdo;
                    bestSqr = sqr;
                }
            }
            Found.Clear();
            return best;
        }
    }
}
