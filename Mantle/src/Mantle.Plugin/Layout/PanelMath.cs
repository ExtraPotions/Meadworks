// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Mantle.Panels
{
    /// <summary>Unity-free placement sums for the inventory panels, linked into the unit tests.</summary>
    public static class PanelMath
    {
        /// <summary>
        /// The stat column's left edge right of the player panel's rect edge, so the gap from the grid (<paramref name="grid"/>
        /// inside the rect, then <paramref name="left"/> to the stats panel) equals the gap to the stats panel's right edge
        /// (<paramref name="left"/> + <paramref name="width"/>) for a column <paramref name="content"/> wide. Never left of the rect edge.
        /// </summary>
        public static float CentredColumn(float grid, float left, float width, float content) =>
            Math.Max(0f, (left + width - content - grid) / 2f);

        /// <summary>
        /// Where the requirement slots' even spread begins in a bar <paramref name="width"/> wide, for a station level star
        /// whose right edge is <paramref name="starRight"/> from the bar's left: <paramref name="gap"/> past it, at most half the bar.
        /// </summary>
        public static float SpreadStart(float starRight, float width, float gap) =>
            Math.Min(Math.Max(starRight + gap, 0f), Math.Max(0f, width * 0.5f));
    }
}
