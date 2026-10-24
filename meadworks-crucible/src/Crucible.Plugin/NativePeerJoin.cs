using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// C04 peer-class handshake at <c>ZNet.RPC_PeerInfo</c> (Charter family-check pattern, <see cref="PeerJoin"/>):
    /// when the game accepts a peer the server greets it with its v2 product set; the client evaluates it (required
    /// products the server lacks turn off for the session, its own refusal is recorded for the Crucible.UI refusal
    /// window) and answers with its own set; the server refuses a client missing a required product or running another
    /// major.minor: it logs the refusal with a code, sends the client a <see cref="RefusalNotice"/> carrying the same
    /// code and disconnects it one second later. The greeting is installed only while <c>Network.PeerClassHandshake</c> is on;
    /// the peer-info gate (<see cref="InstallPeerInfoGate"/>) is installed on every game, independent of that flag.
    /// Main thread only.
    /// </summary>
    internal static class NativePeerJoin {
        internal const string GreetRpc="ExtraPotions.Crucible.PeerClass",RefuseRpc="ExtraPotions.Crucible.PeerRefuse",HelloRpc="ExtraPotions.Crucible.Hello";
        const double Grace=1;
        static readonly Dictionary<ZRpc,PeerClassVerdict> verdicts=new Dictionary<ZRpc,PeerClassVerdict>();
        static readonly Dictionary<ZNetPeer,double> leaving=new Dictionary<ZNetPeer,double>();
        // Server: connections that delivered Jotunn's mod list before their peer info (ZRpc is per connection, so a
        // reconnect never inherits an earlier session's list; Jotunn's own cache is keyed by endpoint and never cleared).
        static readonly HashSet<ZRpc> modLists=new HashSet<ZRpc>();
        static bool peerInfoGate;
        static readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        static ProductRegistry? services;static Harmony? harmony,gateHarmony;static MeadworksLog? log;static double nextSilentCheck;

        internal static bool Installed=>harmony!=null;
        /// <summary>True once the peer-info gate is patched in (any game, any handshake setting).</summary>
        internal static bool PeerInfoGateActive=>peerInfoGate;
        static MeadworksLog Log=>log??=MeadworksLog.For(Plugin.Log??BepInEx.Logging.Logger.CreateLogSource("Crucible"),"Crucible");

        internal static void Install(ProductRegistry registry){
            if(harmony!=null)return;
            services=registry;harmony=new Harmony(Plugin.Guid+".peerjoin");
            harmony.Patch(AccessTools.Method(typeof(ZNet),"OnNewConnection"),postfix:new HarmonyMethod(typeof(NativePeerJoin),nameof(AfterNewConnection)));
            harmony.Patch(AccessTools.Method(typeof(ZNet),"RPC_PeerInfo"),postfix:new HarmonyMethod(typeof(NativePeerJoin),nameof(AfterPeerInfo)));
        }

        /// <summary>
        /// Server gate at the game's peer-info step (V1, network-c00b0f52): a client with no mods reconnecting from an
        /// endpoint that earlier sent Jotunn's mod list passed Jotunn's check (stale endpoint cache), spawned, and was
        /// refused only later by Charter with a plain disconnect. Here every connection must announce itself before its
        /// peer info, either with Jotunn's mod list (every MeadWorks version sends it) or with Crucible's hello (sent from
        /// this version on); under Strict one that did neither is refused with the game's ErrorVersion before the world
        /// loads. Without Jotunn the gate runs on the hello alone. If Jotunn's handler is present but cannot be observed,
        /// the gate stays off rather than refuse older MeadWorks clients; the greeting window and Charter still apply.
        /// </summary>
        /// <remarks>
        /// Installed from <c>Plugin.Awake</c> on every game, independent of <c>Network.PeerClassHandshake</c>: release
        /// checks on October 20 found the gate tied to that flag and its "on" line hidden by the Quiet log level, so no run
        /// showed it. It writes exactly one line straight to the BepInEx log (not through the Quiet filter): on, off (the
        /// Jotunn receiver cannot be observed, so older MeadWorks clients could not be told apart) or error (patching
        /// failed). Off and error leave nothing patched; admission continues through Jotunn, the greeting window and
        /// Charter. It never throws.
        /// </remarks>
        internal static void InstallPeerInfoGate(){
            if(gateHarmony!=null)return;
            try{
                gateHarmony=new Harmony(Plugin.Guid+".peerinfogate");
                var jotunn=AccessTools.TypeByName(PeerInfoGateTarget.JotunnType);
                if(jotunn!=null){
                    var receiver=AccessTools.Method(jotunn,PeerInfoGateTarget.JotunnMethod);
                    if(receiver==null||!PeerInfoGateTarget.Fits(receiver.IsStatic,receiver.GetParameters().Select(p=>p.ParameterType.Name).ToArray())){
                        GateLine(BepInEx.Logging.LogLevel.Warning,"Peer-info gate off: Jotunn's "+PeerInfoGateTarget.JotunnMethod+" is missing or has an unexpected shape. "+GateFallback);return;}
                    gateHarmony.Patch(receiver,prefix:new HarmonyMethod(typeof(NativePeerJoin),nameof(BeforeModList)));
                }
                gateHarmony.Patch(AccessTools.Method(typeof(ZNet),"OnNewConnection"),postfix:new HarmonyMethod(typeof(NativePeerJoin),nameof(RegisterHello)));
                gateHarmony.Patch(AccessTools.Method(typeof(ZNet),"RPC_ClientHandshake"),prefix:new HarmonyMethod(typeof(NativePeerJoin),nameof(BeforeClientHandshake)));
                gateHarmony.Patch(AccessTools.Method(typeof(ZNet),"RPC_PeerInfo"),prefix:new HarmonyMethod(typeof(NativePeerJoin),nameof(BeforePeerInfo)){priority=Priority.High});
                peerInfoGate=true;
                GateLine(BepInEx.Logging.LogLevel.Info,"Peer-info gate on: connections must send "+(jotunn!=null?"Jotunn's mod list or ":"")+"Crucible's hello before their peer info; under Strict admission one that sent neither is refused before it spawns.");
            }catch(Exception e){
                peerInfoGate=false;
                try{gateHarmony?.UnpatchSelf();}catch{}
                GateLine(BepInEx.Logging.LogLevel.Error,"Peer-info gate error: patching the game's join failed ("+e.GetType().Name+": "+e.Message+"). "+GateFallback);
            }
        }

        const string GateFallback="Clients without Meadworks are refused by Jotunn, the greeting window or Charter instead of before they spawn.";
        static void GateLine(BepInEx.Logging.LogLevel level,string text){
            try{(Plugin.Log??BepInEx.Logging.Logger.CreateLogSource("Crucible")).Log(level,"[Meadworks:Crucible] "+text);}catch{}
        }

        // Positional binding: Jotunn's parameter name differs between versions (2.30.2: sender).
        static void BeforeModList(ZRpc __0){
            try{var net=ZNet.instance;if(__0!=null&&net!=null&&net.IsServer())modLists.Add(__0);}catch(Exception e){Log.Warning("Recording a peer's mod list failed: "+e.GetType().Name);}
        }

        /// <summary>
        /// Client: announces Crucible to the server before the game sends its peer info. The server's ClientHandshake
        /// call runs <c>RPC_ClientHandshake</c> on the client, which sends the peer info (directly or after the password
        /// dialog), so the hello always precedes it on the same connection.
        /// </summary>
        static void BeforeClientHandshake(ZRpc __0){
            try{var net=ZNet.instance;if(__0!=null&&net!=null&&!net.IsServer())__0.Invoke(HelloRpc);}catch(Exception e){Log.Warning("Sending the Crucible hello failed: "+e.GetType().Name);}
        }

        /// <summary>Server: accepts the hello on every new connection (registered by the gate, not the greeting).</summary>
        static void RegisterHello(ZNet __instance,ZNetPeer peer){
            try{if(peer?.m_rpc!=null&&__instance!=null&&__instance.IsServer())peer.m_rpc.Register(HelloRpc,OnHello);}catch(Exception e){Log.Warning("Registering the Crucible hello failed: "+e.GetType().Name);}
        }

        static void OnHello(ZRpc rpc){try{var net=ZNet.instance;if(rpc!=null&&net!=null&&net.IsServer())modLists.Add(rpc);}catch{}}

        static bool BeforePeerInfo(ZNet __instance,ZRpc rpc,bool __runOriginal){
            if(!__runOriginal)return false;
            try{
                if(!peerInfoGate||__instance==null||!__instance.IsServer()||rpc==null)return true;
                var decision=MeadworksAdmission.Report();
                if(AdmissionPolicy.ForPeerInfo(decision,modLists.Contains(rpc))==HandshakePeerAction.Continue)return true;
                var name=rpc.GetSocket()?.GetHostName()??"?";
                var code=RefusalCodes.New();LastRefusalCode=code;
                Log.Error("Refusing peer "+name+" before spawn: it sent no mod list or Crucible hello during the handshake (client without Meadworks). "+decision.Summary+" Refusal code "+code+".");
                rpc.Invoke("Error",(int)ZNet.ConnectionStatus.ErrorVersion);
                var peer=__instance.GetPeers().FirstOrDefault(p=>ReferenceEquals(p.m_rpc,rpc));
                if(peer!=null)leaving[peer]=clock.Elapsed.TotalSeconds+Grace;
                return false;
            }catch(Exception e){Log.Error("The peer-info gate failed; the game's own checks continue: "+e.GetType().Name);return true;}
        }

        internal static void Uninstall(){harmony?.UnpatchSelf();harmony=null;gateHarmony?.UnpatchSelf();gateHarmony=null;peerInfoGate=false;verdicts.Clear();leaving.Clear();modLists.Clear();MeadworksAdmission.Reset();}

        /// <summary>The client's verdict on the server behind <paramref name="rpc"/>, when that server greeted it.</summary>
        internal static PeerClassVerdict? VerdictFor(ZRpc? rpc)=>rpc!=null&&verdicts.TryGetValue(rpc,out var verdict)?verdict:null;

        /// <summary>Applies the join verdict's per-product server states to the authority gate (after a presence reset).</summary>
        internal static bool ApplyServerStates(ZRpc? rpc){
            var verdict=VerdictFor(rpc);if(verdict==null||services==null)return false;
            MeadworksAuthority.SetServerProducts(PeerProtocol.LocalProducts(services).Select(p=>new KeyValuePair<string,ServerProduct>(p.Id,verdict.ServerStateOf(p.Id))));
            return true;
        }

        internal static void Update(){
            if(verdicts.Count>0)foreach(var rpc in verdicts.Keys.Where(r=>!r.IsConnected()).ToArray())verdicts.Remove(rpc);
            if(modLists.Count>0)modLists.RemoveWhere(r=>r==null||!r.IsConnected());
            var now=clock.Elapsed.TotalSeconds;var net=ZNet.instance;
            if(now>=nextSilentCheck){nextSilentCheck=now+1;CheckSilentPeers(net,now);}
            if(leaving.Count==0)return;
            foreach(var pair in leaving.ToArray()){
                if(now<pair.Value&&pair.Key.m_rpc!=null&&pair.Key.m_rpc.IsConnected())continue;
                leaving.Remove(pair.Key);
                if(net!=null&&net.IsServer()&&net.GetPeers().Contains(pair.Key))net.Disconnect(pair.Key);
            }
        }

        static void AfterNewConnection(ZNet __instance,ZNetPeer peer){
            try{
                if(peer?.m_rpc==null)return;
                if(__instance.IsServer()){peer.m_rpc.Register<ZPackage>(GreetRpc,OnClientAnswer);return;}
                // A client's only peer is its server connection; the greeting arrives before ZNet.GetServerRPC() reports it.
                verdicts.Remove(peer.m_rpc);
                peer.m_rpc.Register<ZPackage>(GreetRpc,OnServerGreeting);
                peer.m_rpc.Register<ZPackage>(RefuseRpc,OnRefused);
            }catch(Exception e){Log.Error("Registering the peer-class handshake failed: "+e);}
        }

        static void AfterPeerInfo(ZNet __instance,ZRpc rpc){
            try{
                if(!__instance.IsServer()||rpc==null||!rpc.IsConnected())return;
                var peer=__instance.GetPeers().FirstOrDefault(p=>ReferenceEquals(p.m_rpc,rpc));
                if(peer==null||!peer.IsReady())return;
                Greet(peer);
            }catch(Exception e){Log.Error("Greeting a new peer failed: "+e);}
        }

        /// <summary>Server: sends this server's v2 product set to a peer the game just accepted.</summary>
        static void Greet(ZNetPeer peer){
            if(services==null)return;
            peer.m_rpc.Invoke(GreetRpc,new object[]{new ZPackage(PeerProtocol.CapabilitiesV2(services))});GreetingsSent++;
            MeadworksAdmission.Peers.Greeted(peer.m_uid,clock.Elapsed.TotalSeconds);
        }

        /// <summary>Server: the peer sent a Crucible frame (greeting answer, probe, capabilities), so it runs Crucible.</summary>
        internal static void MarkCrucible(ZNetPeer? peer){if(harmony!=null&&peer!=null)MeadworksAdmission.Peers.Answered(peer.m_uid);}

        /// <summary>
        /// Server, once a second: peers that neither answered the greeting nor sent any Crucible frame before their
        /// quiet wait ends are vanilla. The quiet wait does not run while the peer's send queue is above
        /// <see cref="PeerPresenceTracker.SendBacklogBytes"/>, and it never runs past the ceiling measured from the
        /// greeting. They are refused with a native center message and the native version error, then disconnected
        /// after the grace second. Installed (with the greeting) only while the handshake flag is on.
        /// </summary>
        static void CheckSilentPeers(ZNet? net,double now){
            var peers=MeadworksAdmission.Peers;
            if(net==null||!net.IsServer()){if(peers.PendingCount+peers.VanillaCount>0)MeadworksAdmission.Reset();return;}
            var connected=net.GetPeers();
            peers.Retain(connected.Select(p=>p.m_uid));
            HoldBusyQueues(connected,now);
            var silent=peers.Expire(now);if(silent.Count==0)return;
            var decision=MeadworksAdmission.Report();
            foreach(var uid in silent){
                var peer=connected.FirstOrDefault(p=>p.m_uid==uid);if(peer?.m_rpc==null||leaving.ContainsKey(peer))continue;
                var name=uid.ToString(CultureInfo.InvariantCulture);
                var code=RefusalCodes.New();LastRefusalCode=code;
                Log.Error("Refusing peer "+name+": no Meadworks answer within "+peers.Waited(uid,now).ToString("0",CultureInfo.InvariantCulture)+" s (vanilla client, or a client with Network.PeerClassHandshake off). "+decision.Summary+" Refusal code "+code+".");
                MeadworksServerNotices.TrySend(Plugin.Guid,uid,AdmissionPolicy.VanillaRefusalNotice);
                try{peer.m_rpc.Invoke("Error",(int)ZNet.ConnectionStatus.ErrorVersion);}catch(Exception e){Log.Warning("Sending the native refusal failed: "+e.GetType().Name);}
                leaving[peer]=now+Grace;
            }
        }

        static void HoldBusyQueues(IEnumerable<ZNetPeer> connected,double now){
            var peers=MeadworksAdmission.Peers;
            foreach(var peer in connected){
                if(peer==null||peers.KindOf(peer.m_uid)!=PeerKind.Pending)continue;
                int queued;
                try{queued=peer.m_socket!=null?peer.m_socket.GetSendQueueSize():0;}
                catch(Exception e){Log.Warning("Reading the send queue for peer "+peer.m_uid.ToString(CultureInfo.InvariantCulture)+" failed: "+e.GetType().Name);continue;}
                if(queued<=PeerPresenceTracker.SendBacklogBytes)continue;
                peers.Extend(peer.m_uid,now);
                if(!peers.NoteSlow(peer.m_uid,now,MeadworksAdmission.FormerAnswerTimeoutSeconds))continue;
                Log.Warning("Peer "+peer.m_uid.ToString(CultureInfo.InvariantCulture)+" is still joining after "+MeadworksAdmission.FormerAnswerTimeoutSeconds.ToString("0",CultureInfo.InvariantCulture)+" s because "+queued.ToString(CultureInfo.InvariantCulture)+" bytes are still queued. The wait continues until the queue drains or "+peers.CeilingSeconds.ToString("0",CultureInfo.InvariantCulture)+" s.");
            }
        }

        /// <summary>Client: evaluates the server's set, records the verdict, answers with this client's set.</summary>
        static void OnServerGreeting(ZRpc rpc,ZPackage package){
            try{
                GreetingsReceived++;
                var net=ZNet.instance;if(services==null||net==null||net.IsServer()||package==null||package.Size()>65536)return;
                var result=PeerJoin.Client(PeerProtocol.LocalProducts(services),package.GetArray(),true);
                if(result.Problem!=null)Log.Warning(result.Problem);
                if(result.Verdict!=null){
                    verdicts[rpc]=result.Verdict;
                    ApplyServerStates(rpc);
                    foreach(var notice in result.Verdict.Notices)Log.Warning(notice);
                    if(!result.Verdict.Admit)foreach(var line in result.Verdict.RefusalLines)Log.Error("The server refuses this connection: "+line);
                    MeadworksRefusals.Record(result.Verdict);
                }
                if(result.Answer!=null)rpc.Invoke(GreetRpc,new object[]{new ZPackage(result.Answer)});
            }catch(Exception e){Log.Error("Reading the server's product list failed: "+e.GetType().Name);}
        }

        /// <summary>Server: refuses a client whose required products do not match, with a code both logs carry.</summary>
        static void OnClientAnswer(ZRpc rpc,ZPackage package){
            try{
                AnswersReceived++;
                var net=ZNet.instance;if(services==null||net==null||!net.IsServer()||package==null||package.Size()>65536)return;
                var peer=net.GetPeers().FirstOrDefault(p=>ReferenceEquals(p.m_rpc,rpc));if(peer==null){Log.Warning("A product list arrived from an unknown peer.");return;}if(leaving.ContainsKey(peer))return;
                var result=PeerJoin.Server(PeerProtocol.LocalProducts(services),package.GetArray(),RefusalCodes.New);
                var name=peer.m_uid.ToString(CultureInfo.InvariantCulture);
                if(result.Problem!=null)Log.Warning("Peer "+name+": "+result.Problem);
                if(result.Notice==null&&result.ClientProducts!=null&&services.Sessions.Current.AuthorityReady)
                    foreach(var product in result.ClientProducts)services.Sessions.Peers.RecordVerified(services.Sessions.Current,name,product.Id,PeerProtocol.Version);
                MarkCrucible(peer);
                if(result.Notice==null)return;
                Log.Error("Refusing peer "+name+": "+result.Notice.Message.Replace("\n"," | "));
                LastRefusalCode=result.Notice.Code;
                rpc.Invoke(RefuseRpc,new object[]{new ZPackage(result.Notice.Encode())});
                leaving[peer]=clock.Elapsed.TotalSeconds+Grace;
            }catch(Exception e){Log.Error("Checking a peer's product list failed: "+e.GetType().Name);}
        }

        /// <summary>Client: the server's refusal notice; its code goes to the log and to <see cref="MeadworksRefusals"/>.</summary>
        static void OnRefused(ZRpc rpc,ZPackage package){
            try{
                var net=ZNet.instance;if(net==null||net.IsServer()||package==null)return;
                var notice=RefusalNotice.Decode(package.GetArray());
                Log.Error("The server refused this connection: "+notice.Message.Replace("\n"," | "));
                MeadworksRefusals.RecordNotice(notice);
            }catch(Exception e){Log.Error("The server's refusal notice could not be read: "+e.GetType().Name);}
        }

        /// <summary>Server: the code of the last refusal, for diagnostics.</summary>
        internal static string? LastRefusalCode { get; private set; }
        /// <summary>Diagnostics counters: greetings this server sent and product lists it received.</summary>
        internal static int GreetingsSent { get; private set; }
        internal static int AnswersReceived { get; private set; }
        /// <summary>Client: greetings received from servers.</summary>
        internal static int GreetingsReceived { get; private set; }
    }
}
