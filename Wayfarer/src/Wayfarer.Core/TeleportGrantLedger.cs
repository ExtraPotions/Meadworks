using System;
using System.Collections.Generic;
using System.Linq;
namespace Wayfarer.Core {
    public sealed class TeleportGrantLedger {
        sealed class Entry {internal TeleportGrant Grant=null!;internal bool Consumed;internal bool Completed;}
        readonly Dictionary<(string,long,string,string),Entry> entries=new Dictionary<(string,long,string,string),Entry>();
        static (string,long,string,string) Key(TeleportGrant grant)=>(grant.ActorId,grant.WorldId,grant.SessionId,grant.RequestId);
        public TeleportGrant? Issue(TeleportActorFacts actor,TeleportRequest request,AuthorizationDecision decision,double now){
            var key=(actor.ActorId,actor.WorldId,actor.SessionId,request.RequestId);
            if(!decision.Allowed||entries.ContainsKey(key)||entries.Count>=4096||entries.Values.Any(e=>e.Grant.ActorId==actor.ActorId&&!e.Completed&&now<e.Grant.ExpiresAt))return null;
            var target=decision.Target;var grant=new TeleportGrant(actor.ActorId,actor.WorldId,actor.SessionId,request.RequestId,Guid.NewGuid().ToString("N"),request.Category,request.DestinationId,target.X,target.Y,target.Z,now+10);
            entries.Add(key,new Entry{Grant=grant});return grant;
        }
        // Called only after the shared authenticated request client verifies the
        // current server response. Repeated packets cannot replace a used grant.
        public bool TrackVerifiedGrant(TeleportGrant grant){var key=Key(grant);if(entries.ContainsKey(key)||entries.Count>=4096)return false;entries.Add(key,new Entry{Grant=grant});return true;}
        public bool TryConsume(TeleportGrant grant,TeleportActorFacts liveActor,double now){
            if(liveActor.ActorId!=grant.ActorId||liveActor.WorldId!=grant.WorldId||liveActor.SessionId!=grant.SessionId||!liveActor.Present||!liveActor.Alive||liveActor.Teleporting||!liveActor.CooldownReady||now>=grant.ExpiresAt)return false;
            if(!entries.TryGetValue(Key(grant),out var entry)||entry.Consumed||entry.Completed||entry.Grant.GrantId!=grant.GrantId||entry.Grant.Category!=grant.Category||entry.Grant.DestinationId!=grant.DestinationId||entry.Grant.X!=grant.X||entry.Grant.Y!=grant.Y||entry.Grant.Z!=grant.Z)return false;
            entry.Consumed=true;return true;
        }
        public bool Complete(TeleportActorFacts actor,string request,string? grantId=null){
            if(!entries.TryGetValue((actor.ActorId,actor.WorldId,actor.SessionId,request),out var entry)||grantId!=null&&entry.Grant.GrantId!=grantId)return false;
            entry.Completed=true;return true;
        }
        public void Reset()=>entries.Clear();
    }
}
