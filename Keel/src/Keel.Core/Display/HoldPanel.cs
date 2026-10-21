// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the container panel of a hold wider than the game's 8 columns (Cargo Columns up to 12, user
// decision 2026-10-08; native defect e5db2aac: 10 to 12 columns spilled out of the panel at 1280x720).
using System;

namespace ExtraPotions.Keel.Display
{
    public static class HoldPanel
    {
        /// <summary>Columns the game's container panel is drawn for.</summary>
        public const int GameColumns = 8;

        /// <summary>Space kept between a panel and the screen edge (canvas units).</summary>
        public const float ScreenMargin = 8f;

        /// <summary>The smallest scale a panel is drawn at to fit the screen.</summary>
        public const float MinScale = 0.25f;

        /// <summary>The panel's width for a hold: the game's width plus one grid step per column beyond 8; never narrower.</summary>
        public static float Width(float gameWidth, float step, int columns) =>
            Finite(gameWidth) + Math.Max(0, columns - GameColumns) * Math.Max(0f, Finite(step));

        /// <summary>
        /// Shift of a child anchored at the panel's center (the game's scrollbar) that keeps it at the right edge when the
        /// panel, pivoted on its left edge, grows from <paramref name="gameWidth"/> to <paramref name="width"/>.
        /// </summary>
        public static float CentreShift(float gameWidth, float width) => (Finite(width) - Finite(gameWidth)) / 2f;

        /// <summary>
        /// Keeps a panel on screen: a scale (at most 1) that makes it fit between the margins, and the horizontal shift of
        /// its left edge that brings the scaled panel inside them. Positions are in one space (the screen's canvas).
        /// </summary>
        public static (float Scale, float Shift) Place(float left, float width, float screenLeft, float screenRight, float margin = ScreenMargin)
        {
            if (!(width > 0f) || float.IsInfinity(width) || float.IsNaN(left) || float.IsNaN(screenLeft) || float.IsNaN(screenRight))
                return (1f, 0f);
            var room = screenRight - screenLeft - 2 * margin;
            var scale = room > 0f ? Math.Max(MinScale, Math.Min(1f, room / width)) : MinScale;
            var scaled = width * scale;
            var lowest = screenLeft + margin;
            var highest = Math.Max(lowest, screenRight - margin - scaled);
            var placed = Math.Min(highest, Math.Max(lowest, left));
            return (scale, placed - left);
        }

        /// <summary>The left and right edges of a grid of <paramref name="columns"/> cells centered on <paramref name="centre"/> (the game centers its grid).</summary>
        public static (float Left, float Right) GridSpan(float centre, float step, int columns)
        {
            var half = Math.Max(0, columns) * step / 2f;
            return (centre - half, centre + half);
        }

        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
