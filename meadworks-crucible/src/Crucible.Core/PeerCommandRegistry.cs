using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    public sealed class PeerCommandLease {
        readonly Func<bool> active;
        internal PeerCommandLease(Func<bool> active){this.active=active;}
        public bool IsActive=>active();
    }
    internal abstract class PeerCommandEntry {
        internal ProductRegistration Owner=null!;internal OperationDescriptor Descriptor=null!;internal bool Active=true;internal Action? Cleanup;
        internal abstract ExecutionFrame Execute(PeerCommandContext context,byte[] payload);
        internal abstract object Codec{get;}
    }
    public sealed class PeerCommandRegistry:IDisposable {
        readonly Dictionary<string,PeerCommandEntry> entries=new Dictionary<string,PeerCommandEntry>(StringComparer.Ordinal);bool disposed;
        public IDisposable Register<TCommand,TReply>(ProductRegistration owner,OperationDescriptor descriptor,IOperationCodec<TCommand,TReply> codec,Func<PeerCommandContext,TCommand,OperationExecution<TReply>> handler){
            if(disposed)throw new ObjectDisposedException(nameof(PeerCommandRegistry));if(owner==null||descriptor==null||codec==null||handler==null)throw new ArgumentNullException();owner.EnsureActive();
            if(entries.ContainsKey(descriptor.Id))throw new InvalidOperationException("Peer command already registered.");
            if(entries.Count>=256)throw new InvalidOperationException("Peer command registry capacity reached.");
            var entry=new TypedEntry<TCommand,TReply>(codec,handler){Owner=owner,Descriptor=descriptor};
            entry.Cleanup=()=>Remove(entry);owner.Disposed+=entry.Cleanup;entries.Add(descriptor.Id,entry);return new OwnedDisposal(entry.Cleanup);
        }
        internal PeerCommandEntry? Find(string command,Version protocol)=>!disposed&&entries.TryGetValue(command,out var entry)&&entry.Active&&entry.Owner.IsActive&&entry.Descriptor.Protocol==protocol?entry:null;
        public bool TryGetCodec<TCommand,TReply>(string command,out OperationDescriptor? descriptor,out IOperationCodec<TCommand,TReply>? codec,out ProductRegistration? owner){
            return TryGetCodec(command,out descriptor,out codec,out owner,out _);
        }
        public bool TryGetCodec<TCommand,TReply>(string command,out OperationDescriptor? descriptor,out IOperationCodec<TCommand,TReply>? codec,out ProductRegistration? owner,out PeerCommandLease? lease){
            descriptor=null;codec=null;owner=null;lease=null;
            if(disposed||!entries.TryGetValue(command,out var entry)||!entry.Active||!entry.Owner.IsActive||!(entry.Codec is IOperationCodec<TCommand,TReply> typed))return false;
            descriptor=entry.Descriptor;codec=typed;owner=entry.Owner;
            lease=new PeerCommandLease(()=>!disposed&&entry.Active&&entry.Owner.IsActive&&entries.TryGetValue(command,out var current)&&ReferenceEquals(current,entry));return true;
        }
        public IReadOnlyList<OperationDescriptor> Snapshot()=>Array.AsReadOnly(entries.Values.Where(e=>e.Active&&e.Owner.IsActive).Select(e=>e.Descriptor).OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray());
        void Remove(PeerCommandEntry entry){if(!entry.Active)return;entry.Active=false;if(entry.Cleanup!=null)entry.Owner.Disposed-=entry.Cleanup;if(entries.TryGetValue(entry.Descriptor.Id,out var current)&&ReferenceEquals(current,entry))entries.Remove(entry.Descriptor.Id);}
        public void Dispose(){if(disposed)return;disposed=true;foreach(var entry in entries.Values.ToArray())Remove(entry);}
        sealed class TypedEntry<TCommand,TReply>:PeerCommandEntry {
            readonly IOperationCodec<TCommand,TReply> codec;readonly Func<PeerCommandContext,TCommand,OperationExecution<TReply>> handler;
            internal override object Codec=>codec;
            internal TypedEntry(IOperationCodec<TCommand,TReply> codec,Func<PeerCommandContext,TCommand,OperationExecution<TReply>> handler){this.codec=codec;this.handler=handler;}
            ExecutionFrame Frame(OperationExecution<TReply> result){
                if(result==null)throw new InvalidOperationException("Missing command result.");
                var bytes=result.State==OperationState.Succeeded?codec.EncodeReply(result.Reply):Array.Empty<byte>();if(bytes==null||bytes.Length>16384)throw new InvalidOperationException("Command reply exceeds bounds.");
                var code=result.Code;if(string.IsNullOrWhiteSpace(code)||code.Length>64||code.IndexOfAny(new[]{'\r','\n','\0'})>=0)code="Rejected";
                return new ExecutionFrame{State=result.State,Reply=(byte[])bytes.Clone(),Code=code,Poll=result.Next==null?null:new Func<ExecutionFrame>(()=>Frame(result.Next()))};
            }
            internal override ExecutionFrame Execute(PeerCommandContext context,byte[] payload)=>Frame(handler(context,codec.DecodeRequest(payload)));
        }
    }
}
