// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Barter
{
    /// <summary>An axis-aligned box. X and Y are the bottom-left corner and Y grows upward.</summary>
    public readonly struct PanelBox
    {
        public PanelBox(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float Right => X + Width;
        public float Top => Y + Height;

        public bool Usable => Finite(X) && Finite(Y) && Finite(Width) && Finite(Height) && Width > 0f && Height > 0f;

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum SidePanelSide
    {
        Left,
        Right,
        Below
    }

    public readonly struct SidePlace
    {
        public SidePlace(SidePanelSide side, PanelBox box)
        {
            Side = side;
            Box = box;
        }

        public SidePanelSide Side { get; }
        public PanelBox Box { get; }
    }

    /// <summary>The purse and the sell list after one placement. A flag is false when that panel is hidden.</summary>
    public readonly struct OpenPlace
    {
        public OpenPlace(bool hasSell, SidePlace sell, bool hasPurse, SidePlace purse)
        {
            HasSell = hasSell;
            Sell = sell;
            HasPurse = hasPurse;
            Purse = purse;
        }

        public bool HasSell { get; }
        public SidePlace Sell { get; }
        public bool HasPurse { get; }
        public SidePlace Purse { get; }
    }

    /// <summary>
    /// Where a side panel sits next to the store. Coordinates are one space (screen pixels of the HUD canvas, after
    /// its scale). <see cref="Place"/> is the purse: left of the store, then the right of the store or of the sell
    /// list, then below. <see cref="PlaceSell"/> is the sell list: right of the store, then left, then below.
    /// <see cref="PlaceBoth"/> keeps the two off each other. A box stays inside the canvas when the panel fits.
    /// </summary>
    public static class SidePanelFit
    {
        public static SidePlace Place(PanelBox canvas, PanelBox store, PanelBox? beside, float width, float height, float gap, float frame)
        {
            width = NonNegative(width);
            height = NonNegative(height);
            gap = NonNegative(gap);
            frame = NonNegative(frame);
            if (!canvas.Usable)
                return new SidePlace(SidePanelSide.Left, new PanelBox(0f, 0f, width, height));

            PanelBox anchor = store.Usable ? store : canvas;
            PanelBox left = NudgeY(canvas, Box(anchor.X - gap - width, anchor.Top - height, width, height), frame);
            if (Inside(canvas, left, frame) && !Hits(left, anchor) && !Hits(left, beside))
                return new SidePlace(SidePanelSide.Left, left);

            float rightEdge = anchor.Right;
            if (beside is PanelBox block && block.Usable)
                rightEdge = Math.Max(rightEdge, block.Right);
            PanelBox right = NudgeY(canvas, Box(rightEdge + gap, anchor.Top - height, width, height), frame);
            if (Inside(canvas, right, frame) && !Hits(right, anchor) && !Hits(right, beside))
                return new SidePlace(SidePanelSide.Right, right);

            float floor = anchor.Y;
            if (beside is PanelBox under && under.Usable)
                floor = Math.Min(floor, under.Y);
            PanelBox below = NudgeX(canvas, Box(anchor.X, floor - gap - height, width, height), frame);
            return new SidePlace(SidePanelSide.Below, Clamp(canvas, below, frame));
        }

        /// <summary>Right of the store, then left, then below. The purse is not an anchor; <see cref="PlaceBoth"/> keeps it clear.</summary>
        public static SidePlace PlaceSell(PanelBox canvas, PanelBox store, float width, float height, float gap, float frame)
        {
            width = NonNegative(width);
            height = NonNegative(height);
            gap = NonNegative(gap);
            frame = NonNegative(frame);
            if (!canvas.Usable)
                return new SidePlace(SidePanelSide.Right, new PanelBox(0f, 0f, width, height));

            PanelBox anchor = store.Usable ? store : canvas;
            PanelBox right = NudgeY(canvas, Box(anchor.Right + gap, anchor.Top - height, width, height), frame);
            if (Inside(canvas, right, frame) && !Hits(right, anchor))
                return new SidePlace(SidePanelSide.Right, right);

            PanelBox left = NudgeY(canvas, Box(anchor.X - gap - width, anchor.Top - height, width, height), frame);
            if (Inside(canvas, left, frame) && !Hits(left, anchor))
                return new SidePlace(SidePanelSide.Left, left);

            PanelBox below = NudgeX(canvas, Box(anchor.X, anchor.Y - gap - height, width, height), frame);
            return new SidePlace(SidePanelSide.Below, Clamp(canvas, below, frame));
        }

        /// <summary>
        /// Sell list first (right, else left, else below the store). The purse then uses <see cref="Place"/> and
        /// treats a sell list that stayed on a side as the panel it must not cover. A below sell list that would
        /// cover the purse moves to the on-screen spot that misses the purse and covers the least of the store.
        /// </summary>
        public static OpenPlace PlaceBoth(
            PanelBox canvas, PanelBox store,
            bool sell, float sellWidth, float sellHeight,
            bool purse, float purseWidth, float purseHeight,
            float gap, float frame)
        {
            gap = NonNegative(gap);
            frame = NonNegative(frame);
            sell = sell && sellWidth > 0f && sellHeight > 0f && PanelBox.Finite(sellWidth) && PanelBox.Finite(sellHeight);
            purse = purse && purseWidth > 0f && purseHeight > 0f && PanelBox.Finite(purseWidth) && PanelBox.Finite(purseHeight);

            SidePlace sellPlace = default;
            bool sellOnSide = false;
            if (sell)
            {
                sellPlace = PlaceSell(canvas, store, sellWidth, sellHeight, gap, frame);
                sellOnSide = sellPlace.Side != SidePanelSide.Below;
            }

            SidePlace pursePlace = default;
            if (purse)
            {
                PanelBox? beside = sell && sellOnSide ? sellPlace.Box : (PanelBox?)null;
                pursePlace = Place(canvas, store, beside, purseWidth, purseHeight, gap, frame);
            }

            if (sell && purse && Hits(sellPlace.Box, pursePlace.Box))
            {
                if (sellOnSide)
                    pursePlace = Yield(canvas, store, pursePlace, sellPlace.Box, gap, frame);
                else
                    sellPlace = Yield(canvas, store, sellPlace, pursePlace.Box, gap, frame);
            }
            if (sell && purse && Hits(sellPlace.Box, pursePlace.Box))
            {
                if (sellOnSide)
                    sellPlace = Yield(canvas, store, sellPlace, pursePlace.Box, gap, frame);
                else
                    pursePlace = Yield(canvas, store, pursePlace, sellPlace.Box, gap, frame);
            }
            return new OpenPlace(sell, sellPlace, purse, pursePlace);
        }

        static SidePlace Yield(PanelBox canvas, PanelBox store, SidePlace panel, PanelBox obstacle, float gap, float frame)
        {
            PanelBox moved = Separate(canvas, store, panel.Box, obstacle, gap, frame);
            if (!Hits(moved, obstacle))
                return new SidePlace(panel.Side, moved);
            return panel;
        }

        /// <summary>
        /// An on-screen box of the same size that misses <paramref name="obstacle"/>. Among those, the one that
        /// covers the least of the store, then the one closest to <paramref name="panel"/>.
        /// </summary>
        static PanelBox Separate(PanelBox canvas, PanelBox store, PanelBox panel, PanelBox obstacle, float gap, float frame)
        {
            if (!Hits(panel, obstacle) || !canvas.Usable || !obstacle.Usable)
                return panel;

            bool found = false;
            PanelBox best = panel;
            float bestArea = 0f;
            float bestDist = 0f;
            void Consider(float x, float y)
            {
                PanelBox candidate = Clamp(canvas, new PanelBox(x, y, panel.Width, panel.Height), frame);
                if (!Inside(canvas, candidate, frame) || Hits(candidate, obstacle))
                    return;
                float area = OverlapArea(candidate, store);
                float dx = candidate.X - panel.X;
                float dy = candidate.Y - panel.Y;
                float dist = dx * dx + dy * dy;
                if (!found || area < bestArea - 0.5f || (Math.Abs(area - bestArea) <= 0.5f && dist < bestDist))
                {
                    found = true;
                    best = candidate;
                    bestArea = area;
                    bestDist = dist;
                }
            }

            Consider(obstacle.X - gap - panel.Width, panel.Y);
            Consider(obstacle.Right + gap, panel.Y);
            Consider(panel.X, obstacle.Top + gap);
            Consider(panel.X, obstacle.Y - gap - panel.Height);
            Consider(canvas.X + frame, canvas.Y + frame);
            Consider(canvas.Right - frame - panel.Width, canvas.Y + frame);
            Consider(canvas.X + frame, canvas.Top - frame - panel.Height);
            Consider(canvas.Right - frame - panel.Width, canvas.Top - frame - panel.Height);
            if (store.Usable)
            {
                Consider(store.X - gap - panel.Width, store.Top - panel.Height);
                Consider(store.Right + gap, store.Top - panel.Height);
            }
            return found ? best : panel;
        }

        static float OverlapArea(PanelBox panel, PanelBox other)
        {
            if (!panel.Usable || !other.Usable)
                return 0f;
            float width = Math.Min(panel.Right, other.Right) - Math.Max(panel.X, other.X);
            float height = Math.Min(panel.Top, other.Top) - Math.Max(panel.Y, other.Y);
            if (width <= 0.5f || height <= 0.5f)
                return 0f;
            return width * height;
        }

        static PanelBox Box(float x, float y, float width, float height) => new PanelBox(x, y, width, height);

        static float NonNegative(float value) => PanelBox.Finite(value) && value > 0f ? value : 0f;

        static PanelBox NudgeY(PanelBox canvas, PanelBox panel, float frame)
        {
            float bottom = canvas.Y + frame;
            float top = canvas.Top - frame;
            if (top - bottom < panel.Height)
                return panel;
            float y = panel.Y;
            if (y < bottom)
                y = bottom;
            if (y + panel.Height > top)
                y = top - panel.Height;
            return new PanelBox(panel.X, y, panel.Width, panel.Height);
        }

        static PanelBox NudgeX(PanelBox canvas, PanelBox panel, float frame)
        {
            float left = canvas.X + frame;
            float right = canvas.Right - frame;
            if (right - left < panel.Width)
                return panel;
            float x = panel.X;
            if (x < left)
                x = left;
            if (x + panel.Width > right)
                x = right - panel.Width;
            return new PanelBox(x, panel.Y, panel.Width, panel.Height);
        }

        static bool Inside(PanelBox canvas, PanelBox panel, float frame) =>
            panel.X >= canvas.X + frame - 0.51f &&
            panel.Y >= canvas.Y + frame - 0.51f &&
            panel.Right <= canvas.Right - frame + 0.51f &&
            panel.Top <= canvas.Top - frame + 0.51f;

        static bool Hits(PanelBox panel, PanelBox? other)
        {
            if (!other.HasValue || !other.Value.Usable)
                return false;
            PanelBox box = other.Value;
            float left = Math.Max(panel.X, box.X);
            float right = Math.Min(panel.Right, box.Right);
            float bottom = Math.Max(panel.Y, box.Y);
            float top = Math.Min(panel.Top, box.Top);
            return right - left > 0.5f && top - bottom > 0.5f;
        }

        static bool Hits(PanelBox panel, PanelBox other) => Hits(panel, (PanelBox?)other);

        static PanelBox Clamp(PanelBox canvas, PanelBox panel, float frame)
        {
            float frameX = frame;
            float frameY = frame;
            if (canvas.Width <= panel.Width + frame * 2f)
                frameX = 0f;
            if (canvas.Height <= panel.Height + frame * 2f)
                frameY = 0f;
            float left = canvas.X + frameX;
            float bottom = canvas.Y + frameY;
            float right = canvas.Right - frameX;
            float top = canvas.Top - frameY;
            float x = panel.X;
            float y = panel.Y;
            if (right - left >= panel.Width)
                x = Math.Max(left, Math.Min(x, right - panel.Width));
            else
                x = left;
            if (top - bottom >= panel.Height)
                y = Math.Max(bottom, Math.Min(y, top - panel.Height));
            else
                y = top - panel.Height;
            return new PanelBox(x, y, panel.Width, panel.Height);
        }
    }
}
