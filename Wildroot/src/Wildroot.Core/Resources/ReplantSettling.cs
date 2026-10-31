namespace ExtraPotions.Wildroot {
    public enum ReplantWaitState { Settled, Waiting, Detach }
    /// <summary>An uncertain shared replant payment keeps its transaction polling: the provider usually answers on a later
    /// status poll (Burrow reports RecoveryRequired while its bounded work queue is full during grid planting). Cancelling at
    /// once would roll back a still-reserved payment and lose the replant. Only a recovery that outlasts the window detaches
    /// the replant from its harvest job; the transaction still settles in the background.</summary>
    public sealed class ReplantSettling {
        public const double WindowSeconds=15;
        double recoverySince=-1;
        public ReplantWaitState Check(bool settled,bool needsRecovery,double now){
            if(settled)return ReplantWaitState.Settled;
            if(!needsRecovery){recoverySince=-1;return ReplantWaitState.Waiting;}
            if(recoverySince<0)recoverySince=now;
            return now-recoverySince>=WindowSeconds?ReplantWaitState.Detach:ReplantWaitState.Waiting;
        }
    }
}
