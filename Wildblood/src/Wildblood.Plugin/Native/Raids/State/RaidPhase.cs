// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Where a raid is in its run, stored as an int in the host's ZDO (<see cref="RaidKeys.Phase"/>). The values are saved
    /// with the world, so they never change meaning: a new phase gets a new number.
    /// </summary>
    public enum RaidPhase
    {
        /// <summary>No raid has run here (a chest that has never been sounded).</summary>
        Idle = 0,

        /// <summary>Sounded: the 20 second countdown before the first wave.</summary>
        Countdown = 1,

        /// <summary>A wave is arriving or fighting.</summary>
        Wave = 2,

        /// <summary>The 20 seconds between a wave's last raider falling and the next wave.</summary>
        Break = 3,

        /// <summary>Over; <see cref="RaidKeys.Ended"/> says how.</summary>
        Ended = 4,
    }
}
