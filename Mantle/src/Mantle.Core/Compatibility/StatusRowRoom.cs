// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Mantle.Compatibility
{
    /// <summary>
    /// How many of the game's status effects fit on one row once Mantle has moved the row left for its stat column, so the
    /// row wraps before another product's published HUD rectangle (<c>meadworks.hud.occupied</c>, Cairn's compass plank among
    /// them). The row grows leftwards from its first cell. The rule and margin match Cairn's <c>HudLayout.StatusEffectsPerRow</c>,
    /// so with Cairn installed both pick the same count and neither rewrites the other's positions. All values are in the HUD
    /// root's units from its top-left corner, y down.
    /// </summary>
    public static class StatusRowRoom
    {
        /// <summary>Units between the row's last cell and the rectangle's right edge (Cairn's <c>StatusEffectMargin</c>).</summary>
        public const float Margin = 8f;

        /// <summary>
        /// Cells per row that keep the row clear of one rectangle. The game's count (<paramref name="nativePerRow"/>) applies when
        /// the rectangle does not share the first row's height band or lies right of the first cell; the result is never more
        /// than that count and never less than one.
        /// </summary>
        public static int PerRow(int nativePerRow, float firstCenter, float cell, float rowTop, float rowBottom,
            float left, float right, float top, float bottom)
        {
            int native = Math.Max(1, nativePerRow);
            if (cell <= 0 || bottom <= rowTop || top >= rowBottom || left >= firstCenter + cell * .5f) return native;
            float room = firstCenter - cell * .5f - (right + Margin);
            if (room < 0) return 1;
            return Math.Max(1, Math.Min(native, 1 + (int)Math.Floor(room / cell)));
        }

        /// <summary>
        /// Whether two screen rectangles touch or overlap (left/right/bottom/top, y up). Used while the inventory is open: a
        /// status row that would reach into an open window is hidden instead of moved.
        /// </summary>
        public static bool Overlaps(float left, float right, float bottom, float top,
            float otherLeft, float otherRight, float otherBottom, float otherTop) =>
            left <= otherRight && otherLeft <= right && bottom <= otherTop && otherBottom <= top;
    }

    /// <summary>
    /// Keeps the status row's layout while a window is open (the inventory and crafting window, the Esc menu, the large map or
    /// a MeadWorks window that blocks the HUD). Opening a window hides the column and the compass plank, and without a hold the
    /// row would move and rewrap until it closed. The hold ends <see cref="ReleaseFrames"/> frames after the last open frame,
    /// so a HUD that reappears a frame late (another product's Update order) does not make the row jump on close.
    /// </summary>
    public sealed class StatusRowHold
    {
        /// <summary>Frames after the window closes during which the held layout stays.</summary>
        public const int ReleaseFrames = 2;

        private bool holding;
        private int lastOpenFrame;

        /// <summary>True while the row's layout is held. Called with the frame number once or more per frame.</summary>
        public bool Holding(bool windowOpen, int frame)
        {
            if (windowOpen)
            {
                holding = true;
                lastOpenFrame = frame;
                return true;
            }
            if (holding && frame - lastOpenFrame <= ReleaseFrames && frame >= lastOpenFrame) return true;
            holding = false;
            return false;
        }

        public void Reset() => holding = false;
    }
}
