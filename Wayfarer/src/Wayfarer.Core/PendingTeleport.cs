namespace Wayfarer.Core {
    public sealed class PendingTeleport {
        readonly TeleportActorFacts actor;readonly TeleportRequest request;readonly long generation;readonly double deadline;readonly TeleportTargetFacts departure;
        bool canceled,movement,recovered;double recoveryDeadline;
        public PendingTeleport(TeleportActorFacts actor,TeleportRequest request,long generation,double now,float departureX,float departureY,float departureZ){this.actor=actor;this.request=request;this.generation=generation;deadline=now+10;departure=new TeleportTargetFacts(LandingPolicy.IsValid(departureX,departureY,departureZ),departureX,departureY,departureZ);}
        bool Same(TeleportActorFacts live,long currentGeneration)=>currentGeneration==generation&&actor.ActorId==live.ActorId&&actor.WorldId==live.WorldId&&actor.SessionId==live.SessionId&&live.Present;
        public void Cancel(){canceled=true;}
        public bool TryAccept(TeleportGrant grant,TeleportActorFacts live,long currentGeneration,double now,bool effectiveEnabled){
            if(canceled||movement||!effectiveEnabled||!Same(live,currentGeneration)||!live.Alive||live.Teleporting||!live.CooldownReady||now>=deadline||now>=grant.ExpiresAt||grant.ActorId!=actor.ActorId||grant.WorldId!=actor.WorldId||grant.SessionId!=actor.SessionId||grant.RequestId!=request.RequestId||grant.Category!=request.Category||grant.DestinationId!=request.DestinationId)return false;
            movement=true;recoveryDeadline=now+30;return true;
        }
        public bool TryRecovery(TeleportActorFacts live,long currentGeneration,double now,out TeleportTargetFacts capturedDeparture){
            capturedDeparture=departure;if(!movement||recovered||!departure.Valid||now>=recoveryDeadline||!Same(live,currentGeneration)||!live.Alive||live.Teleporting)return false;
            recovered=true;return true;
        }
    }
}
