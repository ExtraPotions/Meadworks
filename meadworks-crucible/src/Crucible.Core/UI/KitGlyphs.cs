using System;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// The kit's code-painted glyphs (KX02): view icons (bars, compact bars, small, medium and large grid squares), sort
    /// icons (D-UI-1: lines shortening for A-Z, lengthening for Z-A, a hollow grid for Type, a weight for Weight, plain
    /// bars for Default), the fallback favorite star, cross, minus and plus for tool buttons, and the weather badges
    /// (Tempest): a six-spoked snowflake, a lightning bolt and a cloud. New glyphs are appended so stored values keep their meaning.
    /// </summary>
    public enum KitGlyph { Bars, CompactBars, SmallGrid, MediumGrid, LargeGrid, SortAscending, SortDescending, SortType, SortWeight, SortDefault, Star, Cross, Minus, Plus, Snowflake, Bolt, Moon, Pillow, Bed, Chevron, Lock, Cloud }

    /// <summary>
    /// Pixel rules of the kit glyphs, white shapes colored by the Image that shows them (the button's text color).
    /// Painted at <see cref="Size"/> pixels so they stay sharp on large screens; each pixel's alpha is its coverage over
    /// 4 x 4 samples, so straight edges on pixel boundaries stay crisp and slanted ones are anti-aliased. Rows run bottom
    /// to top as Unity's <c>SetPixels32</c> expects. Crucible-authored (the shapes follow OpenKeep 3.0.0's view icons,
    /// doubled from 16 to 32 pixels; no code adopted).
    /// </summary>
    public static class KitGlyphs {
        public const int Size=32;
        /// <summary>Clear pixels between bars and squares.</summary>
        public const int Gap=4;
        const int Samples=4;

        /// <summary>The glyph's coverage (0..1) of the pixel whose lower-left corner is (<paramref name="px"/>, <paramref name="py"/>).</summary>
        public static float Coverage(KitGlyph glyph,float px,float py){
            if(px<0f||py<0f||px>=Size||py>=Size)return 0f;
            int inside=0;
            for(int j=0;j<Samples;j++)for(int i=0;i<Samples;i++)
                if(Inside(glyph,px+(i+0.5f)/Samples,py+(j+0.5f)/Samples))inside++;
            return inside/(float)(Samples*Samples);
        }

        public static byte[] Alpha(KitGlyph glyph){
            var alpha=new byte[Size*Size];
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)alpha[y*Size+x]=(byte)(255f*Coverage(glyph,x,y));
            return alpha;
        }

        public static string SpriteName(KitGlyph glyph)=>KitArtCatalog.SpritePrefix+"glyph_"+Name(glyph);

        public static string Name(KitGlyph glyph){
            switch(glyph){
                case KitGlyph.CompactBars:return "compact_bars";
                case KitGlyph.SmallGrid:return "small_grid";
                case KitGlyph.MediumGrid:return "medium_grid";
                case KitGlyph.LargeGrid:return "large_grid";
                case KitGlyph.SortAscending:return "sort_ascending";
                case KitGlyph.SortDescending:return "sort_descending";
                case KitGlyph.SortType:return "sort_type";
                case KitGlyph.SortWeight:return "sort_weight";
                case KitGlyph.SortDefault:return "sort_default";
                default:return glyph.ToString().ToLowerInvariant();
            }
        }

        /// <summary>The view button's glyph for a tile list layout.</summary>
        public static KitGlyph View(KitTileView view){
            switch(view){
                case KitTileView.CompactList:return KitGlyph.CompactBars;
                case KitTileView.SmallGrid:return KitGlyph.SmallGrid;
                case KitTileView.MediumGrid:return KitGlyph.MediumGrid;
                case KitTileView.LargeGrid:return KitGlyph.LargeGrid;
                default:return KitGlyph.Bars;
            }
        }

        /// <summary>Whether a point (pixel units, y up) lies in the glyph.</summary>
        static bool Inside(KitGlyph glyph,float x,float y){
            int ix=(int)x,iy=(int)y,top=Size-1-iy;
            switch(glyph){
                case KitGlyph.Bars:return Stripe(iy,3);
                case KitGlyph.CompactBars:return Stripe(iy,4);
                case KitGlyph.SmallGrid:return Stripe(ix,3)&&Stripe(iy,3);
                case KitGlyph.MediumGrid:return Stripe(ix,2)&&Stripe(iy,2);
                case KitGlyph.LargeGrid:return ix>=2&&iy>=2&&ix<Size-2&&iy<Size-2;
                case KitGlyph.SortAscending:return Stripe(top,4)&&ix<Size-StripeIndex(top,4)*8;
                case KitGlyph.SortDescending:return Stripe(top,4)&&ix<8+StripeIndex(top,4)*8;
                case KitGlyph.SortDefault:return Stripe(top,4)&&ix>=2&&ix<Size-8;
                case KitGlyph.SortType:return Stripe(ix,2)&&Stripe(iy,2)&&!(Local(ix,2)>=3&&Local(ix,2)<Bar(2)-3&&Local(iy,2)>=3&&Local(iy,2)<Bar(2)-3);
                case KitGlyph.SortWeight:return Weight(x,y);
                case KitGlyph.Star:return Star(x,y);
                case KitGlyph.Cross:return Segment(x,y,5f,5f,27f,27f)<=3f||Segment(x,y,5f,27f,27f,5f)<=3f;
                case KitGlyph.Minus:return ix>=4&&ix<28&&iy>=13&&iy<19;
                case KitGlyph.Plus:return ix>=4&&ix<28&&iy>=13&&iy<19||ix>=13&&ix<19&&iy>=4&&iy<28;
                case KitGlyph.Snowflake:return Snowflake(x,y);
                case KitGlyph.Bolt:return Polygon(x,y,BoltX,BoltY);
                case KitGlyph.Moon:return Distance(x,y,16,16)<=14 && Distance(x,y,23,21)>12;
                case KitGlyph.Cloud:return Cloud(x,y);
                case KitGlyph.Pillow:return x>=3&&x<=29&&y>=7&&y<=25 &&
                    !(x>6&&x<26&&y>10&&y<22);
                case KitGlyph.Bed:return x>=3&&x<=6&&y>=3&&y<=25 || x>=26&&x<=29&&y>=3&&y<=18 ||
                    x>=6&&x<=26&&y>=8&&y<=18 || x>=8&&x<=15&&y>=20&&y<=25;
                case KitGlyph.Chevron:return Segment(x,y,9,4,23,16)<=2.5f || Segment(x,y,23,16,9,28)<=2.5f;
                case KitGlyph.Lock:return x>=6&&x<=26&&y>=3&&y<=18&&!(x>=14&&x<=18&&y>=8&&y<=14) ||
                    y>=18&&Distance(x,y,16,22)<=9&&Distance(x,y,16,22)>=6;
                default:return false;
            }
        }

        static int Pitch(int stripes)=>(Size+Gap)/stripes;
        static int Bar(int stripes)=>Pitch(stripes)-Gap;
        static int StripeIndex(int at,int stripes)=>at/Pitch(stripes);
        static int Local(int at,int stripes)=>at%Pitch(stripes);
        /// <summary>Inside one of n stripes across the glyph with <see cref="Gap"/> clear pixels between them.</summary>
        static bool Stripe(int at,int stripes)=>Local(at,stripes)<Bar(stripes);

        /// <summary>A weight: a trapezoid body under a ring handle.</summary>
        static bool Weight(float x,float y){
            if(y>=2f&&y<=20f){
                float half=14f+(9f-14f)*(y-2f)/18f;
                if(Math.Abs(x-16f)<=half)return true;
            }
            float d=Distance(x,y,16f,25f);
            return d<=6.5f&&d>=3.5f;
        }

        /// <summary>A five-pointed star, one point up, outer radius 15, inner radius 0.382 of it.</summary>
        static bool Star(float x,float y){
            const float outer=15f,inner=outer*0.382f,cx=16f,cy=16f;
            bool inside=false;
            float px=0f,py=0f,fx=0f,fy=0f;
            for(int k=0;k<10;k++){
                float r=k%2==0?outer:inner;
                float angle=KitMath.PI/2f+k*KitMath.PI/5f;
                float vx=cx+r*KitMath.Cos(angle),vy=cy+r*KitMath.Sin(angle);
                if(k==0){fx=vx;fy=vy;}
                else if((py>y)!=(vy>y)&&x<(vx-px)*(y-py)/(vy-py)+px)inside=!inside;
                px=vx;py=vy;
            }
            if((py>y)!=(fy>y)&&x<(fx-px)*(y-py)/(fy-py)+px)inside=!inside;
            return inside;
        }

        /// <summary>
        /// A snowflake: six spokes of radius 14 from the center, one pointing up, each with two short arms branching
        /// outwards at 60 degrees from a point 8.5 along the spoke.
        /// </summary>
        static bool Snowflake(float x,float y){
            const float cx=16f,cy=16f,spoke=14f,fork=8.5f,arm=4.5f,spokeHalf=1.5f,armHalf=1.25f,branch=KitMath.PI/3f;
            for(int k=0;k<6;k++){
                float angle=KitMath.PI/2f+k*KitMath.PI/3f;
                float ux=KitMath.Cos(angle),uy=KitMath.Sin(angle);
                if(Segment(x,y,cx,cy,cx+spoke*ux,cy+spoke*uy)<=spokeHalf)return true;
                float fx=cx+fork*ux,fy=cy+fork*uy;
                for(int side=-1;side<=1;side+=2){
                    float a=angle+side*branch;
                    if(Segment(x,y,fx,fy,fx+arm*KitMath.Cos(a),fy+arm*KitMath.Sin(a))<=armHalf)return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A cloud: a flat-bottomed base from x 4 to 28 with rounded ends (y 6 to 14), a small puff on the left and a larger
        /// one right of center rising to y 26.
        /// </summary>
        static bool Cloud(float x,float y){
            if(x>=8f&&x<=24f&&y>=6f&&y<=14f)return true;
            if(Distance(x,y,8f,10f)<=4f||Distance(x,y,24f,10f)<=4f)return true;
            return Distance(x,y,12f,14f)<=6f||Distance(x,y,19f,17f)<=8.5f;
        }

        /// <summary>A lightning bolt (y up): a flat head top center, a notch each side, and the tip at the bottom left.</summary>
        static readonly float[] BoltX={13f,20f,17f,25f,12f,15f,7f};
        static readonly float[] BoltY={30f,30f,19f,19f,2f,13f,13f};

        /// <summary>Whether a point lies in the closed polygon with these vertices (even-odd rule).</summary>
        static bool Polygon(float x,float y,float[] xs,float[] ys){
            bool inside=false;
            for(int i=0,j=xs.Length-1;i<xs.Length;j=i++){
                if((ys[i]>y)!=(ys[j]>y)&&x<(xs[j]-xs[i])*(y-ys[i])/(ys[j]-ys[i])+xs[i])inside=!inside;
            }
            return inside;
        }

        static float Distance(float x,float y,float cx,float cy){float dx=x-cx,dy=y-cy;return KitMath.Sqrt(dx*dx+dy*dy);}

        /// <summary>From a point to the segment a-b.</summary>
        static float Segment(float x,float y,float ax,float ay,float bx,float by){
            float abx=bx-ax,aby=by-ay;
            float t=KitMath.Clamp01(((x-ax)*abx+(y-ay)*aby)/(abx*abx+aby*aby));
            return Distance(x,y,ax+abx*t,ay+aby*t);
        }
    }

    /// <summary>Square icon buttons (KX02): the glyph inset 5 from the button's shorter side, centered.</summary>
    public static class KitIconLayout {
        public const float Inset=5f;

        /// <summary>The glyph's rect inside a button of <paramref name="button"/> size (origin at the button's lower left).</summary>
        public static KitRect Glyph(KitVector button){
            float side=KitMath.Max(0f,KitMath.Min(button.X,button.Y)-Inset*2f);
            return new KitRect((button.X-side)/2f,(button.Y-side)/2f,side,side);
        }
    }

    /// <summary>Which click a kit button got: plain, with Shift, or with Ctrl (Ctrl wins when both are held).</summary>
    public enum KitClickKind { Plain, Shift, Ctrl }

    public static class KitClicks {
        const float WheelEpsilon=0.00001f;

        /// <summary>Modifiers read through the C07 input service (ZInput, released on focus loss).</summary>
        public static KitClickKind Of(bool shift,bool ctrl)=>ctrl?KitClickKind.Ctrl:shift?KitClickKind.Shift:KitClickKind.Plain;

        /// <summary>The wheel's step: up +1, down -1, none 0.</summary>
        public static int Wheel(float delta)=>delta>WheelEpsilon?1:delta<-WheelEpsilon?-1:0;

        /// <summary>Click the next mode, Shift + click the previous one (view and sort buttons), wrapping around.</summary>
        public static int Cycle(int index,int count,bool backward){
            if(count<=0)throw new ArgumentOutOfRangeException(nameof(count));
            int current=((index%count)+count)%count;
            return backward?(current+count-1)%count:(current+1)%count;
        }

        /// <summary>An on/off icon button (favorites only; Almanac's toggle AX05): lit while on and usable, else dimmed.</summary>
        public static KitTabVisual Toggle(bool on,bool usable)=>KitControls.Tab(on,usable);
    }
}
