// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (BC03b): starting a join from the game's main menu the way the game starts an invited join.
using System.Collections.Generic;
using ExtraPotions.Beacon.Servers;

namespace ExtraPotions.Beacon.Runtime
{
    /// <summary>
    /// The game's main menu as Beacon sees it. A Beacon join queues the server on the game's start screen and starts the
    /// chosen character exactly as the game does for an invite, so the game runs its own checks (privileges, version,
    /// login) and its own join. Main thread only.
    /// </summary>
    internal static class BeaconMenu
    {
        /// <summary>The main menu is up and idle: no intro, no queued join or invite, and at least one character.</summary>
        public static bool Ready
        {
            get
            {
                var startup = FejdStartup.instance;
                if (startup == null || Game.instance != null) return false;
                if (startup.m_mainMenu?.activeInHierarchy != true && startup.m_serverListPanel?.activeInHierarchy != true) return false;
                if (CinematicsManager.IsStartedPlaying()) return false;
                if (startup.m_queuedJoinServer.IsValid || MatchmakingManager.HasPendingInvite()) return false;
                return startup.m_profiles != null && startup.m_profiles.Count > 0;
            }
        }

        /// <summary>Character names and file names in the start screen's order, and the selected index.</summary>
        public static (IReadOnlyList<string> Names, IReadOnlyList<string> Files, int Selected) Characters()
        {
            var names = new List<string>();
            var files = new List<string>();
            var startup = FejdStartup.instance;
            if (startup?.m_profiles == null) return (names, files, -1);
            foreach (var profile in startup.m_profiles)
            {
                names.Add(profile.GetName() ?? "");
                files.Add(profile.GetFilename() ?? "");
            }
            return (names, files, startup.m_profileIndex);
        }

        /// <summary>Joins <paramref name="address"/> with the character at <paramref name="character"/> (null: the selected one).</summary>
        public static bool Join(ServerAddress address, int? character)
        {
            if (!Ready || !BeaconJoin.TryJoinData(address, out var data)) return false;
            var startup = FejdStartup.instance;
            var index = character ?? startup.m_profileIndex;
            if (index < 0 || index >= startup.m_profiles.Count) return false;
            if (startup.m_connectionFailedPanel != null) startup.m_connectionFailedPanel.SetActive(false);
            startup.m_profileIndex = index;
            startup.m_queuedJoinServer = data;
            startup.OnCharacterStart();
            return true;
        }
    }
}
