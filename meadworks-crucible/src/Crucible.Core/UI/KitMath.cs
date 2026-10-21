using System;
using System.Globalization;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Unity-free stand-ins for the parts of <c>Mathf</c>, <c>Vector2</c>, <c>Rect</c> and <c>Color</c> the PackPanel UI
    /// kit's geometry uses (C08). Each method follows Unity's own float implementation (for example <c>Mathf.Lerp</c>
    /// clamps t, <c>Mathf.Repeat</c> floors, <c>Mathf.Sin</c> is <c>(float)Math.Sin</c>), so the procedural Timber
    /// outline computed here is the one PackPanel 0.12.0 draws.
    /// </summary>
    public static class KitMath {
        public const float PI=3.14159274f;
        public const float Deg2Rad=PI*2f/360f;
        public static float Sin(float f)=>(float)Math.Sin(f);
        public static float Cos(float f)=>(float)Math.Cos(f);
        public static float Sqrt(float f)=>(float)Math.Sqrt(f);
        public static float Floor(float f)=>(float)Math.Floor(f);
        public static int CeilToInt(float f)=>(int)Math.Ceiling(f);
        public static float Abs(float f)=>Math.Abs(f);
        public static float Min(float a,float b)=>a<b?a:b;
        public static float Max(float a,float b)=>a>b?a:b;
        public static float Clamp(float value,float min,float max)=>value<min?min:value>max?max:value;
        public static float Clamp01(float value)=>value<0f?0f:value>1f?1f:value;
        public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
        public static float Repeat(float t,float length)=>Clamp(t-Floor(t/length)*length,0f,length);
        public static float MoveTowards(float current,float target,float maxDelta){
            if(Abs(target-current)<=maxDelta)return target;
            return current+(target-current>=0f?1f:-1f)*maxDelta;
        }
    }

    /// <summary>A 2D point or direction (Unity <c>Vector2</c> semantics, float precision).</summary>
    public readonly struct KitVector:IEquatable<KitVector> {
        public float X { get; }
        public float Y { get; }
        public KitVector(float x,float y){X=x;Y=y;}
        public static KitVector Zero=>new KitVector(0f,0f);
        public static KitVector operator+(KitVector a,KitVector b)=>new KitVector(a.X+b.X,a.Y+b.Y);
        public static KitVector operator-(KitVector a,KitVector b)=>new KitVector(a.X-b.X,a.Y-b.Y);
        public static KitVector operator*(KitVector a,float d)=>new KitVector(a.X*d,a.Y*d);
        public static KitVector operator/(KitVector a,float d)=>new KitVector(a.X/d,a.Y/d);
        public static bool operator==(KitVector a,KitVector b)=>a.Equals(b);
        public static bool operator!=(KitVector a,KitVector b)=>!a.Equals(b);
        public static KitVector Scale(KitVector a,KitVector b)=>new KitVector(a.X*b.X,a.Y*b.Y);
        public static float Dot(KitVector a,KitVector b)=>a.X*b.X+a.Y*b.Y;
        public float SqrMagnitude=>X*X+Y*Y;
        public static float Distance(KitVector a,KitVector b){float dx=a.X-b.X,dy=a.Y-b.Y;return (float)Math.Sqrt(dx*dx+dy*dy);}
        public static KitVector Lerp(KitVector a,KitVector b,float t){t=KitMath.Clamp01(t);return new KitVector(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);}
        public bool Equals(KitVector other)=>X.Equals(other.X)&&Y.Equals(other.Y);
        public override bool Equals(object? obj)=>obj is KitVector other&&Equals(other);
        public override int GetHashCode()=>(X.GetHashCode()*397)^Y.GetHashCode();
        public override string ToString()=>"("+X.ToString("0.###",CultureInfo.InvariantCulture)+", "+Y.ToString("0.###",CultureInfo.InvariantCulture)+")";
    }

    /// <summary>An axis-aligned rectangle (Unity <c>Rect</c> semantics: position is the minimum corner).</summary>
    public readonly struct KitRect:IEquatable<KitRect> {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public KitRect(float x,float y,float width,float height){X=x;Y=y;Width=width;Height=height;}
        public static KitRect MinMax(float xMin,float yMin,float xMax,float yMax)=>new KitRect(xMin,yMin,xMax-xMin,yMax-yMin);
        public float XMin=>X;
        public float YMin=>Y;
        public float XMax=>X+Width;
        public float YMax=>Y+Height;
        public KitVector Center=>new KitVector(X+Width/2f,Y+Height/2f);
        public KitVector Size=>new KitVector(Width,Height);
        public bool Equals(KitRect other)=>X.Equals(other.X)&&Y.Equals(other.Y)&&Width.Equals(other.Width)&&Height.Equals(other.Height);
        public override bool Equals(object? obj)=>obj is KitRect other&&Equals(other);
        public override int GetHashCode()=>(((X.GetHashCode()*397)^Y.GetHashCode())*397^Width.GetHashCode())*397^Height.GetHashCode();
        public static bool operator==(KitRect a,KitRect b)=>a.Equals(b);
        public static bool operator!=(KitRect a,KitRect b)=>!a.Equals(b);
        public override string ToString()=>"("+X+", "+Y+", "+Width+", "+Height+")";
    }

    /// <summary>A color as Unity's <c>Color</c> (floats 0..1); also used for four-float values such as text margins.</summary>
    public readonly struct KitColor:IEquatable<KitColor> {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }
        public KitColor(float r,float g,float b,float a=1f){R=r;G=g;B=b;A=a;}
        /// <summary>A <c>Color32</c> value as Unity converts it to <c>Color</c> (byte / 255).</summary>
        public static KitColor FromBytes(byte r,byte g,byte b,byte a=255)=>new KitColor(r/255f,g/255f,b/255f,a/255f);
        public bool Equals(KitColor other)=>R.Equals(other.R)&&G.Equals(other.G)&&B.Equals(other.B)&&A.Equals(other.A);
        public override bool Equals(object? obj)=>obj is KitColor other&&Equals(other);
        public override int GetHashCode()=>(((R.GetHashCode()*397)^G.GetHashCode())*397^B.GetHashCode())*397^A.GetHashCode();
        public static bool operator==(KitColor a,KitColor b)=>a.Equals(b);
        public static bool operator!=(KitColor a,KitColor b)=>!a.Equals(b);
        public override string ToString()=>"RGBA("+R+", "+G+", "+B+", "+A+")";
    }
}
