using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    public sealed class PeerCommandRouter {
        sealed class Work {internal PeerCommandContext Context=null!;internal PeerCommandEntry Entry=null!;internal RequestEnvelope Envelope=null!;internal OperationResult Result=null!;internal double Deadline;internal Func<ExecutionFrame>? Poll;}
        readonly PeerCommandRegistry registry;readonly SessionTracker sessions;readonly Func<double> clock;readonly Dictionary<RequestIdentity,Work> work=new Dictionary<RequestIdentity,Work>();readonly RequestLedger ledger=new RequestLedger();bool stopped;
        Work[]? workSnapshot;
        Work[] SnapshotWork()=>workSnapshot??(workSnapshot=work.Values.ToArray());
        void RemoveWork(RequestIdentity id){if(work.Remove(id))workSnapshot=null;}
        public Func<PeerCommandContext,bool>? ValidateLiveAuthority{get;set;}
        public PeerCommandRouter(PeerCommandRegistry registry,SessionTracker sessions,Func<double> monotonicClock){this.registry=registry??throw new ArgumentNullException(nameof(registry));this.sessions=sessions??throw new ArgumentNullException(nameof(sessions));clock=monotonicClock??throw new ArgumentNullException(nameof(monotonicClock));}
        static bool Terminal(OperationState state)=>state==OperationState.Succeeded||state==OperationState.Rejected||state==OperationState.Cancelled;
        bool Current(PeerCommandContext context){
            var current=sessions.Current;var incoming=context.Session;
            return current.AuthorityReady&&incoming.AuthorityReady&&incoming.Generation==current.Generation&&incoming.WorldId==current.WorldId&&incoming.SessionId==current.SessionId&&
                (context.IsLocal?current.IsAuthority&&incoming.IsAuthority:!current.IsAuthority&&!incoming.IsAuthority&&context.ServerConnectionVerified);
        }
        bool NativeCurrent(PeerCommandContext context)=>Current(context)&&(ValidateLiveAuthority==null||ValidateLiveAuthority(context));
        public RequestAdmission Submit(PeerCommandContext context,RequestEnvelope envelope){
            if(context==null||envelope==null)throw new ArgumentNullException();
            if(stopped||!NativeCurrent(context)||context.Session.WorldId!=envelope.WorldId||context.Session.SessionId!=envelope.SessionId)return new RequestAdmission(false,"SessionUnavailable");
            if(envelope.PayloadLength>16384)return new RequestAdmission(false,"PayloadTooLarge");
            var identity=new RequestIdentity(context.LocalPeerId,envelope.WorldId,envelope.SessionId,envelope.RequestId);
            var expired=new List<RequestIdentity>();ledger.Expire(clock(),expired);foreach(var old in expired)RemoveWork(old);
            var entry=registry.Find(envelope.OperationId,envelope.Protocol);if(entry==null)return new RequestAdmission(false,"CommandUnavailable");
            if(work.TryGetValue(identity,out var existing)){
                if(!ReferenceEquals(existing.Entry,entry)||!entry.Active||!entry.Owner.IsActive)return new RequestAdmission(false,"OwnerUnavailable");
                if(existing.Context.ConnectionId!=context.ConnectionId||existing.Context.AuthorityPeerId!=context.AuthorityPeerId||!NativeCurrent(existing.Context))return new RequestAdmission(false,"ConnectionChanged");
                var same=existing.Envelope.Same(envelope);return new RequestAdmission(same,same?"Existing":"IdentityConflict",same?existing.Result:null);
            }
            // Old terminal epochs cannot be replayed; uncertain outcomes remain
            // retained until their product's durable recovery establishes them.
            foreach(var old in work.Where(pair=>Terminal(pair.Value.Result.State)&&(pair.Value.Context.Session.SessionId!=sessions.Current.SessionId||pair.Value.Context.Session.WorldId!=sessions.Current.WorldId)).Select(pair=>pair.Key).ToArray()){RemoveWork(old);ledger.Forget(old);}
            // Completed ids are held for a bounded window (RequestLedger); unresolved receipts still count toward the
            // 4096 capacity and are never evicted.
            var evicted=new List<RequestIdentity>();bool room=ledger.Admit(identity.ActorId,work.Count-ledger.Count,clock(),evicted);foreach(var old in evicted)RemoveWork(old);
            if(!room)return new RequestAdmission(false,"LedgerCapacity");
            var pending=work.Values.Where(w=>!Terminal(w.Result.State)).ToArray();
            if(pending.Length>=256||pending.Count(w=>w.Context.LocalPeerId==context.LocalPeerId)>=32)return new RequestAdmission(false,"PendingCapacity");
            var item=new Work{Context=context.ForRequest(identity),Envelope=new RequestEnvelope(envelope.OperationId,envelope.Protocol,envelope.WorldId,envelope.SessionId,envelope.RequestId,envelope.Payload),Entry=entry,Deadline=clock()+10,Result=new OperationResult(identity,OperationState.Queued,"Queued")};
            work.Add(identity,item);workSnapshot=null;return new RequestAdmission(true,"Queued",item.Result);
        }
        static OperationResult Unavailable(Work item,string code)=>new OperationResult(item.Result.Identity,item.Context.DecisionMade||item.Result.State==OperationState.Succeeded||item.Result.State==OperationState.RecoveryRequired?OperationState.RecoveryRequired:OperationState.Cancelled,code);
        public OperationResult Status(RequestIdentity identity){
            if(identity==null)throw new ArgumentNullException(nameof(identity));
            if(!work.TryGetValue(identity,out var item))return new OperationResult(identity,OperationState.Rejected,"UnknownCommand");
            if(!item.Entry.Active||!item.Entry.Owner.IsActive)return Unavailable(item,"OwnerUnavailable");
            if(!NativeCurrent(item.Context))return Unavailable(item,"SessionChanged");
            return item.Result;
        }
        public OperationResult Status(RequestIdentity identity,string command,Version protocol,string connection){
            if(identity==null)throw new ArgumentNullException(nameof(identity));
            if(work.TryGetValue(identity,out var item)&&(item.Envelope.OperationId!=command||item.Envelope.Protocol!=protocol||item.Context.ConnectionId!=connection))return new OperationResult(identity,OperationState.Rejected,"IdentityConflict");
            return Status(identity);
        }
        void Set(Work item,OperationState state,string code,byte[]? reply=null){
            if((item.Context.DecisionMade||item.Result.State==OperationState.RecoveryRequired)&&(state==OperationState.Rejected||state==OperationState.Cancelled))state=OperationState.RecoveryRequired;
            item.Result=new OperationResult(item.Result.Identity,state,code,reply);
            if(Terminal(state))ledger.Completed(item.Result.Identity,clock());
            if(Terminal(state)||state==OperationState.RecoveryRequired)item.Poll=null;
        }
        public void Cancel(RequestIdentity identity){if(identity==null)throw new ArgumentNullException(nameof(identity));if(work.TryGetValue(identity,out var item)&&!Terminal(item.Result.State))Set(item,item.Context.DecisionMade?OperationState.RecoveryRequired:OperationState.Cancelled,"Canceled");}
        public void Drain(){
            if(stopped)return;
            var snapshot=SnapshotWork();
            foreach(var item in snapshot){
                if(Terminal(item.Result.State)||item.Result.State==OperationState.RecoveryRequired)continue;
                try{
                    if(!item.Entry.Active||!item.Entry.Owner.IsActive){Set(item,OperationState.Cancelled,"OwnerUnavailable");continue;}
                    if(!NativeCurrent(item.Context)){Set(item,OperationState.Cancelled,"SessionChanged");continue;}
                    if(clock()>=item.Deadline){Set(item,OperationState.Cancelled,"Expired");continue;}
                    ExecutionFrame? frame=null;
                    if(item.Result.State==OperationState.Queued){item.Result=new OperationResult(item.Result.Identity,OperationState.Pending,"Dispatching");frame=item.Entry.Execute(item.Context,item.Envelope.Payload);}
                    else if(item.Poll!=null)frame=item.Poll();
                    if(frame!=null){item.Poll=frame.Poll;Set(item,frame.State,frame.Code,frame.Reply);}
                }catch{Set(item,OperationState.Rejected,"HandlerFailed");}
            }
        }
        public void Stop(){if(stopped)return;stopped=true;foreach(var item in work.Values)if(!Terminal(item.Result.State))Set(item,OperationState.Cancelled,"Shutdown");}
    }
}
