using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    // Native adapters serialize access onto the Unity thread; this coordinator
    // never executes work on the network thread or assumes a remote actor ID.
    public sealed class RequestRouter {
        sealed class Work {internal RequestEnvelope Envelope=null!;internal AuthorityContext Context=null!;internal OperationEntry Entry=null!;internal OperationResult Result=null!;internal double Expires;internal Func<ExecutionFrame>? Poll;internal long RecoveryGeneration=-1;}
        sealed class Listener {internal ProductRegistration Owner=null!;internal Action<OperationResult> Callback=null!;internal Action Cleanup=null!;}
        readonly OperationRegistry operations;readonly SessionTracker sessions;readonly Func<double> clock;
        readonly Dictionary<RequestIdentity,Work> work=new Dictionary<RequestIdentity,Work>();readonly List<Listener> listeners=new List<Listener>();readonly RequestLedger ledger=new RequestLedger();bool stopped;
        Work[]? workSnapshot;
        Work[] SnapshotWork()=>workSnapshot??(workSnapshot=work.Values.ToArray());
        void RemoveWork(RequestIdentity id){if(work.Remove(id))workSnapshot=null;}
        public Func<AuthorityContext,bool>? ValidateLiveAuthority{get;set;}
        public RequestRouter(OperationRegistry operations,SessionTracker sessions,Func<double> monotonicClock){this.operations=operations??throw new ArgumentNullException(nameof(operations));this.sessions=sessions??throw new ArgumentNullException(nameof(sessions));clock=monotonicClock??throw new ArgumentNullException(nameof(monotonicClock));}
        static bool Terminal(OperationState state)=>state==OperationState.Succeeded||state==OperationState.Rejected||state==OperationState.Cancelled;
        bool Current(AuthorityContext peer)=>peer.Session.AuthorityReady&&peer.Session.IsAuthority&&sessions.Current.AuthorityReady&&sessions.Current.IsAuthority&&peer.Session.Generation==sessions.Current.Generation&&peer.Session.SessionId==sessions.Current.SessionId&&peer.Session.WorldId==sessions.Current.WorldId;
        public RequestAdmission Submit(AuthorityContext peer,RequestEnvelope envelope) {
            if(peer==null||envelope==null)throw new ArgumentNullException();
            if(stopped||!Current(peer)||envelope.SessionId!=peer.Session.SessionId||envelope.WorldId!=peer.Session.WorldId)return new RequestAdmission(false,"SessionUnavailable");
            if(envelope.PayloadLength>16384)return new RequestAdmission(false,"PayloadTooLarge");
            var id=new RequestIdentity(peer.ActorId,envelope.WorldId,envelope.SessionId,envelope.RequestId);
            var expired=new List<RequestIdentity>();ledger.Expire(clock(),expired);foreach(var old in expired)RemoveWork(old);
            var entry=operations.Find(envelope.OperationId,envelope.Protocol);
            if(entry==null)return new RequestAdmission(false,"OperationUnavailable");
            if(work.TryGetValue(id,out var existing)){
                if(!ReferenceEquals(existing.Entry,entry)||!existing.Entry.Active)return new RequestAdmission(false,"OwnerUnavailable");
                return new RequestAdmission(existing.Envelope.Same(envelope),existing.Envelope.Same(envelope)?"Existing":"IdentityConflict",existing.Result);
            }
            if(!peer.IsLocal&&!sessions.Peers.Resolve(peer.SenderId,entry.Owner.Metadata.Id,envelope.Protocol))return new RequestAdmission(false,"PeerUnsupported");
            var pending=work.Values.Where(w=>!Terminal(w.Result.State)).ToArray();
            if(pending.Length>=256||pending.Count(w=>w.Context.SenderId==peer.SenderId)>=32)return new RequestAdmission(false,"PendingCapacity");
            // Completed ids are held for a bounded window (RequestLedger), so replays inside it reuse the stored outcome
            // while a long session never fills the ledger; only an actor's own quota or a burst of young ids refuses.
            var evicted=new List<RequestIdentity>();bool room=ledger.Admit(id.ActorId,0,clock(),evicted);foreach(var old in evicted)RemoveWork(old);
            if(!room)return new RequestAdmission(false,"LedgerCapacity");
            var item=new Work{Envelope=new RequestEnvelope(envelope.OperationId,envelope.Protocol,envelope.WorldId,envelope.SessionId,envelope.RequestId,envelope.Payload),Context=peer.ForRequest(id),Entry=entry,Expires=clock()+10,Result=new OperationResult(id,OperationState.Queued,"Queued")};
            work.Add(id,item);workSnapshot=null;return new RequestAdmission(true,"Queued",item.Result);
        }
        public void Drain() {
            if(stopped)return;
            var snapshot=SnapshotWork();
            foreach(var item in snapshot) {
                if(Terminal(item.Result.State))continue;
                if(!item.Entry.Active){Set(item,item.Context.DecisionMade?OperationState.RecoveryRequired:OperationState.Cancelled,"OwnerUnavailable");continue;}
                try {
                    ExecutionFrame? frame=null;
                    if(item.Result.State==OperationState.RecoveryRequired) {
                        if(!sessions.Current.AuthorityReady||!sessions.Current.IsAuthority||sessions.Current.WorldId!=item.Context.Identity.WorldId)continue;
                        item.RecoveryGeneration=sessions.Current.Generation;
                        frame=item.Entry.Recover(item.Context.Identity);
                    }else if(item.RecoveryGeneration>=0) {
                        if(!sessions.Current.AuthorityReady||!sessions.Current.IsAuthority||sessions.Current.Generation!=item.RecoveryGeneration||sessions.Current.WorldId!=item.Context.Identity.WorldId){Set(item,OperationState.RecoveryRequired,"SessionChanged");continue;}
                        if(item.Poll!=null)frame=item.Poll();
                    }else if(!Current(item.Context)) {Set(item,item.Context.DecisionMade?OperationState.RecoveryRequired:OperationState.Cancelled,"SessionChanged");continue;}
                    else if(item.Result.State==OperationState.Queued) {
                        if(clock()>=item.Expires){Set(item,OperationState.Cancelled,"Expired");continue;}
                        if(ValidateLiveAuthority!=null&&!ValidateLiveAuthority(item.Context)){Set(item,OperationState.Cancelled,"ActorUnavailable");continue;}
                        // Put the request in-flight before dispatch so reentrant
                        // duplicates see this same identity and never re-execute.
                        item.Result=new OperationResult(item.Context.Identity,OperationState.Pending,"Dispatching");
                        frame=item.Entry.Execute(item.Context,item.Envelope.Payload);
                    }else if(item.Poll!=null)frame=item.Poll();
                    if(frame!=null){item.Poll=frame.Poll;Set(item,frame.State,frame.Code,frame.Reply);}
                }catch{Set(item,item.Context.DecisionMade?OperationState.RecoveryRequired:OperationState.Rejected,"HandlerFailed");}
            }
        }
        void Set(Work item,OperationState state,string code,byte[]? reply=null) {
            // A consumer cannot report ordinary cancellation/rejection after it
            // marked a durable decision; reconciliation must resolve that decision.
            if(item.Context.DecisionMade&&(state==OperationState.Cancelled||state==OperationState.Rejected)){state=OperationState.RecoveryRequired;code="RecoveryRequired";}
            if(state==OperationState.RecoveryRequired){item.Poll=null;item.RecoveryGeneration=-1;}
            bool changed=state!=item.Result.State||code!=item.Result.Code;
            item.Result=new OperationResult(item.Context.Identity,state,code,reply);
            if(Terminal(state))ledger.Completed(item.Context.Identity,clock());
            if(changed&&(Terminal(state)||state==OperationState.RecoveryRequired))foreach(var listener in listeners.ToArray())if(ReferenceEquals(listener.Owner,item.Entry.Owner))try{listener.Callback(item.Result);}catch{ }
        }
        public OperationState Cancel(RequestIdentity id){if(!work.TryGetValue(id,out var item))return OperationState.Rejected;if(Terminal(item.Result.State))return item.Result.State;Set(item,item.Context.DecisionMade?OperationState.RecoveryRequired:OperationState.Cancelled,"Canceled");return item.Result.State;}
        public OperationResult Status(RequestIdentity id){if(!work.TryGetValue(id,out var item))return new OperationResult(id,OperationState.Rejected,"UnknownRequest");if(!item.Entry.Active)return new OperationResult(id,item.Context.DecisionMade||item.Result.State==OperationState.Succeeded?OperationState.RecoveryRequired:OperationState.Rejected,"OwnerUnavailable");return item.Result;}
        public IDisposable Subscribe(ProductRegistration owner,Action<OperationResult> callback){owner.EnsureActive();if(callback==null)throw new ArgumentNullException(nameof(callback));if(stopped)throw new ObjectDisposedException(nameof(RequestRouter));var listener=new Listener{Owner=owner,Callback=callback};listener.Cleanup=()=>{listeners.Remove(listener);owner.Disposed-=listener.Cleanup;};owner.Disposed+=listener.Cleanup;listeners.Add(listener);return new OwnedDisposal(listener.Cleanup);}
        public void ResetSession(SessionSnapshot next){if(next==null)throw new ArgumentNullException(nameof(next));foreach(var item in work.Values.ToArray()){if(item.Context.Session.SessionId==next.SessionId&&item.Context.Session.Generation==next.Generation)continue;if(!Terminal(item.Result.State))Set(item,item.Context.DecisionMade?OperationState.RecoveryRequired:OperationState.Cancelled,"SessionChanged");if(!item.Context.DecisionMade||Terminal(item.Result.State)){RemoveWork(item.Context.Identity);ledger.Forget(item.Context.Identity);}}}
        public RequestDiagnostics Diagnostics()=>new RequestDiagnostics(work.Values.Count(w=>!Terminal(w.Result.State)),work.Values.Count(w=>Terminal(w.Result.State)),work.Values.Count(w=>w.Result.State==OperationState.RecoveryRequired));
        public void Stop(){if(stopped)return;foreach(var item in work.Values.Where(w=>!Terminal(w.Result.State)).ToArray())Set(item,item.Context.DecisionMade?OperationState.RecoveryRequired:OperationState.Cancelled,"Shutdown");foreach(var listener in listeners.ToArray())listener.Cleanup();stopped=true;}
    }
}
