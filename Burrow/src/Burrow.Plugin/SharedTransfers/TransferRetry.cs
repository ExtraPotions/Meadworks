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
        /// <summary>When the next automatic resend may go out; infinity once the round is used up.</summary>
        internal double NextAt=>Exhausted?double.PositiveInfinity:retryAt;
    }

    // When the requester walks its ledger. An unresolved transfer used to be reconciled every frame, including one whose
    // resends were used up or whose chest was unavailable. A pass now runs at once on an event (a reply arrived, a new
    // transfer, a new authority session), every Busy seconds while a reply is awaited or something changed, and otherwise
    // backs off to MaxStalled seconds, never later than the next resend. Timing only: what a pass decides is unchanged.
    internal sealed class TransferPassGate {
        internal const double Busy=0.25,MaxStalled=5;
        double nextAt,delay;
        internal bool Due(double now,bool signal)=>signal||now>=nextAt;
        internal void Wake(){nextAt=0;delay=0;}
        internal void Working(double now){delay=0;nextAt=now+Busy;}
        internal void Stalled(double now,double resendAt){
            delay=Math.Min(MaxStalled,Math.Max(Busy,delay*2));
            nextAt=Math.Min(now+delay,Math.Max(now,resendAt));
        }
    }
}
