// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Patches;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The raiders this machine has loaded, listed as their steering wakes and dropped as it goes, so a stop or a lost
    /// raid reaches them without a scene search, and the raiders walking off, keyed by their AI, for the one patch on
    /// every monster's AI step (<see cref="RaiderLeavePatch"/>). That patch's first test is <see cref="Leaving"/>: with
    /// no raider walking off here - nearly always - every monster pays one static read and nothing else.
    /// </summary>
    internal static class RaiderRoster
    {
        private static readonly List<RaiderSteering> Live = new List<RaiderSteering>();
        private static readonly Dictionary<BaseAI, RaiderSteering> Leavers = new Dictionary<BaseAI, RaiderSteering>();

        /// <summary>How many raiders this machine owns that are walking off now.</summary>
        public static int Leaving => Leavers.Count;

        public static void Add(RaiderSteering raider)
        {
            if (!Live.Contains(raider))
            {
                Live.Add(raider);
            }
        }

        // Swap-remove: the order means nothing. Called from OnDestroy, never while ForRaid walks the list.
        public static void Remove(RaiderSteering raider)
        {
            StopLeaving(raider);
            int at = Live.IndexOf(raider);
            if (at < 0)
            {
                return;
            }
            Live[at] = Live[Live.Count - 1];
            Live.RemoveAt(Live.Count - 1);
        }

        public static void StartLeaving(RaiderSteering raider) => Leavers[raider.Ai] = raider;

        public static void StopLeaving(RaiderSteering raider)
        {
            if (raider.Ai != null)
            {
                Leavers.Remove(raider.Ai);
            }
        }

        /// <summary>The raider walking off behind this AI; false for every other creature.</summary>
        public static bool TryLeaver(BaseAI ai, out RaiderSteering raider) => Leavers.TryGetValue(ai, out raider);

        /// <summary>
        /// Gives the order to every raider of that raid this machine holds and owns: <paramref name="kill"/> kills it where
        /// it stands (a stop), otherwise it walks off (a lost raid). A death or a vanishing only destroys the object at the
        /// end of the frame, so the list is never changed under this loop; one raider failing never spares the rest.
        /// </summary>
        public static void ForRaid(ZDOID host, long raid, bool kill)
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                RaiderSteering raider = Live[i];
                if (raider != null && raider.Host == host && raider.Raid == raid)
                {
                    SafeCall.Run("raider order", static (one, killIt) => one.Obey(killIt), raider, kill);
                }
            }
        }
    }
}
