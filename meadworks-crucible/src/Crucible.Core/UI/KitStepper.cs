using System;
using System.Globalization;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>The stepper (KX04): <c>- amount +</c>, buttons 44 wide, the field 62, 6 apart (kindle-design.md 3.2).</summary>
    public static class KitStepperLayout {
        public const float ButtonWidth=44f;
        public const float FieldWidth=62f;
        public const float Between=KitTabLayout.Between;
        public const float Width=ButtonWidth*2f+FieldWidth+Between*2f;
        public const float FieldX=ButtonWidth+Between;
        public const float PlusX=Width-ButtonWidth;
        /// <summary>From the stepper to the button it sits beside (Craft).</summary>
        public const float CraftGap=6f;
        /// <summary>The - and + captions' size (the Craft button's font).</summary>
        public const float CaptionSize=26f;
    }

    /// <summary>
    /// The stepper's amount rules (OpenKeep 3.0.0 batch behavior, rebuilt generic): a click steps one; Shift + click to
    /// the next multiple of ten; Ctrl + click to the minimum or to the most now; the wheel does the same over each part;
    /// a held D-pad, which the game repeats, steps by tens after <see cref="FastAfter"/> quick repeats. Always within the
    /// minimum and the smallest of the maximum, the product's "most now" cap and four digits.
    /// </summary>
    public static class KitStepRules {
        public const int Most=9999;
        public const float RepeatWindow=0.35f;
        public const int FastAfter=8;

        /// <summary>The highest amount allowed now (never under <paramref name="min"/>).</summary>
        public static int Limit(int min,int max,int cap){
            if(min<0)throw new ArgumentOutOfRangeException(nameof(min));
            int high=Math.Min(Math.Min(max,cap),Most);
            return Math.Max(high,min);
        }

        /// <summary>The amount after one step in <paramref name="direction"/> (+1 or -1), clamped to min and <paramref name="limit"/>.</summary>
        public static int Step(int value,int direction,int min,int limit,KitClickKind kind,bool fast){
            if(direction==0)return Clamp(value,min,limit);
            int up=direction>0?1:-1;
            int target;
            if(kind==KitClickKind.Ctrl)target=up>0?limit:min;
            else if(kind==KitClickKind.Shift||fast)target=up>0?(value/10+1)*10:(value-1)/10*10;
            else target=value+up;
            return Clamp(target,min,limit);
        }

        public static int Clamp(int value,int min,int limit)=>value<min?min:value>limit?limit:value;
    }

    /// <summary>Counts quick gamepad repeats in one direction; true once there have been <see cref="KitStepRules.FastAfter"/>.</summary>
    public sealed class KitStepRepeat {
        int streak,lastDirection;
        float last=-10f;

        public bool Next(int direction,float now,bool gamepad){
            bool repeat=gamepad&&direction==lastDirection&&now-last<KitStepRules.RepeatWindow;
            streak=repeat?streak+1:0;
            lastDirection=direction;
            last=now;
            return streak>=KitStepRules.FastAfter;
        }

        public void Reset(){streak=0;lastDirection=0;last=-10f;}
    }

    /// <summary>
    /// The stepper's amount: within <see cref="Min"/> and the limit of <see cref="Max"/>, the product's <see cref="Cap"/>
    /// ("most now", which may drop as materials are used) and four digits. <see cref="Changed"/> fires on every change.
    /// </summary>
    public sealed class KitAmount {
        public int Min { get; }
        public int Max { get; }
        public int Cap { get; private set; }
        public int Value { get; private set; }
        public int Limit=>KitStepRules.Limit(Min,Max,Cap);
        public bool CanLess=>Value>Min;
        public bool CanMore=>Value<Limit;
        public string Text=>Value.ToString(CultureInfo.InvariantCulture);

        public event Action<int>? Changed;

        public KitAmount(int min=1,int max=KitStepRules.Most){
            if(min<0)throw new ArgumentOutOfRangeException(nameof(min));
            if(max<min)throw new ArgumentOutOfRangeException(nameof(max));
            Min=min;Max=max;Cap=max;Value=min;
        }

        /// <summary>The most allowed now; the amount drops to it when above.</summary>
        public void Follow(int cap){Cap=cap;Put(KitStepRules.Clamp(Value,Min,Limit));}

        public void Step(int direction,KitClickKind kind,bool fast)=>Put(KitStepRules.Step(Value,direction,Min,Limit,kind,fast));

        /// <summary>An amount typed into the field (empty or invalid keeps the current one).</summary>
        public void Set(string? typed)=>Put(KitFieldRules.Amount(typed,Value,Min,Limit));

        public void Set(int value)=>Put(KitStepRules.Clamp(value,Min,Limit));

        public void Reset()=>Put(Min);

        void Put(int value){
            if(value==Value)return;
            Value=value;
            Changed?.Invoke(value);
        }
    }
}
