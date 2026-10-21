// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Kit parts for Unity layout groups: a card (a laid-out rect whose kit face is inset by the panel overhang, so the
    /// background art fills exactly the card), a layout-safe divider, and a read-only floating card placed beside the
    /// pointer. Coordinates are parent-local with y upward; a floating card's pivot is its top-left corner.
    /// </summary>
    public static class KitCardLayout {
        /// <summary>Content padding inside a card, clear of the frame art.</summary>
        public const float Padding=8f;
        public const float DividerThickness=KitRowLayout.DividerThickness;
        public static readonly KitColor DividerColour=KitPalette.Divider;
        /// <summary>Gap between the pointer and a floating card.</summary>
        public const float FloatOffset=16f;
        public const float FloatWidth=320f;

        /// <summary>
        /// Offsets of a card's stretched face: inset by the overhang on every side, so a background reaching past the face
        /// by the overhang ends exactly on the card's edge. A negative or non-finite overhang insets nothing.
        /// </summary>
        public static (KitVector Min,KitVector Max) FaceInset(float overhang){
            float o=KitMapRules.Finite(overhang)&&overhang>0f?overhang:0f;
            return (new KitVector(o,o),new KitVector(-o,-o));
        }

        /// <summary>
        /// Top-left corner of a floating card of <paramref name="size"/> beside <paramref name="pointer"/>: right of and
        /// below it by <see cref="FloatOffset"/>, flipped left or above when it would leave <paramref name="bounds"/>, then
        /// clamped so the frame art (reaching <paramref name="overhang"/> past the card) stays inside. A card larger than
        /// the bounds keeps its top-left corner visible.
        /// </summary>
        public static KitVector FloatTopLeft(KitVector pointer,KitVector size,KitRect bounds,float overhang){
            float o=KitMapRules.Finite(overhang)&&overhang>0f?overhang:0f;
            float w=KitMapRules.Finite(size.X)?KitMath.Max(0f,size.X):0f,h=KitMapRules.Finite(size.Y)?KitMath.Max(0f,size.Y):0f;
            if(!KitMapRules.Finite(pointer))pointer=new KitVector(bounds.XMin,bounds.YMax);
            float x=pointer.X+FloatOffset,y=pointer.Y-FloatOffset;
            if(x+w+o>bounds.XMax)x=pointer.X-FloatOffset-w;
            if(y-h-o<bounds.YMin)y=pointer.Y+FloatOffset+h;
            x=KitMath.Max(bounds.XMin+o,KitMath.Min(x,bounds.XMax-o-w));
            y=KitMath.Min(bounds.YMax-o,KitMath.Max(y,bounds.YMin+o+h));
            return new KitVector(x,y);
        }
    }
}
