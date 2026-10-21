using System;

namespace ExtraPotions.Meadworks.Crucible {
    public enum OperationState { Queued, Pending, Succeeded, Rejected, Cancelled, RecoveryRequired }
    public sealed class RequestIdentity:IEquatable<RequestIdentity> {
        public string ActorId{get;} public long WorldId{get;} public string SessionId{get;} public string RequestId{get;}
        public RequestIdentity(string actor,long world,string session,string request){IdentityText(actor);IdentityText(session);IdentityText(request);if(world==0)throw new ArgumentException("Missing world identity.");ActorId=actor;WorldId=world;SessionId=session;RequestId=request;}
        internal static void IdentityText(string value){if(string.IsNullOrWhiteSpace(value)||value.Length>128||value.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new ArgumentException("Invalid correlation identity.");}
        public bool Equals(RequestIdentity? other)=>other!=null&&ActorId==other.ActorId&&WorldId==other.WorldId&&SessionId==other.SessionId&&RequestId==other.RequestId;
        public override bool Equals(object? obj)=>obj is RequestIdentity id&&Equals(id);
        public override int GetHashCode(){unchecked{return (((ActorId.GetHashCode()*397)^WorldId.GetHashCode())*397^SessionId.GetHashCode())*397^RequestId.GetHashCode();}}
    }
    public sealed class RequestEnvelope {
        readonly byte[] payload;public string OperationId{get;} public Version Protocol{get;} public long WorldId{get;} public string SessionId{get;} public string RequestId{get;}
        public byte[] Payload=>(byte[])payload.Clone();public int PayloadLength=>payload.Length;
        public RequestEnvelope(string operation,Version protocol,long world,string session,string request,byte[] payload){Validation.Id(operation);Validation.Version(protocol);RequestIdentity.IdentityText(session);RequestIdentity.IdentityText(request);OperationId=operation;Protocol=protocol;WorldId=world;SessionId=session;RequestId=request;this.payload=(byte[])(payload??throw new ArgumentNullException(nameof(payload))).Clone();}
        internal bool Same(RequestEnvelope other)=>OperationId==other.OperationId&&Protocol==other.Protocol&&WorldId==other.WorldId&&SessionId==other.SessionId&&RequestId==other.RequestId&&SameBytes(payload,other.payload);
        static bool SameBytes(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    }
    public sealed class AuthorityContext {
        public SessionSnapshot Session{get;}public string SenderId{get;}public string ActorId{get;}public bool IsAdmin{get;}public bool IsLocal{get;}
        public RequestIdentity Identity{get;private set;}=null!;public bool DecisionMade{get;private set;}
        public AuthorityContext(SessionSnapshot session,string sender,string actor,bool admin,bool local=false){Session=session??throw new ArgumentNullException(nameof(session));RequestIdentity.IdentityText(sender);RequestIdentity.IdentityText(actor);SenderId=sender;ActorId=actor;IsAdmin=admin;IsLocal=local;}
        internal AuthorityContext ForRequest(RequestIdentity id)=>new AuthorityContext(Session,SenderId,ActorId,IsAdmin,IsLocal){Identity=id};
        public void MarkDecisionMade(){if(Identity==null)throw new InvalidOperationException("No dispatched request.");DecisionMade=true;}
    }
    public sealed class OperationDescriptor {
        public string Id{get;}public Version Protocol{get;}
        public OperationDescriptor(string id,Version protocol){Validation.Id(id);if(!id.StartsWith("meadworks.",StringComparison.Ordinal))throw new ArgumentException("Operation requires Meadworks namespace.");Validation.Version(protocol);Id=id;Protocol=protocol;}
    }
    public interface IOperationCodec<TRequest,TReply>{byte[] EncodeRequest(TRequest request);TRequest DecodeRequest(byte[] bytes);byte[] EncodeReply(TReply reply);TReply DecodeReply(byte[] bytes);}
    public sealed class OperationExecution<T> {
        public OperationState State{get;}public T Reply{get;}public string Code{get;}internal Func<OperationExecution<T>>? Next{get;}
        OperationExecution(OperationState state,T reply,string code,Func<OperationExecution<T>>? next=null){State=state;Reply=reply;Code=code;Next=next;}
        public static OperationExecution<T> Success(T reply)=>new OperationExecution<T>(OperationState.Succeeded,reply,"Success");
        public static OperationExecution<T> Rejected(string code)=>new OperationExecution<T>(OperationState.Rejected,default!,code);
        public static OperationExecution<T> Pending(Func<OperationExecution<T>>? poll)=>new OperationExecution<T>(OperationState.Pending,default!,"Pending",poll);
        public static OperationExecution<T> RecoveryRequired()=>new OperationExecution<T>(OperationState.RecoveryRequired,default!,"RecoveryRequired");
    }
    public sealed class OperationResult {
        readonly byte[] reply;public RequestIdentity Identity{get;}public OperationState State{get;}public string Code{get;}public byte[] Reply=>(byte[])reply.Clone();
        public OperationResult(RequestIdentity id,OperationState state,string code,byte[]? reply=null){Identity=id??throw new ArgumentNullException(nameof(id));State=state;Code=code;this.reply=reply==null?Array.Empty<byte>():(byte[])reply.Clone();}
    }
    public sealed class OperationResult<T> {
        public OperationState State{get;}public string Code{get;}public T Reply{get;}public RequestIdentity? Identity{get;}
        public OperationResult(OperationState state,string code,T reply,RequestIdentity? identity){State=state;Code=code;Reply=reply;Identity=identity;}
    }
    public sealed class RequestAdmission {
        public bool Accepted{get;}public string Code{get;}public OperationResult? Result{get;}
        internal RequestAdmission(bool accepted,string code,OperationResult? result=null){Accepted=accepted;Code=code;Result=result;}
    }
    public sealed class RequestDiagnostics {
        public int Pending{get;}public int Terminal{get;}public int Recovering{get;}
        internal RequestDiagnostics(int pending,int terminal,int recovering){Pending=pending;Terminal=terminal;Recovering=recovering;}
        public override string ToString()=>$"Pending={Pending}; Terminal={Terminal}; Recovering={Recovering}";
    }
}
