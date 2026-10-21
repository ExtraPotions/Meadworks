using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    internal sealed class NativeRequestTransport:IDisposable {
        const string RpcName="ExtraPotions.Crucible.Protocol1";
        sealed class Connection {internal PeerHandshake Handshake=new PeerHandshake(System.Guid.NewGuid().ToString("N"));internal double NextProbe;internal double Window;internal int Received;internal string Sender="";}
        readonly ProductRegistry services;readonly NativeSessionAdapter sessions;readonly RequestDispatcher dispatcher;readonly Dictionary<ZRpc,Connection> connections=new Dictionary<ZRpc,Connection>();readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        // Reused every frame by Update (no per-frame peer arrays).
        readonly List<ZRpc> live=new List<ZRpc>(),stale=new List<ZRpc>();
        readonly ServerPresenceTimer presence;ZRpc? presenceRpc;
        MeadworksLog? log;
        /// <summary>
        /// C04 peer-class handshake (feature flag <c>Network.PeerClassHandshake</c>): v2 capability frames both directions
        /// (the server's set rides on the Hello payload, which older clients ignore). Join refusal and the client's
        /// session turn-off happen earlier, at <c>ZNet.RPC_PeerInfo</c> (<see cref="NativePeerJoin"/>); the Hello only
        /// re-evaluates when no join greeting arrived.
        /// </summary>
        internal bool PeerClassHandshake;
        MeadworksLog Log=>log??=MeadworksLog.For(Plugin.Log??BepInEx.Logging.Logger.CreateLogSource("Crucible"),"Crucible");
        internal event Action<ProtocolFrame>? OnReply;
        internal NativeRequestTransport(ProductRegistry services,NativeSessionAdapter sessions,RequestDispatcher dispatcher,ServerPresenceTimer presence){this.services=services;this.sessions=sessions;this.dispatcher=dispatcher;this.presence=presence;}
        internal void Update(){
            var net=ZNet.instance;live.Clear();if(net!=null)foreach(var peer in net.GetPeers()){var rpc=peer.m_rpc;if(rpc!=null&&rpc.IsConnected())live.Add(rpc);}
            if(connections.Count>0){
                stale.Clear();
                foreach(var rpc in connections.Keys)if(!live.Contains(rpc))stale.Add(rpc);
                foreach(var rpc in stale){rpc.Unregister(RpcName);services.Sessions.Peers.RemovePeer(connections[rpc].Sender);connections.Remove(rpc);}
            }
            foreach(var rpc in live)if(!connections.ContainsKey(rpc)){connections.Add(rpc,new Connection());rpc.Register<ZPackage>(RpcName,Receive);}
            TrackPresence(net);
            if(net!=null&&!net.IsServer()){var rpc=net.GetServerRPC();if(rpc!=null&&connections.TryGetValue(rpc,out var state)&&state.Handshake.Epoch.Length==0&&clock.Elapsed.TotalSeconds>=state.NextProbe){state.NextProbe=clock.Elapsed.TotalSeconds+1;Send(rpc,new ProtocolFrame(FrameKind.Probe,0,"",state.Handshake.Challenge,"",Array.Empty<byte>()));}}
        }
        /// <summary>
        /// C02 vanilla-server timeout: the wait starts once this client is in a world on a server connection (role
        /// RemoteClient) and ends with the server's Hello; no Hello within the timeout reports Absent.
        /// </summary>
        void TrackPresence(ZNet? net){
            var rpc=net!=null&&!net.IsServer()&&services.Sessions.Current.Role==SessionRole.RemoteClient?net.GetServerRPC():null;
            if(rpc==null||!rpc.IsConnected()){if(presenceRpc!=null){presenceRpc=null;presence.Disconnect();MeadworksAuthority.ClearServerProducts();}return;}
            if(!ReferenceEquals(presenceRpc,rpc)){presenceRpc=rpc;presence.Disconnect();MeadworksAuthority.ClearServerProducts();NativePeerJoin.ApplyServerStates(rpc);if(connections.TryGetValue(rpc,out var known)&&known.Handshake.Epoch.Length>0)presence.Hello();else presence.Connect(clock.Elapsed.TotalSeconds);}
            presence.Tick(clock.Elapsed.TotalSeconds);
        }
        void Receive(ZRpc rpc,ZPackage package){
            if(package.Size()>65536||!connections.TryGetValue(rpc,out var state))return;
            var now=clock.Elapsed.TotalSeconds;if(now-state.Window>=1){state.Window=now;state.Received=0;}if(++state.Received>128)return;
            try{var frame=ProtocolFrame.Decode(package.GetArray());dispatcher.Enqueue(()=>Process(rpc,state,frame));}catch{ }
        }
        void Process(ZRpc rpc,Connection state,ProtocolFrame frame){
            if(!rpc.IsConnected()||!connections.TryGetValue(rpc,out var current)||!ReferenceEquals(state,current))return;
            var net=ZNet.instance;if(net==null)return;var snapshot=services.Sessions.Current;
            if(!net.IsServer()){
                if(!ReferenceEquals(net.GetServerRPC(),rpc))return;
                if(frame.Kind==FrameKind.Hello){if(sessions.AcceptServer(rpc,state.Handshake,frame)){if(ReferenceEquals(presenceRpc,rpc))presence.Hello();sessions.Update();var adopted=services.Sessions.Current;
                    // v2 only when the server's Hello carried its v2 set, so a server without the flag keeps reading v1.
                    var v2=PeerClassHandshake&&frame.Payload.Length>0;
                    Send(rpc,new ProtocolFrame(FrameKind.Capabilities,adopted.WorldId,adopted.SessionId,"","",v2?PeerProtocol.CapabilitiesV2(services):PeerProtocol.Capabilities(services)));if(v2&&NativePeerJoin.VerdictFor(rpc)==null)EvaluateServer(frame.Payload);}return;}
                if(frame.Kind==FrameKind.Reply&&snapshot.AuthorityReady&&frame.WorldId==snapshot.WorldId&&frame.Epoch==snapshot.SessionId)OnReply?.Invoke(frame);return;
            }
            var peer=net.GetPeers().FirstOrDefault(p=>ReferenceEquals(p.m_rpc,rpc));if(peer==null||!peer.IsReady())return;NativePeerJoin.MarkCrucible(peer);if(!snapshot.AuthorityReady)return;
            if(frame.Kind==FrameKind.Probe){Send(rpc,new ProtocolFrame(FrameKind.Hello,snapshot.WorldId,snapshot.SessionId,frame.RequestId,"",PeerClassHandshake?PeerProtocol.CapabilitiesV2(services):Array.Empty<byte>()));return;}
            if(frame.WorldId!=snapshot.WorldId||frame.Epoch!=snapshot.SessionId)return;
            var sender=peer.m_uid.ToString(CultureInfo.InvariantCulture);
            state.Sender=sender;
            if(frame.Kind==FrameKind.Capabilities){
                if(!PeerClassHandshake){foreach(var capability in PeerProtocol.ReadCapabilities(frame.Payload))services.Sessions.Peers.RecordVerified(snapshot,sender,capability.Item1,capability.Item2);return;}
                // Refusal already happened at join (NativePeerJoin); here the set is only recorded.
                var set=CapabilityCodec.Decode(frame.Payload);
                foreach(var product in set.Products)services.Sessions.Peers.RecordVerified(snapshot,sender,product.Id,PeerProtocol.Version);
                return;
            }
            var context=sessions.Context(rpc);if(context==null)return;var id=new RequestIdentity(context.ActorId,frame.WorldId,frame.Epoch,frame.RequestId);
            if(frame.Kind==FrameKind.Request){var admission=services.Requests.Submit(context,new RequestEnvelope(frame.OperationId,PeerProtocol.Version,frame.WorldId,frame.Epoch,frame.RequestId,frame.Payload));Reply(rpc,frame,admission.Result??new OperationResult(id,OperationState.Rejected,admission.Code));}
            else if(frame.Kind==FrameKind.Status)Reply(rpc,frame,services.Requests.Status(id));
            else if(frame.Kind==FrameKind.Cancel){services.Requests.Cancel(id);Reply(rpc,frame,services.Requests.Status(id));}
        }
        /// <summary>Client: compares the server's set with ours, turns off required products the server lacks, says why.</summary>
        void EvaluateServer(byte[] payload){
            CapabilitySet set;
            try{set=CapabilityCodec.Decode(payload);}
            catch(NotSupportedException e){Log.Warning(e.Message);return;}
            var local=PeerProtocol.LocalProducts(services);var verdict=PeerClassPolicy.Evaluate(set.Products,local);
            MeadworksAuthority.SetServerProducts(local.Select(p=>new KeyValuePair<string,ServerProduct>(p.Id,verdict.ServerStateOf(p.Id))));
            foreach(var notice in verdict.Notices)Log.Warning(notice);
            if(!verdict.Admit)foreach(var line in verdict.RefusalLines)Log.Error("The server refuses this connection: "+line);
            MeadworksRefusals.Record(verdict);
        }
        void Reply(ZRpc rpc,ProtocolFrame request,OperationResult result)=>Send(rpc,new ProtocolFrame(FrameKind.Reply,result.Identity.WorldId,result.Identity.SessionId,result.Identity.RequestId,request.OperationId,PeerProtocol.Reply(result)));
        internal bool SendServer(ProtocolFrame frame){var rpc=ZNet.instance?.GetServerRPC();if(rpc==null||!rpc.IsConnected()||!connections.ContainsKey(rpc))return false;Send(rpc,frame);return true;}
        static void Send(ZRpc rpc,ProtocolFrame frame)=>rpc.Invoke(RpcName,new object[]{new ZPackage(frame.Encode())});
        public void Dispose(){foreach(var rpc in connections.Keys)rpc.Unregister(RpcName);connections.Clear();OnReply=null;}
    }
}
