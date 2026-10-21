// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Patches;
using ExtraPotions.Wildblood.Adopted.Util;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The game's own raids keep away from a chest raid (features/raids.md section 2, "One at a time"): no game event
    /// starts within 200 m of a running raid of this mod. The server picks an event's place from the players standing
    /// where it may happen (<c>RandEventSystem.GetValidEventPoints</c>, for the timed roll, the standalone events and the
    /// `randomevent` command alike), so the places near a chest raid are struck from that list: the game then draws among
    /// the rest, or starts nothing this time, exactly as when no player qualifies. Everything else about base raids stays
    /// the game's. An admin's `event` command names its own place and is left alone. The list is asked for once per event
    /// on the same frame with the same players, so each place is looked up once a frame. The game rolls an event every
    /// few in-game minutes, so the lookup (the hosts' ZDOs around a point, <see cref="RaidSpacing.ChestRaidNear"/>) is
    /// never on a hot path. A failure is reported and swallowed, leaving the game's list as it was.
    /// </summary>
    [HarmonyPatch(typeof(RandEventSystem), "GetValidEventPoints")]
    internal static class GameRaidSpacingPatch
    {
        /// <summary>The places looked up this frame and whether a chest raid runs near each: keyed by the place and
        /// <see cref="_frame"/>, emptied when the frame changes.</summary>
        private static readonly List<KeyValuePair<Vector3, bool>> Looked = new List<KeyValuePair<Vector3, bool>>();

        private static int _frame = -1;

        private static void Postfix(List<Vector3> __result)
        {
            if (__result != null && __result.Count > 0)
            {
                SafeCall.Run("RandEventSystem.GetValidEventPoints raid spacing", static points => Thin(points), __result);
            }
        }

        private static void Thin(List<Vector3> points)
        {
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                Looked.Clear();
            }
            for (int i = points.Count - 1; i >= 0; i--)
            {
                if (ChestRaidNear(points[i]))
                {
                    if (Log.Diagnostics)
                    {
                        Log.Diag($"game raid held back at {points[i]:F0}: a chest raid runs within {RaidTable.RaidSpacing} m");
                    }
                    points.RemoveAt(i);
                }
            }
        }

        private static bool ChestRaidNear(Vector3 at)
        {
            for (int i = 0; i < Looked.Count; i++)
            {
                if (Looked[i].Key == at)
                {
                    return Looked[i].Value;
                }
            }
            bool near = RaidSpacing.ChestRaidNear(at, ZDOID.None);
            Looked.Add(new KeyValuePair<Vector3, bool>(at, near));
            return near;
        }
    }
}
