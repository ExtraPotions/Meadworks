// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: maps a Join Game favorite move from visible rows to the full favorites list.
using System.Collections.Generic;

namespace ExtraPotions.Beacon.Servers
{
    /// <summary>
    /// The game's favorite move swaps rows by their place in the filtered list but applies it to the full favorites list,
    /// so with a filter typed it can swap the wrong pair. This maps the selected row and its visible neighbor to their
    /// places in the full list. Entries hidden by the filter keep their places.
    /// </summary>
    public static class FavoriteMove
    {
        /// <param name="visibleToFull">For each visible row, its index in the full favorites list, or -1 if it is not a favorite.</param>
        /// <param name="fullCount">Number of entries in the full favorites list.</param>
        /// <param name="selected">The selected visible row, or -1.</param>
        /// <param name="offset">-1 to move up, 1 to move down.</param>
        /// <returns>False when the move has no valid pair and must be skipped.</returns>
        public static bool Resolve(IReadOnlyList<int> visibleToFull, int fullCount, int selected, int offset, out int from, out int to)
        {
            from = -1; to = -1;
            if (visibleToFull == null || (offset != 1 && offset != -1)) return false;
            var neighbor = selected + offset;
            if (selected < 0 || selected >= visibleToFull.Count || neighbor < 0 || neighbor >= visibleToFull.Count) return false;
            from = visibleToFull[selected]; to = visibleToFull[neighbor];
            if (from < 0 || from >= fullCount || to < 0 || to >= fullCount || from == to) { from = -1; to = -1; return false; }
            return true;
        }
    }
}
