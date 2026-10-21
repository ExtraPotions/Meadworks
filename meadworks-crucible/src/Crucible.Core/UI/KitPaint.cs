// Adapted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/NightShade.cs, RoundedFill.cs and CrossMark.cs at
// 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: the light curve and the code-painted sprites' pixel rules
// restated without Unity (inputs are plain floats, outputs alpha bytes) so they are testable outside the game.
using System;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Kit panels darken with the light around the player, as the game's own wooden panels do (their <c>litpanel</c>
    /// material reads <c>_SunColor</c> and <c>_AmbientColor</c>): one grey brightness, a clear Meadows noon at full
    /// brightness and a clear midnight at the Night Shade setting; dark weather lands in between or below, never under
    /// the setting's floor. Faded at <see cref="Fade"/> per second; changes under <see cref="Epsilon"/> are ignored.
    /// </summary>
    public static class NightShadeCurve {
        public const float Default=0.65f;
        public const float Min=0.3f;
        public const float Max=1f;
        /// <summary>How much the sun counts beside the ambient light.</summary>
        public const float SunWeight=0.25f;
        /// <summary>The light of a clear Meadows midnight (ambient 0.37, sun 0.20, read in game 2026-10-02).</summary>
        public const float NightLight=0.42f;
        /// <summary>The light of a clear Meadows noon (ambient #7592B4, sun #FFC47B x 1.7, the game's weather data).</summary>
        public const float DayLight=0.90f;
        /// <summary>The most the shade changes per second, so a weather change fades in.</summary>
        public const float Fade=0.5f;
        public const float Epsilon=0.004f;

        public static float Luminance(float r,float g,float b)=>0.2126f*r+0.7152f*g+0.0722f*b;

        /// <summary>1 by day, the setting at a clear midnight; 1 with the kit look off and outside a world.</summary>
        public static float Target(float night,bool lookOn,bool inWorld,float ambientR,float ambientG,float ambientB,float sunR,float sunG,float sunB){
            if(night>=1f||!lookOn||!inWorld)return 1f;
            float light=Luminance(ambientR,ambientG,ambientB)+SunWeight*Luminance(sunR,sunG,sunB);
            return KitMath.Lerp(night,1f,KitMath.Clamp01((light-NightLight)/(DayLight-NightLight)));
        }

        public static bool Moves(float current,float target)=>!(KitMath.Abs(target-current)<Epsilon);

        public static float Step(float current,float target,float unscaledDeltaTime)=>KitMath.MoveTowards(current,target,Fade*unscaledDeltaTime);
    }

    /// <summary>
    /// Pixel rules of the code-painted kit sprites: the white rounded 9-slice fill (stat sheet inset) and the white X with
    /// round ends (blocked cells). Each pixel's alpha is its coverage, anti-aliased over one pixel; straight edges fall
    /// on pixel boundaries and stay crisp. Rows run bottom to top as Unity's <c>SetPixels32</c> expects.
    /// </summary>
    public static class KitPaint {
        /// <summary>The rounded fill's corner radius in UI units, whatever the size.</summary>
        public const float RoundedRadius=6f;
        /// <summary>The corner's radius in texture pixels: fine enough to stay smooth on a 4K screen.</summary>
        public const int RoundedCorner=16;
        public const int RoundedSize=RoundedCorner*2+2;
        public const int CrossSize=64;
        public const float CrossMargin=14f;
        public const float CrossHalfWidth=2.5f;

        public static float RoundedCoverage(float x,float y){
            float dx=KitMath.Max(KitMath.Max(RoundedCorner-x,x-(RoundedSize-RoundedCorner)),0f);
            float dy=KitMath.Max(KitMath.Max(RoundedCorner-y,y-(RoundedSize-RoundedCorner)),0f);
            return KitMath.Clamp01(RoundedCorner-KitMath.Sqrt(dx*dx+dy*dy)+0.5f);
        }

        public static byte[] RoundedAlpha(){
            var alpha=new byte[RoundedSize*RoundedSize];
            for(int y=0;y<RoundedSize;y++)for(int x=0;x<RoundedSize;x++)alpha[y*RoundedSize+x]=(byte)(255f*RoundedCoverage(x+0.5f,y+0.5f));
            return alpha;
        }

        public static float CrossCoverage(float x,float y){
            float low=CrossMargin,high=CrossSize-CrossMargin;
            var p=new KitVector(x,y);
            float d=KitMath.Min(Distance(p,new KitVector(low,low),new KitVector(high,high)),Distance(p,new KitVector(low,high),new KitVector(high,low)));
            return KitMath.Clamp01(CrossHalfWidth-d+0.5f);
        }

        public static byte[] CrossAlpha(){
            var alpha=new byte[CrossSize*CrossSize];
            for(int y=0;y<CrossSize;y++)for(int x=0;x<CrossSize;x++)alpha[y*CrossSize+x]=(byte)(255f*CrossCoverage(x+0.5f,y+0.5f));
            return alpha;
        }

        /// <summary>From a point to the segment a-b.</summary>
        static float Distance(KitVector p,KitVector a,KitVector b){
            var ab=b-a;
            float t=KitMath.Clamp01(KitVector.Dot(p-a,ab)/ab.SqrMagnitude);
            return KitVector.Distance(p,a+ab*t);
        }
    }
}
