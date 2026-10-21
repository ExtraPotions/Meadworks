using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace ExtraPotions.Meadworks.Crucible {
    public sealed class NativeRequestClient:IDisposable {
        sealed class Pending {internal RequestEnvelope Envelope=null!;internal RequestIdentity Identity=null!;internal ProductRegistration Owner=null!;internal long Generation;internal CancellationToken Cancellation;internal double Started,NextPoll;internal Action<OperationResult> Complete=null!;}
        readonly ProductRegistry services;readonly NativeSessionAdapter sessions;readonly NativeRequestTransport transport;readonly Dictionary<string,Pending> pending=new Dictionary<string,Pending>();readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();bool disposed;
        internal NativeRequestClient(ProductRegistry services,NativeSessionAdapter sessions,NativeRequestTransport transport){this.services=services;this.sessions=sessions;this.transport=transport;transport.OnReply+=Receive;}
        // Call and poll from Update. No gameplay is performed in task continuations.
        public Task<OperationResult<TReply>> InvokeAsync<TRequest,TReply>(string operationId,TRequest request,CancellationToken cancellation){
            var completion=new TaskCompletionSource<OperationResult<TReply>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<OperationResult<TReply>> Reject(string code,OperationState state=OperationState.Rejected){completion.SetResult(new OperationResult<TReply>(state,code,default!,null));return completion.Task;}
            var session=services.Sessions.Current;var actor=NativeSessionAdapter.LocalActor();
            if(disposed||!session.AuthorityReady||actor==null)return Reject("SessionUnavailable");if(cancellation.IsCancellationRequested)return Reject("Canceled",OperationState.Cancelled);if(pending.Count>=32)return Reject("PendingCapacity");
            if(!services.Operations.TryGetCodec<TRequest,TReply>(operationId,out var descriptor,out var codec,out var owner))return Reject("OperationUnavailable");
            byte[] bytes;try{bytes=codec!.EncodeRequest(request);}catch{return Reject("MalformedRequest");}if(bytes==null||bytes.Length>16384)return Reject("PayloadTooLarge");
            var requestId=System.Guid.NewGuid().ToString("N");var identity=new RequestIdentity(actor,session.WorldId,session.SessionId,requestId);var envelope=new RequestEnvelope(operationId,descriptor!.Protocol,session.WorldId,session.SessionId,requestId,bytes);
            var item=new Pending{Envelope=envelope,Identity=identity,Owner=owner!,Generation=session.Generation,Cancellation=cancellation,Started=clock.Elapsed.TotalSeconds};
            item.Complete=result=>{try{completion.TrySetResult(new OperationResult<TReply>(result.State,result.Code,result.State==OperationState.Succeeded?codec!.DecodeReply(result.Reply):default!,result.Identity));}catch{completion.TrySetResult(new OperationResult<TReply>(OperationState.Rejected,"MalformedReply",default!,result.Identity));}};
            pending.Add(requestId,item);
            if(session.IsAuthority){var context=sessions.Context(null);var admission=context==null?null:services.Requests.Submit(context,envelope);if(admission==null||!admission.Accepted)Finish(item,new OperationResult(identity,OperationState.Rejected,admission?.Code??"SessionUnavailable"));}
            else if(!transport.SendServer(Frame(item,FrameKind.Request)))Finish(item,new OperationResult(identity,OperationState.Rejected,"SessionUnavailable"));
            return completion.Task;
        }
        static ProtocolFrame Frame(Pending item,FrameKind kind)=>new ProtocolFrame(kind,item.Envelope.WorldId,item.Envelope.SessionId,item.Envelope.RequestId,item.Envelope.OperationId,kind==FrameKind.Request?item.Envelope.Payload:Array.Empty<byte>());
        static bool CompleteState(OperationState state)=>state==OperationState.Succeeded||state==OperationState.Rejected||state==OperationState.Cancelled||state==OperationState.RecoveryRequired;
        void Finish(Pending item,OperationResult result){pending.Remove(item.Envelope.RequestId);item.Complete(result);}
        void Receive(ProtocolFrame frame){if(pending.TryGetValue(frame.RequestId,out var item)&&frame.OperationId==item.Envelope.OperationId&&item.Generation==services.Sessions.Current.Generation){if(!item.Owner.IsActive){Finish(item,new OperationResult(item.Identity,OperationState.RecoveryRequired,"OwnerUnavailable"));return;}var result=PeerProtocol.ReadReply(frame,item.Identity.ActorId);if(result.Identity.Equals(item.Identity)&&CompleteState(result.State))Finish(item,result);}}
        internal void Update(){if(pending.Count==0)return;var session=services.Sessions.Current;foreach(var item in pending.Values.ToArray()){
            if(!item.Owner.IsActive||item.Generation!=session.Generation||!session.AuthorityReady){Finish(item,new OperationResult(item.Identity,OperationState.RecoveryRequired,"SessionChanged"));continue;}
            if(item.Cancellation.IsCancellationRequested||clock.Elapsed.TotalSeconds-item.Started>=10){
                if(session.IsAuthority){services.Requests.Cancel(item.Identity);Finish(item,services.Requests.Status(item.Identity));}
                else {transport.SendServer(Frame(item,FrameKind.Cancel));Finish(item,new OperationResult(item.Identity,OperationState.RecoveryRequired,"OutcomeUnknown"));}continue;
            }
            if(session.IsAuthority){var result=services.Requests.Status(item.Identity);if(CompleteState(result.State))Finish(item,result);}
            else if(clock.Elapsed.TotalSeconds>=item.NextPoll){item.NextPoll=clock.Elapsed.TotalSeconds+.25;transport.SendServer(Frame(item,FrameKind.Status));}
        }}
        public void Dispose(){if(disposed)return;disposed=true;transport.OnReply-=Receive;foreach(var item in pending.Values.ToArray())Finish(item,new OperationResult(item.Identity,OperationState.RecoveryRequired,"Shutdown"));}
    }
}
