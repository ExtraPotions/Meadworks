// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// What a raider makes of the raid it was spawned for, read on its owner from the host's ZDO (features/raids.md
    /// section 5). <see cref="RaiderTag.RaidRuns"/> answers yes or no; a raider needs the four cases apart: a running raid
    /// it fights in, a stopped one it dies for, a lost or ended one it walks off from, and a host this machine has no ZDO
    /// for at all, which may simply lie beyond the area the server sends it.
    /// </summary>
    internal enum RaidVerdict
    {
        /// <summary>Its raid is running: it fights and goes for the base.</summary>
        Runs,

        /// <summary>Its raid was stopped: it dies where it stands, dropping nothing.</summary>
        Stopped,

        /// <summary>Its raid ended any other way, or another raid of the host has begun since: it walks off.</summary>
        Over,

        /// <summary>This machine holds no ZDO for the host: gone, or never sent here.</summary>
        Unknown,
    }

    /// <summary>Reads a <see cref="RaidVerdict"/> from the host's ZDO. Cheap: one lookup and a few ints.</summary>
    internal static class RaidVerdicts
    {
        public static RaidVerdict Read(ZDOID host, long raid)
        {
            ZDO? zdo = host.IsNone() || ZDOMan.instance == null ? null : ZDOMan.instance.GetZDO(host);
            if (zdo == null)
            {
                return RaidVerdict.Unknown;
            }
            RaidState state = new RaidState(zdo);
            if (state.StartedAt != raid)
            {
                return RaidVerdict.Over; // the host has raided again since, or never ran this raid
            }
            if (state.Running)
            {
                return RaidVerdict.Runs;
            }
            return state.Ended == RaidEnd.Stopped ? RaidVerdict.Stopped : RaidVerdict.Over;
        }
    }
}
