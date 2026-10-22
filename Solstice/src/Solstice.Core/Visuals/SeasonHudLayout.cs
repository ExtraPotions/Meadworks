// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The season plate sits under other HUD rectangles and off the status row.
namespace ExtraPotions.Solstice.Visuals
{
    /// <summary>A rectangle in HUD-root units, origin at the root's top-left, y downward. The occupancy contract uses the same axes.</summary>
    public readonly struct HudBox
    {
        public HudBox(float left, float top, float width, float height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public float Left { get; }
        public float Top { get; }
        public float Width { get; }
        public float Height { get; }
        public float Right => Left + Width;
        public float Bottom => Top + Height;
        public bool Shown => Width > 0f && Height > 0f;
    }

    /// <summary>
    /// Where the season plate goes. <paramref name="occupiedBottom"/> is the lowest bottom edge of other products'
    /// HUD across this plate's width, or below 0 when nothing is there (the same number <c>meadworks.hud.occupied</c> yields).
    /// A status row that crosses the plate pushes it below that row.
    /// </summary>
    public static class SeasonHudLayout
    {
        public const float Gap = 6f;
        public const float TopGap = 14f;
        public const float ChipWidth = 188f;
        public const float ChipHeight = 24f;

        public static float Left(float parentWidth, float chipWidth)
        {
            if (parentWidth > 0f && chipWidth > parentWidth) chipWidth = parentWidth;
            return parentWidth > chipWidth ? (parentWidth - chipWidth) * 0.5f : 0f;
        }

        public static HudBox Place(float parentWidth, float chipWidth, float chipHeight, float occupiedBottom, HudBox status)
        {
            if (chipWidth < 1f) chipWidth = ChipWidth;
            if (chipHeight < 1f) chipHeight = ChipHeight;
            if (parentWidth > 0f && chipWidth > parentWidth) chipWidth = parentWidth;
            float left = Left(parentWidth, chipWidth);
            float top = TopGap;
            if (occupiedBottom >= 0f && top < occupiedBottom + Gap) top = occupiedBottom + Gap;
            if (Overlaps(left, top, chipWidth, chipHeight, status)) top = status.Bottom + Gap;
            return new HudBox(left, top, chipWidth, chipHeight);
        }

        public static bool Overlaps(float left, float top, float width, float height, HudBox other)
        {
            if (!other.Shown || width <= 0f || height <= 0f) return false;
            return left < other.Right && left + width > other.Left && top < other.Bottom && top + height > other.Top;
        }
    }
}
