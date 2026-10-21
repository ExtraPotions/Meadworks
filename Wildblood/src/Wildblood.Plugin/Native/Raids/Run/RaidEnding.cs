// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The ends that can come at any moment of a raid (features/raids.md, "How it ends"), checked on the owner each tick
    /// before the clock moves on: abandoned (the server restarted, the last player at the raid logged out - or every
    /// player, from a dedicated server running the raid itself - or nobody has been within 96 m for 60 seconds) and timed
    /// out (15 minutes from the sounding). The others come from events: won
    /// when the last wave falls (<see cref="RaidPhases"/>), robbed when the Raiders Chest breaks and stopped when a player
    /// or an admin stops it (<see cref="Raid"/>). Abandonment is checked first: a raid nobody is at times out unseen.
    /// </summary>
    internal static class RaidEnding
    {
        public static RaidEnd Check(RaidState state, long now)
        {
            if (RaidPresence.ServerRestarted(state) || RaidPresence.RunnerLoggedOut(state) || RaidPresence.ServerEmpty()
                || now - state.SeenAt > RaidState.Ms(RaidTable.AbandonSeconds))
            {
                return RaidEnd.Abandoned;
            }
            return now >= state.Deadline ? RaidEnd.TimedOut : RaidEnd.None;
        }
    }
}
