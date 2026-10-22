// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The meter shows the warm-clothes overheat building and easing. It does not change the regen penalty.
using System;

namespace ExtraPotions.Solstice.Visuals
{
    /// <summary>Fill for the heat meter. 1 is the same moment <c>ClimateRules.Overheating</c> is true. One warm piece sits at 0.4.</summary>
    public static class HeatMeter
    {
        public const float Partial = 0.4f;
        public const float FullSeconds = 8f;
        public const float ReliefSeconds = 4f;
        public const float LabelWide = 1280f;
        public const float LabelMinPixels = 8f;
        public const float LabelSizeAtWide = 14f;

        /// <summary>
        /// 0 draws the bar without the word. A window at least 1280 wide keeps 14.
        /// A narrower window grows the font until the word is 8 pixels, and drops the word when that would not fit the bar.
        /// </summary>
        public static float LabelFontSize(float barHeight, float canvasScale, float screenWidth, float hudWidth)
        {
            float width = screenWidth > 1f ? screenWidth : hudWidth;
            if (!float.IsNaN(width) && !float.IsInfinity(width) && width >= LabelWide) return LabelSizeAtWide;
            float scale = canvasScale;
            if (float.IsNaN(scale) || scale < 0.05f) scale = 1f;
            if (LabelSizeAtWide * scale >= LabelMinPixels) return LabelSizeAtWide;
            float needed = LabelMinPixels / scale;
            float cap = barHeight - 2f;
            if (cap >= 1f && needed >= 1f && needed <= cap) return needed;
            return 0f;
        }

        public static float Target(bool settingOn, bool seasonOverheats, bool shelteredOrCold, int warmClothes, bool coolingFood)
        {
            if (!settingOn || !seasonOverheats || shelteredOrCold || coolingFood || warmClothes <= 0) return 0f;
            return warmClothes == 1 ? Partial : 1f;
        }

        public static float Advance(float current, float target, float seconds)
        {
            if (float.IsNaN(current) || float.IsInfinity(current) || current < 0f) current = 0f;
            if (current > 1f) current = 1f;
            if (float.IsNaN(target) || float.IsInfinity(target) || target < 0f) target = 0f;
            if (target > 1f) target = 1f;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f || current == target) return current;
            float rate = target > current ? 1f / FullSeconds : 1f / ReliefSeconds;
            float step = seconds * rate;
            if (step > 1f) step = 1f;
            float next = target > current ? current + step : current - step;
            if (target > current) return next > target ? target : next;
            return next < target ? target : next;
        }
    }

    /// <summary>Where the heat meter sits: under the season plate, and off the status row, notices and hotbar.</summary>
    public static class HeatMeterLayout
    {
        public const float Width = 168f;
        public const float Height = 18f;

        public static HudBox Union(HudBox a, HudBox b)
        {
            if (!a.Shown) return b;
            if (!b.Shown) return a;
            float left = a.Left < b.Left ? a.Left : b.Left;
            float top = a.Top < b.Top ? a.Top : b.Top;
            float right = a.Right > b.Right ? a.Right : b.Right;
            float bottom = a.Bottom > b.Bottom ? a.Bottom : b.Bottom;
            return new HudBox(left, top, right - left, bottom - top);
        }

        public static HudBox Place(float parentWidth, float parentHeight, HudBox above, float occupiedBottom, HudBox status, HudBox notices, HudBox hotbar)
        {
            float width = Width;
            float height = Height;
            if (parentWidth > 0f && width > parentWidth) width = parentWidth;
            float left = SeasonHudLayout.Left(parentWidth, width);
            float top = SeasonHudLayout.TopGap;
            if (above.Shown && top < above.Bottom + SeasonHudLayout.Gap) top = above.Bottom + SeasonHudLayout.Gap;
            if (occupiedBottom >= 0f && top < occupiedBottom + SeasonHudLayout.Gap) top = occupiedBottom + SeasonHudLayout.Gap;
            if (Hits(left, top, width, height, status)) top = status.Bottom + SeasonHudLayout.Gap;
            Shift(ref left, top, width, height, notices, parentWidth);
            if (Hits(left, top, width, height, hotbar))
            {
                float raised = hotbar.Top - SeasonHudLayout.Gap - height;
                if (raised >= 0f && !Hits(left, raised, width, height, hotbar) && !Hits(left, raised, width, height, above) && !Hits(left, raised, width, height, status))
                    top = raised;
            }
            if (parentHeight > 0f && top + height > parentHeight) top = parentHeight - height;
            if (top < 0f) top = 0f;
            if (occupiedBottom >= 0f && top < occupiedBottom) return default;
            if (Hits(left, top, width, height, above) || Hits(left, top, width, height, status)
                || Hits(left, top, width, height, notices) || Hits(left, top, width, height, hotbar))
                return default;
            return new HudBox(left, top, width, height);
        }

        static void Shift(ref float left, float top, float width, float height, HudBox block, float parentWidth)
        {
            if (!Hits(left, top, width, height, block)) return;
            float toLeft = block.Left - SeasonHudLayout.Gap - width;
            float toRight = block.Right + SeasonHudLayout.Gap;
            bool leftFits = toLeft >= 0f && !Hits(toLeft, top, width, height, block);
            bool rightFits = (parentWidth <= 0f || toRight + width <= parentWidth) && !Hits(toRight, top, width, height, block);
            if (rightFits && (!leftFits || block.Left < left)) left = toRight;
            else if (leftFits) left = toLeft;
        }

        static bool Hits(float left, float top, float width, float height, HudBox other) =>
            SeasonHudLayout.Overlaps(left, top, width, height, other);
    }
}
