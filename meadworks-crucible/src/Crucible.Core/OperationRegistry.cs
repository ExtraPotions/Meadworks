using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    internal sealed class ExecutionFrame {
        public OperationState State;public byte[] Reply=Array.Empty<byte>();public string Code="";public Func<ExecutionFrame>? Poll;
    }
    internal abstract class OperationEntry {
        internal ProductRegistration Owner=null!;internal OperationDescriptor Descriptor=null!;internal bool Active=true;internal Action? Cleanup;
        internal abstract ExecutionFrame Execute(AuthorityContext context,byte[] payload);
        internal abstract ExecutionFrame? Recover(RequestIdentity identity);
        internal abstract object Codec{get;}
    }
    public sealed class OperationRegistry:IDisposable {
        readonly Dictionary<string,OperationEntry> entries=new Dictionary<string,OperationEntry>(StringComparer.Ordinal);bool disposed;
        /// <summary>
        /// The one protocol version a remote client's request reaches the router at (the native transport submits every remote
        /// request at its fixed protocol; the wire frame carries no version). When set, <see cref="Register{TRequest,TReply}"/>
        /// refuses an operation declared at any other version, because a client could never reach it. Null (the default for a
        /// bare registry) checks nothing; the Crucible plugin sets it to the transport version before any product registers.
        /// </summary>
        public Version? RemoteProtocol{get;set;}
        public IDisposable Register<TRequest,TReply>(ProductRegistration owner,OperationDescriptor descriptor,IOperationCodec<TRequest,TReply> codec,Func<AuthorityContext,TRequest,OperationExecution<TReply>> handler,Func<RequestIdentity,OperationExecution<TReply>>? recovery=null) {
            if(disposed)throw new ObjectDisposedException(nameof(OperationRegistry));if(owner==null||descriptor==null||codec==null||handler==null)throw new ArgumentNullException();owner.EnsureActive();
            if(RemoteProtocol!=null&&descriptor.Protocol!=RemoteProtocol)throw new InvalidOperationException("Operation "+descriptor.Id+" of product "+owner.Metadata.Id+" is declared at protocol "+descriptor.Protocol+", but remote requests reach the router at "+RemoteProtocol+" only, so no client could call it. Declare it at "+RemoteProtocol+".");
            if(entries.ContainsKey(descriptor.Id))throw new InvalidOperationException("Operation already registered.");
            if(entries.Count>=256)throw new InvalidOperationException("Operation registry capacity reached.");
            var entry=new TypedEntry<TRequest,TReply>(codec,handler,recovery){Owner=owner,Descriptor=descriptor};
            Action cleanup=()=>Remove(entry);entry.Cleanup=cleanup;owner.Disposed+=cleanup;entries.Add(descriptor.Id,entry);
            return new OwnedDisposal(cleanup);
        }
        internal OperationEntry? Find(string operation,Version protocol)=>!disposed&&entries.TryGetValue(operation,out var entry)&&entry.Active&&entry.Descriptor.Protocol==protocol?entry:null;
        public IReadOnlyList<OperationDescriptor> Snapshot()=>Array.AsReadOnly(entries.Values.Where(e=>e.Active).Select(e=>e.Descriptor).OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray());
        public bool TryGetCodec<TRequest,TReply>(string operation,out OperationDescriptor? descriptor,out IOperationCodec<TRequest,TReply>? codec,out ProductRegistration? owner){
            descriptor=null;codec=null;owner=null;
            if(disposed||!entries.TryGetValue(operation,out var entry)||!entry.Active||!(entry.Codec is IOperationCodec<TRequest,TReply> typed))return false;
            descriptor=entry.Descriptor;codec=typed;owner=entry.Owner;return true;
        }
        void Remove(OperationEntry entry){if(!entry.Active)return;entry.Active=false;if(entry.Cleanup!=null)entry.Owner.Disposed-=entry.Cleanup;if(entries.TryGetValue(entry.Descriptor.Id,out var current)&&ReferenceEquals(entry,current))entries.Remove(entry.Descriptor.Id);}
        public void Dispose(){if(disposed)return;disposed=true;foreach(var entry in new List<OperationEntry>(entries.Values))Remove(entry);}
        sealed class TypedEntry<TRequest,TReply>:OperationEntry {
            readonly IOperationCodec<TRequest,TReply> codec;readonly Func<AuthorityContext,TRequest,OperationExecution<TReply>> handler;readonly Func<RequestIdentity,OperationExecution<TReply>>? recovery;
            internal override object Codec=>codec;
            internal TypedEntry(IOperationCodec<TRequest,TReply> codec,Func<AuthorityContext,TRequest,OperationExecution<TReply>> handler,Func<RequestIdentity,OperationExecution<TReply>>? recovery){this.codec=codec;this.handler=handler;this.recovery=recovery;}
            ExecutionFrame Frame(OperationExecution<TReply> result){if(result==null)throw new InvalidOperationException("Missing operation result.");var bytes=result.State==OperationState.Succeeded?codec.EncodeReply(result.Reply):Array.Empty<byte>();if(bytes==null||bytes.Length>16384)throw new InvalidOperationException("Operation reply exceeds bounds.");return new ExecutionFrame{State=result.State,Reply=(byte[])bytes.Clone(),Code=SafeCode(result.Code),Poll=result.Next==null?null:new Func<ExecutionFrame>(()=>Frame(result.Next()))};}
            static string SafeCode(string code)=>string.IsNullOrWhiteSpace(code)||code.Length>64||code.IndexOfAny(new[]{'\r','\n','\0'})>=0?"Rejected":code;
            internal override ExecutionFrame Execute(AuthorityContext context,byte[] payload)=>Frame(handler(context,codec.DecodeRequest(payload)));
            internal override ExecutionFrame? Recover(RequestIdentity identity)=>recovery==null?null:Frame(recovery(identity));
        }
    }
    internal sealed class OwnedDisposal:IDisposable {Action? action;internal OwnedDisposal(Action action){this.action=action;}public void Dispose(){var current=action;action=null;current?.Invoke();}}
}
