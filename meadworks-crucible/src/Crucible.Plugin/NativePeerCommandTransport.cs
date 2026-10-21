using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    internal sealed class NativePeerCommandBinding {
        internal ZRpc? Rpc;
        internal long Generation;
        internal string PeerId="",ConnectionId="",Epoch="";
        internal long WorldId;
        internal bool Bound;
        internal double NextBind,Window;
        internal int Received;
    }
    internal sealed class NativePeerCommandTransport:IDisposable {
        internal const string RpcName="ExtraPotions.Crucible.OwnerProtocol1";
        static readonly System.Version WireVersion=new System.Version(1,0);
        readonly ProductRegistry services;
        readonly RequestDispatcher dispatcher;
        readonly Dictionary<ZRpc,NativePeerCommandBinding> connections=new Dictionary<ZRpc,NativePeerCommandBinding>();
        // Reused every frame by Update (no per-frame peer arrays).
        readonly List<ZRpc> live=new List<ZRpc>(),stale=new List<ZRpc>();
        readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        NativePeerCommandBinding? local;
        long generation=-1;
        internal event Action<NativePeerCommandBinding,PeerCommandFrame>? OnReply;
        internal NativePeerCommandTransport(ProductRegistry services,RequestDispatcher dispatcher){this.services=services;this.dispatcher=dispatcher;}
        internal static string LocalPeerId=>ZNet.GetUID().ToString(CultureInfo.InvariantCulture);
        internal void Update(){
            var snapshot=services.Sessions.Current;var net=ZNet.instance;
            if(generation!=snapshot.Generation){Clear();generation=snapshot.Generation;}
            live.Clear();if(net!=null)foreach(var peer in net.GetPeers()){var rpc=peer.m_rpc;if(rpc!=null&&rpc.IsConnected())live.Add(rpc);}
            if(connections.Count>0){
                stale.Clear();
                foreach(var rpc in connections.Keys)if(!live.Contains(rpc))stale.Add(rpc);
                foreach(var rpc in stale){rpc.Unregister(RpcName);connections.Remove(rpc);}
            }
            foreach(var rpc in live)if(!connections.ContainsKey(rpc)){connections.Add(rpc,new NativePeerCommandBinding{Rpc=rpc,Generation=snapshot.Generation});rpc.Register<ZPackage>(RpcName,Receive);}
            if(net==null||!snapshot.AuthorityReady||net.IsServer())return;
            var server=net.GetServerRPC();
            if(server==null||!connections.TryGetValue(server,out var state))return;
            if(state.ConnectionId.Length==0){state.ConnectionId=Guid.NewGuid().ToString("N");state.PeerId=LocalPeerId;state.WorldId=snapshot.WorldId;state.Epoch=snapshot.SessionId;}
            if(!state.Bound&&clock.Elapsed.TotalSeconds>=state.NextBind){state.NextBind=clock.Elapsed.TotalSeconds+1;SendRaw(server,new PeerCommandFrame(PeerCommandFrameKind.Bind,state.WorldId,state.Epoch,state.PeerId,state.ConnectionId,"","",WireVersion,Array.Empty<byte>()));}
        }
        void Receive(ZRpc rpc,ZPackage package){
            if(package.Size()>65536||!connections.TryGetValue(rpc,out var state))return;
            var now=clock.Elapsed.TotalSeconds;if(now-state.Window>=1){state.Window=now;state.Received=0;}if(++state.Received>128)return;
            try{var frame=PeerCommandFrame.Decode(package.GetArray());dispatcher.Enqueue(()=>Process(rpc,state,frame));}catch{ }
        }
        static bool Matches(NativePeerCommandBinding state,PeerCommandFrame frame)=>state.PeerId==frame.TargetPeerId&&state.ConnectionId==frame.ConnectionId&&state.WorldId==frame.WorldId&&state.Epoch==frame.Epoch;
        void Process(ZRpc rpc,NativePeerCommandBinding state,PeerCommandFrame frame){
            var net=ZNet.instance;var snapshot=services.Sessions.Current;
            if(net==null||!snapshot.AuthorityReady||state.Generation!=snapshot.Generation||!rpc.IsConnected()||!connections.TryGetValue(rpc,out var current)||!ReferenceEquals(state,current)||frame.WorldId!=snapshot.WorldId||frame.Epoch!=snapshot.SessionId)return;
            if(net.IsServer()){
                var peer=net.GetPeers().FirstOrDefault(p=>ReferenceEquals(p.m_rpc,rpc));
                if(peer==null||!peer.IsReady()||frame.TargetPeerId!=peer.m_uid.ToString(CultureInfo.InvariantCulture)||!services.Sessions.Peers.Resolve(frame.TargetPeerId,Plugin.Guid,WireVersion))return;
                if(frame.Kind==PeerCommandFrameKind.Bind){
                    if(frame.Protocol!=WireVersion||state.Bound&&!Matches(state,frame))return;
                    state.PeerId=frame.TargetPeerId;state.ConnectionId=frame.ConnectionId;state.WorldId=frame.WorldId;state.Epoch=frame.Epoch;state.Bound=true;
                    SendRaw(rpc,new PeerCommandFrame(PeerCommandFrameKind.Bound,state.WorldId,state.Epoch,state.PeerId,state.ConnectionId,"","",WireVersion,Array.Empty<byte>()));return;
                }
                if(state.Bound&&Matches(state,frame)&&frame.Kind==PeerCommandFrameKind.Reply)OnReply?.Invoke(state,frame);
                return;
            }
            if(!ReferenceEquals(net.GetServerRPC(),rpc)||!Matches(state,frame)||frame.TargetPeerId!=LocalPeerId)return;
            if(frame.Kind==PeerCommandFrameKind.Bound){if(frame.Protocol==WireVersion)state.Bound=true;return;}
            if(!state.Bound||frame.Kind!=PeerCommandFrameKind.Request&&frame.Kind!=PeerCommandFrameKind.Status&&frame.Kind!=PeerCommandFrameKind.Cancel)return;
            var context=Context(state);if(context==null)return;
            var identity=new RequestIdentity(context.LocalPeerId,frame.WorldId,frame.Epoch,frame.CorrelationId);
            OperationResult result;
            if(frame.Kind==PeerCommandFrameKind.Request){
                var admission=services.PeerCommandRequests.Submit(context,new RequestEnvelope(frame.CommandId,frame.Protocol,frame.WorldId,frame.Epoch,frame.CorrelationId,frame.Payload));
                result=admission.Accepted&&admission.Result!=null?admission.Result:new OperationResult(identity,OperationState.Rejected,admission.Code);
            }else {
                result=services.PeerCommandRequests.Status(identity,frame.CommandId,frame.Protocol,frame.ConnectionId);
                if(frame.Kind==PeerCommandFrameKind.Cancel&&result.Code!="IdentityConflict"&&result.Code!="UnknownCommand"){
                    services.PeerCommandRequests.Cancel(identity);result=services.PeerCommandRequests.Status(identity,frame.CommandId,frame.Protocol,frame.ConnectionId);
                }
            }
            SendRaw(rpc,PeerCommandFrame.Reply(frame,result));
        }
        internal NativePeerCommandBinding? Bind(string peerId,ProductRegistration owner){
            var snapshot=services.Sessions.Current;var net=ZNet.instance;
            if(net==null||!net.IsServer()||!snapshot.AuthorityReady||!owner.IsActive)return null;
            if(peerId==LocalPeerId){
                if(local==null||local.Generation!=snapshot.Generation)local=new NativePeerCommandBinding{Generation=snapshot.Generation,PeerId=peerId,ConnectionId=Guid.NewGuid().ToString("N"),WorldId=snapshot.WorldId,Epoch=snapshot.SessionId,Bound=true};
                return local;
            }
            var peer=net.GetPeers().FirstOrDefault(p=>p.m_uid.ToString(CultureInfo.InvariantCulture)==peerId&&p.IsReady()&&p.m_rpc.IsConnected());
            if(peer==null||!connections.TryGetValue(peer.m_rpc,out var state)||!Current(state)||!services.Sessions.Peers.Resolve(peerId,owner.Metadata.Id,WireVersion))return null;
            return state;
        }
        internal bool Current(NativePeerCommandBinding state){
            var snapshot=services.Sessions.Current;var net=ZNet.instance;
            if(net==null||!snapshot.AuthorityReady||!state.Bound||state.Generation!=snapshot.Generation||state.WorldId!=snapshot.WorldId||state.Epoch!=snapshot.SessionId)return false;
            if(state.Rpc==null)return net.IsServer()&&ReferenceEquals(state,local)&&state.PeerId==LocalPeerId;
            if(!state.Rpc.IsConnected()||!connections.TryGetValue(state.Rpc,out var current)||!ReferenceEquals(state,current))return false;
            return net.IsServer()?net.GetPeers().Any(p=>ReferenceEquals(p.m_rpc,state.Rpc)&&p.IsReady()&&p.m_uid.ToString(CultureInfo.InvariantCulture)==state.PeerId):ReferenceEquals(net.GetServerRPC(),state.Rpc)&&state.PeerId==LocalPeerId;
        }
        internal PeerCommandContext? Context(NativePeerCommandBinding state){
            if(!Current(state))return null;
            var net=ZNet.instance;var authority=net.IsServer()?LocalPeerId:net.GetServerPeer()?.m_uid.ToString(CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(authority)?null:new PeerCommandContext(services.Sessions.Current,authority!,LocalPeerId,state.Rpc==null,true,state.ConnectionId);
        }
        internal bool Validate(PeerCommandContext context){
            if(context.LocalPeerId!=LocalPeerId)return false;
            var state=context.IsLocal?local:connections.Values.FirstOrDefault(s=>s.ConnectionId==context.ConnectionId);
            var current=state==null?null:Context(state);
            return current!=null&&current.IsLocal==context.IsLocal&&current.AuthorityPeerId==context.AuthorityPeerId&&current.ConnectionId==context.ConnectionId;
        }
        internal bool Send(NativePeerCommandBinding state,PeerCommandFrame frame){
            if(state.Rpc==null||!Current(state)||!Matches(state,frame))return false;
            try{SendRaw(state.Rpc,frame);return true;}catch{return false;}
        }
        static void SendRaw(ZRpc rpc,PeerCommandFrame frame)=>rpc.Invoke(RpcName,new object[]{new ZPackage(frame.Encode())});
        void Clear(){foreach(var rpc in connections.Keys)rpc.Unregister(RpcName);connections.Clear();local=null;}
        public void Dispose(){Clear();OnReply=null;}
    }
}
