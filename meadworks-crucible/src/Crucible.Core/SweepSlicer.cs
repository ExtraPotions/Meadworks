using System;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Spreads a periodic sweep over a list across frames. A sweep starts at most once per period and each frame takes
    /// a slice of at least <c>budget</c> items, more when needed to finish the sweep within one period at the current
    /// frame time. No single frame walks a long list, and every item is still visited once per period. Main thread only.
    /// </summary>
    public sealed class SweepSlicer {
        readonly float period;readonly int budget;
        float nextSweep;int cursor;bool sweeping;

        public SweepSlicer(float period,int budget){
            if(float.IsNaN(period)||period<=0f)throw new ArgumentOutOfRangeException(nameof(period));
            if(budget<=0)throw new ArgumentOutOfRangeException(nameof(budget));
            this.period=period;this.budget=budget;
        }

        /// <summary>Whether a sweep is part-way through the list.</summary>
        public bool Sweeping=>sweeping;

        /// <summary>
        /// The slice to visit this frame: items [<paramref name="start"/>, start + returned count). Zero when no sweep
        /// is due or the list is empty. <paramref name="deltaTime"/> is the last frame's duration in seconds.
        /// </summary>
        public int Take(float now,float deltaTime,int total,out int start){
            start=0;
            if(total<=0){sweeping=false;cursor=0;return 0;}
            if(!sweeping){
                if(now<nextSweep)return 0;
                sweeping=true;cursor=0;nextSweep=now+period;
            }
            if(cursor>=total){sweeping=false;cursor=0;return 0;}
            int needed=float.IsNaN(deltaTime)||deltaTime<=0f?0:(int)Math.Min(int.MaxValue,Math.Ceiling(total*(double)deltaTime/period));
            start=cursor;
            int count=Math.Min(Math.Max(budget,needed),total-cursor);
            cursor+=count;
            if(cursor>=total){sweeping=false;cursor=0;}
            return count;
        }

        /// <summary>The item at <paramref name="index"/> left the list; keeps the sweep on the item that was next.</summary>
        public void Removed(int index){if(sweeping&&index>=0&&index<cursor)cursor--;}
    }
}
