using System;
namespace ExtraPotions.Wildroot {
    public enum SharedPlacementReadiness { Ready, Waiting, Expired }
    /// <summary>Retries only settled unpaid cells; uncertain payments retain their original transaction.</summary>
    public sealed class SharedPlacementRetry {
        public const int MaxRetries=4;
        public double WindowSeconds{get;}
        public int Attempts{get;private set;}
        public double ReadyAt{get;private set;}
        // Only observed area unavailability consumes this window; normal FIFO residence is not a failure.
        double waitingSince=-1;
        public SharedPlacementRetry(double roundTripSeconds=0){
            double latency=double.IsNaN(roundTripSeconds)||double.IsInfinity(roundTripSeconds)?0:Math.Max(0,roundTripSeconds);
            // Burrow can spend ten seconds obtaining chest ownership before answering a reservation.
            WindowSeconds=Math.Min(45,15+8*latency);
        }
        void Observe(double now){if(waitingSince<0)waitingSince=now;}
        public bool Expired(double now)=>waitingSince>=0&&now-waitingSince>=WindowSeconds;
        public bool WaitForArea(bool stillPlanting,bool permitted,bool areaReady,double now){
            if(areaReady){waitingSince=-1;return false;}
            if(!stillPlanting||!permitted)return false;
            Observe(now);return !Expired(now);
        }
        public SharedPlacementReadiness CheckReadiness(bool areaReady,double now){
            if(WaitForArea(true,true,areaReady,now))return SharedPlacementReadiness.Waiting;
            if(!areaReady)return SharedPlacementReadiness.Expired;
            return Waiting(now)?SharedPlacementReadiness.Waiting:SharedPlacementReadiness.Ready;
        }
        public bool TryRetry(PlacementResultKind result,bool stillPlanting,bool permitted,double now){
            if(!stillPlanting||!permitted||(result!=PlacementResultKind.Cancelled&&result!=PlacementResultKind.InsufficientResources))return false;
            if(Attempts>=MaxRetries)return false;
            Attempts++;ReadyAt=now+Math.Min(4,Math.Pow(2,Attempts-1));return true;
        }
        public bool Waiting(double now)=>now<ReadyAt;
        public string RequestId(string cell)=>Attempts==0?cell:cell+":retry"+(Attempts==1?"":":"+Attempts);
    }
}
