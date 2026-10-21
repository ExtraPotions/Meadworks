using System;
using System.Globalization;
using System.Linq;
using Jotunn.Managers;
using Jotunn.Utils;
namespace ExtraPotions.Meadworks.Crucible {
    internal sealed class NativeSessionAdapter:IDisposable {
        readonly ProductRegistry services;ZNet? network;ZRpc? server;bool synchronized;string epoch="";long authoritativeWorld;
        int synchronizationEvents,initialSynchronizationEvents;bool initialNetworkReferenceCurrent,initialServerReferenceCurrent;
        internal NativeSessionAdapter(ProductRegistry services){this.services=services;SynchronizationManager.OnConfigurationSynchronized+=OnSynchronized;SynchronizationManager.OnAdminStatusChanged+=OnAdminChanged;}
        void OnSynchronized(object sender,ConfigurationSynchronizationEventArgs args){
            synchronizationEvents++;
            if(args.InitialSynchronization){
                initialSynchronizationEvents++;var net=ZNet.instance;
                initialNetworkReferenceCurrent=ReferenceEquals(network,net);initialServerReferenceCurrent=ReferenceEquals(server,net?.GetServerRPC());
                // Jotunn can finish initial synchronization during the native
                // peer-info callback, before our next frame has observed its
                // server RPC. Observe that transition before setting the latch,
                // so the next frame cannot erase this connection's proof.
                Update();synchronized=true;
            }
        }
        void OnAdminChanged(){ /* Admin facts are refreshed in the next Update. */ }
        internal bool AcceptServer(ZRpc rpc,PeerHandshake handshake,ProtocolFrame frame){
            var net=ZNet.instance;if(net==null||net.IsServer()||!ReferenceEquals(net.GetServerRPC(),rpc)||!rpc.IsConnected())return false;
            if(!handshake.Accept(true,frame.RequestId,frame.WorldId,frame.Epoch))return false;epoch=handshake.Epoch;authoritativeWorld=handshake.WorldId;return true;
        }
        internal void Update(){
            var net=ZNet.instance;var rpc=net!=null&&!net.IsServer()?net.GetServerRPC():null;
            if(!ReferenceEquals(network,net)||!ReferenceEquals(server,rpc)){network=net;server=rpc;synchronized=false;epoch="";authoritativeWorld=0;}
            var previous=services.Sessions.Current;
            var nativeWorld=net==null||net.GetWorld()==null?0:net.GetWorldUID();
            var world=net!=null&&!net.IsServer()&&authoritativeWorld!=0?authoritativeWorld:nativeWorld;
            bool connected=net!=null&&(net.IsServer()||rpc!=null&&rpc.IsConnected());
            var facts=new SessionFacts(world!=0,connected,net!=null&&net.IsServer(),net!=null&&net.IsDedicated(),net==null?0:net.GetPeers().Count,world,net!=null&&(net.IsServer()||synchronized),net!=null&&(net.IsServer()&&!net.IsDedicated()?net.LocalPlayerIsAdminOrHost():SynchronizationManager.Instance.PlayerIsAdmin),epoch,net!=null&&net.IsServer()&&ZNet.IsOpenServer());
            var next=services.Sessions.Update(facts);if(next.Generation!=previous.Generation)services.Requests.ResetSession(next);
        }
        internal static string? LocalActor(){var player=Player.m_localPlayer;return player==null||player.GetPlayerID()==0?null:player.GetPlayerID().ToString(CultureInfo.InvariantCulture);}
        internal static string? PeerActor(ZNetPeer peer){
            if(!peer.IsReady()||!peer.m_rpc.IsConnected())return null;
            var character=ZDOMan.instance?.GetZDO(peer.m_characterID);
            if(character==null||character.GetOwner()!=peer.m_uid)return null;
            var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(character.GetPrefab()):null;
            if(!prefab||!prefab.GetComponent<Player>())return null;
            var playerId=character.GetLong(ZDOVars.s_playerID,0);
            if(playerId==0||peer.m_playerID!=0&&peer.m_playerID!=playerId)return null;
            return playerId.ToString(CultureInfo.InvariantCulture);
        }
        internal bool Validate(AuthorityContext context){
            if(context.IsLocal){var live=Context(null);return live!=null&&live.ActorId==context.ActorId&&live.IsAdmin==context.IsAdmin;}
            var net=ZNet.instance;var peer=net?.GetPeers().FirstOrDefault(p=>p.m_uid.ToString(CultureInfo.InvariantCulture)==context.SenderId);var current=peer==null?null:Context(peer.m_rpc);
            return current!=null&&current.ActorId==context.ActorId&&current.IsAdmin==context.IsAdmin;
        }
        internal AuthorityContext? Context(ZRpc? rpc){
            var snapshot=services.Sessions.Current;var net=ZNet.instance;if(net==null||!snapshot.IsAuthority||!snapshot.AuthorityReady)return null;
            if(rpc==null){var actor=LocalActor();return actor==null?null:new AuthorityContext(snapshot,"local",actor,net.LocalPlayerIsAdminOrHost(),true);}
            var peer=net.GetPeers().FirstOrDefault(p=>ReferenceEquals(p.m_rpc,rpc));var actorId=peer==null?null:PeerActor(peer);if(actorId==null)return null;
            return new AuthorityContext(snapshot,peer!.m_uid.ToString(CultureInfo.InvariantCulture),actorId,net.IsAdmin(rpc.GetSocket().GetHostName()));
        }
        public void Dispose(){SynchronizationManager.OnConfigurationSynchronized-=OnSynchronized;SynchronizationManager.OnAdminStatusChanged-=OnAdminChanged;}
    }
}
