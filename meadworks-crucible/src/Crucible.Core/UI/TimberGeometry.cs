// Adapted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/TimberFrame.cs, PackPanel/PackPanel/src/Look/TimberCoordinates.cs and the
// ring loop of PackPanel/PackPanel/src/Look/TimberBackground.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: restated over KitVector/KitRect/KitMath so the geometry is
// Unity-free and testable; the border width and jaggedness are parameters instead of PackPanel settings.
using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// One continuous wood silhouette and bevel; no separate border texture or metal band (PackPanel's Timber look).
    /// The contour is chipped by a deterministic noise along each side, the bevel is four rings of the same contour, the
    /// fill is an ear-clipped triangulation of the innermost ring, and the wallpaper is one aspect-preserving image
    /// anchored at the root canvas's top-left, so growing a panel only reveals more of it.
    /// </summary>
    public static class TimberGeometry {
        public const float DefaultWidth=5.5f;
        public const float MinWidth=3f;
        public const float MaxWidth=8f;
        public const float DefaultJaggedness=1.5f;
        public const float MinJaggedness=0f;
        public const float MaxJaggedness=2.5f;
        public const int RoundSegments=192;
        public const int Rings=4;

        public static float BevelWidth(KitRect rect,bool round,float width,float roughness){
            float shortSide=KitMath.Min(rect.Width,rect.Height);
            float radius=round?shortSide*0.5f:KitMath.Min(12f,shortSide*0.3f);
            float cut=round?0f:KitMath.Min(roughness*3f,shortSide*0.12f);
            return KitMath.Min(KitMath.Min(width,shortSide*0.12f),KitMath.Max(0.5f,radius-cut-0.75f));
        }

        public static void Contour(KitRect rect,bool round,float roughness,List<KitVector> points,List<KitVector> normals){
            if(points==null)throw new ArgumentNullException(nameof(points));
            if(normals==null)throw new ArgumentNullException(nameof(normals));
            points.Clear();
            normals.Clear();
            if(round){
                for(int i=0;i<RoundSegments;i++){
                    float angle=-i*KitMath.PI*2f/RoundSegments;
                    var n=new KitVector(KitMath.Cos(angle),KitMath.Sin(angle));
                    points.Add(rect.Center+KitVector.Scale(n,rect.Size*0.5f));
                    normals.Add(n);
                }
                return;
            }
            float radius=KitMath.Min(12f,KitMath.Min(rect.Width,rect.Height)*0.3f);
            KitVector[] centers={
                new KitVector(rect.XMin+radius,rect.YMax-radius),
                new KitVector(rect.XMax-radius,rect.YMax-radius),
                new KitVector(rect.XMax-radius,rect.YMin+radius),
                new KitVector(rect.XMin+radius,rect.YMin+radius)
            };
            for(int side=0;side<4;side++){
                float startAngle=(180f-side*90f)*KitMath.Deg2Rad;
                var normal=new KitVector(KitMath.Cos(startAngle),KitMath.Sin(startAngle));
                var start=centers[(side+3)%4]+normal*radius;
                var end=centers[side]+normal*radius;
                int steps=Math.Max(1,KitMath.CeilToInt(KitVector.Distance(start,end)/2f));
                for(int i=0;i<steps;i++)
                    Add(KitVector.Lerp(start,end,i/(float)steps),normal,rect,roughness,points,normals);
                for(int i=0;i<8;i++){
                    float angle=startAngle-i/8f*KitMath.PI*0.5f;
                    var n=new KitVector(KitMath.Cos(angle),KitMath.Sin(angle));
                    Add(centers[side]+n*radius,n,rect,roughness,points,normals);
                }
            }
        }

        static void Add(KitVector p,KitVector n,KitRect rect,float roughness,List<KitVector> points,List<KitVector> normals){
            // Deliberate planar chips with visible depth. Each side varies along its own length,
            // rather than averaging two smooth fields until the silhouette becomes a straight line.
            float x=p.X-rect.XMin,y=p.Y-rect.YMin;
            float vertical=Noise(y,17f,2f)*0.8f+Noise(y,57f,12f)*0.2f;
            float horizontal=Noise(x,21f,9f)*0.8f+Noise(x,63f,21f)*0.2f;
            float depth=KitMath.Min(roughness*3f,KitMath.Min(rect.Width,rect.Height)*0.12f);
            float weight=KitMath.Abs(n.X)+KitMath.Abs(n.Y);
            float cut=depth*(0.12f+(KitMath.Abs(n.X)*vertical+KitMath.Abs(n.Y)*horizontal)/weight*0.88f);
            float profile=(KitMath.Abs(n.X)*vertical+KitMath.Abs(n.Y)*horizontal)/weight;
            cut+=depth*0.45f*KitMath.Clamp01((profile-0.55f)/0.3f);
            points.Add(p-n*cut);
            normals.Add(n);
        }

        /// <summary>The chip profile along one side, 0 to 1.</summary>
        public static float Noise(float position,float spacing,float seed){
            float cell=KitMath.Floor(position/spacing);
            float left=(cell+Hash(cell+seed+41f)*0.4f)*spacing;
            if(position<left){
                cell--;
                left=(cell+Hash(cell+seed+41f)*0.4f)*spacing;
            }
            float right=(cell+1f+Hash(cell+seed+42f)*0.4f)*spacing;
            float t=(position-left)/(right-left);
            // Ease the cut faces slightly without reducing their peak-to-valley depth.
            t=KitMath.Lerp(t,t*t*(3f-2f*t),0.35f);
            return KitMath.Lerp(Hash(cell+seed),Hash(cell+seed+1f),t);
        }

        static float Hash(float value)=>KitMath.Repeat(KitMath.Sin(value*127.1f)*43758.5453f,1f);

        /// <summary>Ear clipping of a clockwise polygon; throws when the contour intersects itself.</summary>
        public static void Triangulate(IReadOnlyList<KitVector> polygon,List<int> triangles){
            if(polygon==null)throw new ArgumentNullException(nameof(polygon));
            if(triangles==null)throw new ArgumentNullException(nameof(triangles));
            triangles.Clear();
            var remaining=new List<int>(polygon.Count);
            for(int i=0;i<polygon.Count;i++)remaining.Add(i);
            int cursor=0;
            while(remaining.Count>2){
                bool clipped=false;
                for(int step=0;step<remaining.Count;step++){
                    int i=(cursor+step)%remaining.Count;
                    int a=remaining[(i+remaining.Count-1)%remaining.Count];
                    int b=remaining[i],c=remaining[(i+1)%remaining.Count];
                    float turn=Cross(polygon[a],polygon[b],polygon[c]);
                    if(turn>0.00001f)continue;
                    if(KitMath.Abs(turn)<=0.00001f){
                        remaining.RemoveAt(i);
                        cursor=i%remaining.Count;
                        clipped=true;
                        break;
                    }
                    bool occupied=false;
                    float minX=KitMath.Min(polygon[a].X,KitMath.Min(polygon[b].X,polygon[c].X));
                    float maxX=KitMath.Max(polygon[a].X,KitMath.Max(polygon[b].X,polygon[c].X));
                    float minY=KitMath.Min(polygon[a].Y,KitMath.Min(polygon[b].Y,polygon[c].Y));
                    float maxY=KitMath.Max(polygon[a].Y,KitMath.Max(polygon[b].Y,polygon[c].Y));
                    foreach(int p in remaining){
                        if(p==a||p==b||p==c)continue;
                        if(polygon[p].X<minX||polygon[p].X>maxX||polygon[p].Y<minY||polygon[p].Y>maxY)continue;
                        if(Cross(polygon[a],polygon[b],polygon[p])<=0f&&Cross(polygon[b],polygon[c],polygon[p])<=0f&&Cross(polygon[c],polygon[a],polygon[p])<=0f){occupied=true;break;}
                    }
                    if(occupied)continue;
                    triangles.Add(a);triangles.Add(b);triangles.Add(c);
                    remaining.RemoveAt(i);
                    cursor=i%remaining.Count;
                    clipped=true;
                    break;
                }
                if(!clipped)throw new InvalidOperationException("Wood contour intersects itself.");
            }
        }

        static float Cross(KitVector a,KitVector b,KitVector c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);

        /// <summary>How far ring 0 to 3 lies inside the contour: the edge, 0.6, 1.4, then the full bevel width.</summary>
        public static float RingDepth(int ring,float width)=>ring==0?0f:ring==1?KitMath.Min(0.6f,width*0.2f):ring==2?KitMath.Min(1.4f,width*0.45f):width;

        /// <summary>The ring's grey shade at a contour normal: the outer ring dark, the upper-left edges lit.</summary>
        public static float RingShade(int ring,KitVector normal){
            float light=KitMath.Clamp(normal.Y-normal.X*0.35f,-1f,1f);
            return ring==0?0.7f:ring==1?0.80f+light*0.12f:ring==2?0.96f+light*0.13f:1f;
        }

        /// <summary>The outer ring fades to transparent; the others are opaque.</summary>
        public static float RingAlpha(int ring)=>ring==0?0f:1f;

        /// <summary>
        /// Wallpaper UV of a canvas point: a single aspect-preserving wood image covering the root canvas, anchored at
        /// its top-left. No panel dimensions enter this calculation: growing a panel only reveals more canvas.
        /// </summary>
        public static KitVector WallpaperUv(KitVector point,KitRect canvas,KitRect wood,KitVector textureSize){
            if(canvas.Width<=0f||canvas.Height<=0f||wood.Width<=0f||wood.Height<=0f||textureSize.X<=0f||textureSize.Y<=0f)
                return KitVector.Zero;
            float scale=Math.Max(canvas.Width/wood.Width,canvas.Height/wood.Height);
            return new KitVector(
                (wood.XMin+(point.X-canvas.XMin)/scale)/textureSize.X,
                (wood.YMax-(canvas.YMax-point.Y)/scale)/textureSize.Y);
        }
    }
}
