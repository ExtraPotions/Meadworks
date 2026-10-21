// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Which raid comes: one of the game's own events (<c>RandEventSystem.m_events</c>) that the world has unlocked, drawn
    /// by <see cref="RaidPick"/> when a raid is sounded. The event name is kept in the host's ZDO so every
    /// wave, on whichever machine runs it, brings that event's spawn list; the display name is what players read.
    /// </summary>
    public sealed class RaidChoice
    {
        public RaidChoice(string eventName, string displayName)
        {
            EventName = eventName;
            DisplayName = displayName;
        }

        /// <summary>The game event's <c>m_name</c>, "army_goblin".</summary>
        public string EventName { get; }

        /// <summary>What players read in the message and on the HUD line, "Fuling raid".</summary>
        public string DisplayName { get; }
    }
}
