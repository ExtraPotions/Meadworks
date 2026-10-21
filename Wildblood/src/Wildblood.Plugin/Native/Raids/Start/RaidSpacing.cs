// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// One raid at a time in one place (features/raids.md section 2): no raid is sounded within 200 m of another, the
    /// game's or a chest's. The game's current event is known on every machine (the server sends it to everyone every two
    /// seconds); other hosts are found by their ZDOs (<see cref="RaidZdos"/>), so a host this machine has not loaded still
    /// counts when the server has sent its ZDO. The same test serves the other way round, on the server, to keep the
    /// game's own raids from starting next to a chest raid (<see cref="ChestRaidNear"/>, <see cref="GameRaidSpacingPatch"/>).
    /// </summary>
    public static class RaidSpacing
    {
        /// <summary>Why no raid may start at <paramref name="at"/> now; null when it may. <paramref name="self"/> is the
        /// host asking, which never blocks itself.</summary>
        public static string? Refusal(Vector3 at, ZDOID self)
        {
            if (GameRaidNear(at))
            {
                return "Another raid is already on nearby.";
            }
            return ChestRaidNear(at, self) ? "Another raid is already on within 200 m." : null;
        }

        /// <summary>True when the game's own raid is on within the spacing of <paramref name="at"/>.</summary>
        public static bool GameRaidNear(Vector3 at)
        {
            RandomEvent? ev = RandEventSystem.instance != null ? RandEventSystem.instance.GetCurrentRandomEvent() : null;
            return ev != null && Utils.DistanceXZ(ev.m_pos, at) < RaidTable.RaidSpacing;
        }

        /// <summary>True when a raid of this mod runs within the spacing of <paramref name="at"/>, other than at
        /// <paramref name="self"/> (pass <c>ZDOID.None</c> to count every host).</summary>
        public static bool ChestRaidNear(Vector3 at, ZDOID self) => RaidZdos.RunningNear(at, RaidTable.RaidSpacing, self) != null;
    }
}
