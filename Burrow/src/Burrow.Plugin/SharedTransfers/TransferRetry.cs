using System;

namespace Burrow.SharedTransfers {
    // Automatic resend of one unresolved shared transfer. Each send is a Crucible
    // operation and the authority keeps every request id for its session, so the
    // client backs off (1, 2, 4, 8, 16, then 30 s) and stops after MaxAttempts.
    // Stopping never settles the intent: no elapsed time can manufacture a refund,
    // so the escrow stays durable and only an event (a new authority session or a
    // player action) re-arms one more bounded round.
    internal sealed class TransferRetry {
        internal const int MaxAttempts=8;
        internal const double FirstDelay=1,MaxDelay=30;
        int failures;
        double retryAt;
        internal bool Exhausted=>failures>=MaxAttempts;
        internal bool Due(double now)=>!Exhausted&&now>=retryAt;
        // An attempt ended without a recorded outcome. True only for the failure
        // that used up the round, so the player is told exactly once.
        internal bool Failed(double now){
            if(Exhausted)return false;
            failures++;
            retryAt=now+Math.Min(MaxDelay,FirstDelay*Math.Pow(2,failures-1));
            return Exhausted;
        }
        internal void Rearm(double now){failures=0;retryAt=now;}
        internal void Reset(){failures=0;retryAt=0;}
    }
}
