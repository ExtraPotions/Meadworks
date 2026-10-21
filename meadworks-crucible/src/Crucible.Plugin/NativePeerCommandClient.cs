using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExtraPotions.Meadworks.Crucible {
    public sealed class NativePeerCommandClient:IDisposable {
        sealed class Pending {
            internal NativePeerCommandBinding Binding=null!;
            internal RequestEnvelope Envelope=null!;
            internal RequestIdentity Identity=null!;
            internal ProductRegistration Owner=null!;
            internal CancellationToken Cancellation;
            internal double Started,NextPoll;
            internal Action<OperationResult> Complete=null!;
            internal Func<bool> Active=null!;
        }
        readonly ProductRegistry services;
        readonly NativePeerCommandTransport transport;
        readonly Dictionary<RequestIdentity,Pending> pending=new Dictionary<RequestIdentity,Pending>();
        readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        bool disposed;
        internal NativePeerCommandClient(ProductRegistry services,NativePeerCommandTransport transport){this.services=services;this.transport=transport;transport.OnReply+=Receive;}
        // Invoke and observe from Unity Update. Continuations must not access Unity.
        public Task<OperationResult<TReply>> SendAsync<TCommand,TReply>(string nativePeerId,string commandId,string correlationId,TCommand command,CancellationToken cancellation){
            var completion=new TaskCompletionSource<OperationResult<TReply>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<OperationResult<TReply>> Reject(string code,OperationState state=OperationState.Rejected){completion.SetResult(new OperationResult<TReply>(state,code,default!,null));return completion.Task;}
            var session=services.Sessions.Current;
            if(disposed||!session.AuthorityReady||!session.IsAuthority||ZNet.instance==null||!ZNet.instance.IsServer())return Reject("AuthorityUnavailable");
            if(cancellation.IsCancellationRequested)return Reject("Canceled",OperationState.Cancelled);
            RequestIdentity identity;try{identity=new RequestIdentity(nativePeerId,session.WorldId,session.SessionId,correlationId);}catch{return Reject("InvalidIdentity");}
            if(pending.ContainsKey(identity))return Reject("AlreadyPending");
            if(pending.Count>=256||pending.Values.Count(p=>p.Identity.ActorId==nativePeerId)>=32)return Reject("PendingCapacity");
            if(!services.PeerCommands.TryGetCodec<TCommand,TReply>(commandId,out var descriptor,out var codec,out var owner,out var lease))return Reject("CommandUnavailable");
            var binding=transport.Bind(nativePeerId,owner!);if(binding==null)return Reject("PeerUnavailable");
            byte[] bytes;try{bytes=codec!.EncodeRequest(command);}catch{return Reject("MalformedCommand");}
            if(bytes==null||bytes.Length>16384)return Reject("PayloadTooLarge");
            if(!lease!.IsActive)return Reject("CommandUnavailable");
            var envelope=new RequestEnvelope(commandId,descriptor!.Protocol,session.WorldId,session.SessionId,correlationId,bytes);
            var item=new Pending{Binding=binding,Envelope=envelope,Identity=identity,Owner=owner!,Cancellation=cancellation,Started=clock.Elapsed.TotalSeconds};
            item.Active=()=>lease.IsActive;
            item.Complete=result=>{
                if(!item.Active()){completion.TrySetResult(new OperationResult<TReply>(OperationState.RecoveryRequired,"OwnerUnavailable",default!,identity));return;}
                try{completion.TrySetResult(new OperationResult<TReply>(result.State,result.Code,result.State==OperationState.Succeeded?codec!.DecodeReply(result.Reply):default!,result.Identity));}
                catch{completion.TrySetResult(new OperationResult<TReply>(OperationState.RecoveryRequired,"MalformedReply",default!,result.Identity));}
            };
            pending.Add(identity,item);
            if(binding.Rpc==null){
                var context=transport.Context(binding);var admission=context==null?null:services.PeerCommandRequests.Submit(context,envelope);
                if(admission==null||!admission.Accepted)Finish(item,new OperationResult(identity,OperationState.Rejected,admission?.Code??"SessionUnavailable"));
            }else if(!transport.Send(binding,Frame(item,PeerCommandFrameKind.Request)))Finish(item,new OperationResult(identity,OperationState.RecoveryRequired,"OutcomeUnknown"));
            return completion.Task;
        }
        static PeerCommandFrame Frame(Pending item,PeerCommandFrameKind kind)=>new PeerCommandFrame(kind,item.Envelope.WorldId,item.Envelope.SessionId,item.Identity.ActorId,item.Binding.ConnectionId,item.Envelope.RequestId,item.Envelope.OperationId,item.Envelope.Protocol,kind==PeerCommandFrameKind.Request?item.Envelope.Payload:Array.Empty<byte>());
        static bool CompleteState(OperationState state)=>state==OperationState.Succeeded||state==OperationState.Rejected||state==OperationState.Cancelled||state==OperationState.RecoveryRequired;
        void Finish(Pending item,OperationResult result){pending.Remove(item.Identity);item.Complete(result);}
        void Receive(NativePeerCommandBinding binding,PeerCommandFrame frame){
            var identity=new RequestIdentity(frame.TargetPeerId,frame.WorldId,frame.Epoch,frame.CorrelationId);
            if(!pending.TryGetValue(identity,out var item)||!ReferenceEquals(item.Binding,binding)||!transport.Current(binding)||frame.ConnectionId!=binding.ConnectionId||frame.CommandId!=item.Envelope.OperationId||frame.Protocol!=item.Envelope.Protocol)return;
            if(!item.Active()){Finish(item,new OperationResult(identity,OperationState.RecoveryRequired,"OwnerUnavailable"));return;}
            try{var result=frame.ReadReply();if(CompleteState(result.State))Finish(item,result);}catch{Finish(item,new OperationResult(identity,OperationState.RecoveryRequired,"MalformedReply"));}
        }
        internal void Update(){
            if(pending.Count==0)return;
            foreach(var item in pending.Values.ToArray()){
                if(!item.Active()||!transport.Current(item.Binding)){Finish(item,new OperationResult(item.Identity,OperationState.RecoveryRequired,"ConnectionChanged"));continue;}
                if(item.Cancellation.IsCancellationRequested||clock.Elapsed.TotalSeconds-item.Started>=10){
                    if(item.Binding.Rpc==null){services.PeerCommandRequests.Cancel(item.Identity);Finish(item,services.PeerCommandRequests.Status(item.Identity));}
                    else {transport.Send(item.Binding,Frame(item,PeerCommandFrameKind.Cancel));Finish(item,new OperationResult(item.Identity,OperationState.RecoveryRequired,"OutcomeUnknown"));}
                    continue;
                }
                if(item.Binding.Rpc==null){var result=services.PeerCommandRequests.Status(item.Identity);if(CompleteState(result.State))Finish(item,result);}
                else if(clock.Elapsed.TotalSeconds>=item.NextPoll){item.NextPoll=clock.Elapsed.TotalSeconds+.25;transport.Send(item.Binding,Frame(item,PeerCommandFrameKind.Status));}
            }
        }
        public void Dispose(){if(disposed)return;disposed=true;transport.OnReply-=Receive;foreach(var item in pending.Values.ToArray())Finish(item,new OperationResult(item.Identity,OperationState.RecoveryRequired,"Shutdown"));}
    }
}
