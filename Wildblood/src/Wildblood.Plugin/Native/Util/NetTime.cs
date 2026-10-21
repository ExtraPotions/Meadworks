// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Util
{
    /// <summary>
    /// The one clock every machine agrees on. Valheim's <c>ZNet.GetTimeSeconds</c> is the server-authoritative world
    /// time, replicated to every client, so a cloud stamped with it expires on the same instant everywhere - which is
    /// what lets the owner and a remote client show and retire the same cloud without a second message. Stored as
    /// whole milliseconds (a long) so a long-running world never loses the sub-second precision a 6-second hazard needs.
    /// </summary>
    public static class NetTime
    {
        /// <summary>Milliseconds of shared world time; 0 before the network is up (no creature exists to read it yet).</summary>
        public static long NowMs()
        {
            return ZNet.instance != null ? (long)(ZNet.instance.GetTimeSeconds() * 1000.0) : 0L;
        }

        /// <summary>Seconds elapsed since a millisecond stamp, on the shared clock.</summary>
        public static float SecondsSince(long stampMs) => (NowMs() - stampMs) / 1000f;

        /// <summary>
        /// Seconds of this clock in one world day: the game's day length (1800, 30 real minutes, unless a mod changed it).
        /// The clock runs at real time and jumps when players sleep, so a day is never 24 hours of it.
        /// </summary>
        public static double DaySeconds() => EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800.0;
    }
}
