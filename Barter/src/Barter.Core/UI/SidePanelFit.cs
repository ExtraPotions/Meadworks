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

    /// <summary>
    /// Where a side panel sits next to the store. Coordinates are one space (screen pixels of the HUD canvas, after
    /// its scale). The panel keeps the left side when its frame stays inside the canvas, then the right of the store
    /// or of the sell list, then below. The box that comes back stays inside the canvas when the panel fits.
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
