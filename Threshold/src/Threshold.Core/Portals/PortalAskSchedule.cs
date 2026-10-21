// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: when a client asks the server for the portal list.
namespace ExtraPotions.Threshold.Portals
{
    /// <summary>
    /// Each ask is a Crucible operation and the authority keeps every request id for its session, so the client must not
    /// ask on a fixed short timer forever. It asks at once on session join and when the picker or map opens (or a page
    /// continues), every <see cref="OpenSeconds"/> while the picker is open (a new portal shows within about 5 s, answered
    /// by the cheap "unchanged" reply when nothing changed), and only every <see cref="IdleSeconds"/> otherwise, which keeps
    /// the walk-in route decision's list reasonably fresh.
    /// </summary>
    public sealed class PortalAskSchedule
    {
        public const double OpenSeconds = 5;
        public const double IdleSeconds = 60;

        double last = double.NegativeInfinity;
        bool now = true;

        /// <summary>Ask as soon as no ask is in flight (picker or map opened, edit answered, more pages).</summary>
        public void Request() => now = true;

        public bool Due(double time, bool open) => now || time >= last + (open ? OpenSeconds : IdleSeconds);

        public void Sent(double time) { now = false; last = time; }

        /// <summary>A new session: ask at once.</summary>
        public void Reset() { now = true; last = double.NegativeInfinity; }
    }
}
