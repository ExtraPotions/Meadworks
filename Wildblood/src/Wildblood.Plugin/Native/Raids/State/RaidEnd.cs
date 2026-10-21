// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// How a raid ended (features/raids.md, "How it ends"), stored as an int in the host's ZDO
    /// (<see cref="RaidKeys.Ended"/>). Saved with the world, so the numbers never change meaning.
    /// </summary>
    public enum RaidEnd
    {
        /// <summary>Not ended: a raid is running, or none ever ran.</summary>
        None = 0,

        /// <summary>The last wave is dead: every coin came back in the drops, with the extra.</summary>
        Won = 1,

        /// <summary>The Raiders Chest was broken: the raiders walk off and vanish with their coins.</summary>
        Robbed = 2,

        /// <summary>A player held E on the chest, or an admin typed `elite raid stop`: every raider dies, dropping nothing.</summary>
        Stopped = 3,

        /// <summary>No player near for a minute, the last one near logged out, or the server restarted: they walk off.</summary>
        Abandoned = 4,

        /// <summary>The 15 minutes ran out: the raiders walk off and vanish with their coins.</summary>
        TimedOut = 5,
    }
}
