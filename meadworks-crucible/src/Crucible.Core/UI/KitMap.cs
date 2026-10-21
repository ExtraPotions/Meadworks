// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>KX09 map view look and feel: PackPanel inset frame, kit label text, value-colored selection ring.</summary>
    public static class KitMapLayout {
        public const float MinZoom=0.25f;
        public const float MaxZoom=8f;
        /// <summary>Zoom multiplier for one mouse wheel step.</summary>
        public const float WheelFactor=1.25f;
        /// <summary>Pad trigger zoom speed in doublings per second at full trigger.</summary>
        public const float PadZoomSpeed=1f;
        /// <summary>Pad cursor speed in view pixels per second at full stick.</summary>
        public const float PadCursorSpeed=360f;
        public const float PadDeadZone=0.2f;
        /// <summary>The pad cursor stays this far inside the view; pushing past it pans the map.</summary>
        public const float EdgeMargin=24f;
        public const float PinSize=20f;
        public const float HitRadius=14f;
        public const float PadHitRadius=28f;
        public const float LabelSize=13f;
        public const float LabelGap=2f;
        public const float SelectionSize=30f;
        public static readonly KitColor Frame=KitPalette.SheetInset;
        public static readonly KitColor Label=KitPalette.SheetLabel;
        public static readonly KitColor Selection=KitPalette.SheetValue;
        public static readonly KitColor Cursor=KitPalette.SheetValue;
        /// <summary>Behind the map image where the zoomed-out map does not cover the view.</summary>
        public static readonly KitColor Backdrop=new KitColor(0f,0f,0f,0.35f);
    }

    /// <summary>KX09 pin visibility rules.</summary>
    public static class KitMapRules {
        /// <summary>
        /// A pin shows when it is always shown, when there is no explored mask, or when the mask says its map point is
        /// explored. A throwing mask hides the pin (fail closed: never reveal an unexplored place).
        /// </summary>
        public static bool Shows(KitVector at,bool always,Func<KitVector,bool>? explored){
            if(always||explored==null)return true;
            try{return explored(at);}catch{return false;}
        }

        internal static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        internal static bool Finite(KitVector value)=>Finite(value.X)&&Finite(value.Y);
    }

    /// <summary>
    /// KX09 camera over a square map in normalized map coordinates (0..1 on both axes, u to the right, v upward, as
    /// the game's <c>Minimap.WorldToMapPoint</c> returns). View coordinates are pixels from the view's bottom-left
    /// corner (Unity <c>RectTransform</c> local space with a bottom-left pivot). At zoom 1 the whole map fits the shorter
    /// view side; zoom is uniform from <see cref="KitMapLayout.MinZoom"/> to <see cref="KitMapLayout.MaxZoom"/>; the
    /// center is clamped so the view never leaves the map (an axis wider than the map stays centered). Non-finite input
    /// is ignored.
    /// </summary>
    public sealed class KitMapCamera {
        static readonly KitVector Middle=new KitVector(0.5f,0.5f);
        public KitVector ViewSize { get; private set; }
        public KitVector Center { get; private set; }=Middle;
        public float Zoom { get; private set; }=1f;
        /// <summary>Pad cursor in view pixels.</summary>
        public KitVector Cursor { get; private set; }

        public KitMapCamera(KitVector viewSize){Resize(viewSize);}

        /// <summary>Map units to view pixels.</summary>
        public float Scale=>KitMath.Min(ViewSize.X,ViewSize.Y)*Zoom;
        public KitVector CursorMap=>ViewToMap(Cursor);
        KitVector Half=>ViewSize/2f;

        /// <summary>Sets the view size (at least one pixel each way), keeps zoom, re-clamps the center and recentres the pad cursor.</summary>
        public void Resize(KitVector size){
            if(!KitMapRules.Finite(size))return;
            ViewSize=new KitVector(KitMath.Max(1f,size.X),KitMath.Max(1f,size.Y));
            Clamp();
            ResetCursor();
        }

        public void ResetCursor(){Cursor=Half;}

        public KitVector MapToView(KitVector map)=>(map-Center)*Scale+Half;
        public KitVector ViewToMap(KitVector view)=>(view-Half)/Scale+Center;

        /// <summary>Sets a clamped zoom keeping the map point under <paramref name="anchor"/> (view pixels; the view center when null).</summary>
        public void SetZoom(float zoom,KitVector? anchor=null){
            if(!KitMapRules.Finite(zoom))return;
            var at=anchor??Half;
            if(!KitMapRules.Finite(at))return;
            var under=ViewToMap(at);
            Zoom=KitMath.Clamp(zoom,KitMapLayout.MinZoom,KitMapLayout.MaxZoom);
            Center=under-(at-Half)/Scale;
            Clamp();
        }

        /// <summary>Mouse wheel: positive steps zoom in by <see cref="KitMapLayout.WheelFactor"/> each, about the pointer.</summary>
        public void ZoomBy(int steps,KitVector anchor){
            if(steps==0||!KitMapRules.Finite(anchor))return;
            SetZoom(Zoom*(float)Math.Pow(KitMapLayout.WheelFactor,steps),anchor);
        }

        /// <summary>Pad triggers: <paramref name="axis"/> -1..1 (in positive) for <paramref name="deltaTime"/> seconds, about the pad cursor.</summary>
        public void ZoomCursor(float axis,float deltaTime){
            if(!KitMapRules.Finite(axis)||!KitMapRules.Finite(deltaTime)||deltaTime<=0f||KitMath.Abs(axis)<KitMapLayout.PadDeadZone)return;
            SetZoom(Zoom*(float)Math.Pow(2.0,KitMath.Clamp(axis,-1f,1f)*KitMapLayout.PadZoomSpeed*deltaTime),Cursor);
        }

        /// <summary>Drag: the map follows the pointer by <paramref name="viewDelta"/> pixels.</summary>
        public void Pan(KitVector viewDelta){
            if(!KitMapRules.Finite(viewDelta))return;
            Center-=viewDelta/Scale;
            Clamp();
        }

        public void CenterOn(KitVector map){
            if(!KitMapRules.Finite(map))return;
            Center=map;
            Clamp();
        }

        /// <summary>
        /// Left stick: moves the pad cursor at <see cref="KitMapLayout.PadCursorSpeed"/>; past the edge margin the map pans
        /// instead (as far as the clamp allows) and the cursor stays at the margin.
        /// </summary>
        public void MoveCursor(KitVector stick,float deltaTime){
            if(!KitMapRules.Finite(stick)||!KitMapRules.Finite(deltaTime)||deltaTime<=0f)return;
            if(stick.SqrMagnitude<KitMapLayout.PadDeadZone*KitMapLayout.PadDeadZone)return;
            if(stick.SqrMagnitude>1f)stick=stick/KitMath.Sqrt(stick.SqrMagnitude);
            var next=Cursor+stick*(KitMapLayout.PadCursorSpeed*deltaTime);
            float margin=KitMath.Min(KitMapLayout.EdgeMargin,KitMath.Min(ViewSize.X,ViewSize.Y)/2f);
            var held=new KitVector(KitMath.Clamp(next.X,margin,ViewSize.X-margin),KitMath.Clamp(next.Y,margin,ViewSize.Y-margin));
            var overflow=next-held;
            if(overflow!=KitVector.Zero)Pan(KitVector.Zero-overflow);
            Cursor=held;
        }

        /// <summary>The map region the view shows, clipped to the map: the texture UV rect of the map image.</summary>
        public KitRect VisibleMap{get{
            var low=ViewToMap(KitVector.Zero);
            var high=ViewToMap(ViewSize);
            return KitRect.MinMax(KitMath.Max(0f,low.X),KitMath.Max(0f,low.Y),KitMath.Min(1f,high.X),KitMath.Min(1f,high.Y));
        }}

        /// <summary>Where the clipped map image sits in the view, in view pixels (so a zoomed-out map never wraps its texture).</summary>
        public KitRect ImageRect{get{
            var visible=VisibleMap;
            var low=MapToView(new KitVector(visible.XMin,visible.YMin));
            var high=MapToView(new KitVector(visible.XMax,visible.YMax));
            return KitRect.MinMax(low.X,low.Y,high.X,high.Y);
        }}

        public bool InView(KitVector view,float margin)=>KitMapRules.Finite(view)&&view.X>=-margin&&view.Y>=-margin&&view.X<=ViewSize.X+margin&&view.Y<=ViewSize.Y+margin;

        /// <summary>
        /// The shown pin nearest to <paramref name="view"/> within <paramref name="radius"/> view pixels, or -1. A tie goes to
        /// the lower index; <paramref name="shows"/> null shows every pin.
        /// </summary>
        public int HitTest(KitVector view,IReadOnlyList<KitVector>? pins,Func<int,bool>? shows,float radius){
            if(pins==null||!KitMapRules.Finite(view)||!KitMapRules.Finite(radius)||radius<0f)return -1;
            int best=-1;
            float bestDistance=radius*radius;
            for(int i=0;i<pins.Count;i++){
                if(!KitMapRules.Finite(pins[i]))continue;
                if(shows!=null){bool shown;try{shown=shows(i);}catch{shown=false;}if(!shown)continue;}
                float distance=(MapToView(pins[i])-view).SqrMagnitude;
                if(distance<=bestDistance&&(best<0||distance<bestDistance)){best=i;bestDistance=distance;}
            }
            return best;
        }

        void Clamp(){
            float scale=Scale;
            Center=new KitVector(Axis(Center.X,ViewSize.X/scale),Axis(Center.Y,ViewSize.Y/scale));
        }

        static float Axis(float centre,float extent)=>extent>=1f?0.5f:KitMath.Clamp(centre,extent/2f,1f-extent/2f);
    }
}
