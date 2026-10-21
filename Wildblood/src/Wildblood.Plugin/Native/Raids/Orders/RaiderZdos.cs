// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Which machines own a raid's raiders, found by the raiders' ZDOs around the host rather than by loaded objects: the
    /// host's owner may hold a raider's ZDO without owning it, and the stop and the walk off go to each raider's owner
    /// (features/raids.md section 5). Not for a hot path - it walks every object in the zones within reach of the host -
    /// and called only as a raid ends. A raider beyond what this machine was sent is not found; it reads the end from the
    /// host's ZDO on its own (<see cref="RaiderSteering"/>).
    /// </summary>
    internal static class RaiderZdos
    {
        /// <summary>Metres from the host searched: the raid's 96 m and the 60 m ring the waves come in from.</summary>
        private const float Reach = RaidTable.RaidRadius + RaidTable.SpawnRingMax;

        private static readonly List<ZDO> Sector = new List<ZDO>();

        /// <summary>Fills <paramref name="into"/> with the other machines owning a raider of this raid, each once.</summary>
        public static void Owners(Vector3 at, ZDOID host, long raid, List<long> into)
        {
            into.Clear();
            if (ZDOMan.instance == null)
            {
                return;
            }
            int zones = Mathf.CeilToInt(Reach / ZoneSystem.c_ZoneSize) + 1;
            Sector.Clear();
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(at), new SimulationDistance(zones, 0, classic: true), Sector);
            long self = ZDOMan.GetSessionID();
            foreach (ZDO zdo in Sector)
            {
                long owner = zdo.GetOwner();
                if (owner != 0L && owner != self && !into.Contains(owner) && Of(zdo, host, raid))
                {
                    into.Add(owner);
                }
            }
            Sector.Clear();
        }

        private static bool Of(ZDO zdo, ZDOID host, long raid) =>
            RaiderTag.IsRaider(zdo) && RaiderTag.Raid(zdo) == raid && RaiderTag.Host(zdo) == host;
    }
}
