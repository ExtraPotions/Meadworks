using System;
namespace ExtraPotions.Wildroot {
    /// <summary>Retries only settled unpaid cells; uncertain payments retain their original transaction.</summary>
    public sealed class SharedPlacementRetry {
        public const int MaxRetries=4;
        public double WindowSeconds{get;}
        public int Attempts{get;private set;}
        public double ReadyAt{get;private set;}
        double waitingSince=-1;
        public SharedPlacementRetry(double roundTripSeconds=0){
            double latency=double.IsNaN(roundTripSeconds)||double.IsInfinity(roundTripSeconds)?0:Math.Max(0,roundTripSeconds);
            // Burrow can spend ten seconds obtaining chest ownership before answering a reservation.
            WindowSeconds=Math.Min(45,15+8*latency);
        }
        void Observe(double now){if(waitingSince<0)waitingSince=now;}
        public bool Expired(double now)=>waitingSince>=0&&now-waitingSince>=WindowSeconds;
        public bool WaitForArea(bool stillPlanting,bool permitted,bool areaReady,double now){
            if(!stillPlanting||!permitted||areaReady)return false;
            Observe(now);return !Expired(now);
        }
        public bool TryRetry(PlacementResultKind result,bool stillPlanting,bool permitted,double now){
            if(!stillPlanting||!permitted||(result!=PlacementResultKind.Cancelled&&result!=PlacementResultKind.InsufficientResources))return false;
            Observe(now);if(Attempts>=MaxRetries||Expired(now))return false;
            Attempts++;ReadyAt=Math.Min(waitingSince+WindowSeconds,now+Math.Min(4,Math.Pow(2,Attempts-1)));return true;
        }
        public bool Waiting(double now)=>now<ReadyAt&&!Expired(now);
        public string RequestId(string cell)=>Attempts==0?cell:cell+":retry"+(Attempts==1?"":":"+Attempts);
    }
}
