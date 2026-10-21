using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Jotunn.Utils;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Optional server-only mode (see <c>docs/server-only-mode.md</c>): the admin-only, server-authoritative setting
    /// <c>Network.AdmissionMode</c>, the effective decision (<see cref="AdmissionPolicy"/>) over the installed products'
    /// declarations and every installed mod's Jotunn metadata, the per-peer presence record fed by the join greeting and
    /// the Crucible frames a client sends, and the shared capabilities products read. Crucible never changes any mod's
    /// Jotunn metadata: while a mod declares EveryoneMustHaveMod or ClientMustHaveMod, Jotunn itself still refuses
    /// vanilla clients and this decision stays Strict. Main thread only.
    /// </summary>
    internal static class MeadworksAdmission {
        /// <summary>Seconds a greeted peer has to answer or send any Crucible frame before it counts as vanilla.</summary>
        internal const double AnswerTimeoutSeconds=30;
        internal static PeerPresenceTracker Peers { get; }=new PeerPresenceTracker(AnswerTimeoutSeconds);
        static ConfigEntry<AdmissionMode>? mode;static string? lastSummary;

        internal static void Bind(ConfigFile config){
            mode=MeadworksConfig.Bind(config,"Network","AdmissionMode",AdmissionPolicy.DefaultMode,
                "Strict: every client must run Meadworks. AllowVanilla: clients without Meadworks (vanilla/console) may join and receive server-side features; it takes effect only when every installed product declares vanilla support and no installed mod's Jotunn metadata requires the mod on clients, otherwise the server logs why and stays Strict. Needs Network.PeerClassHandshake on the server. Saves and items are never changed.",
                ConfigScope.ServerAuthoritative);
            mode.SettingChanged+=(_,__)=>{try{Report();}catch(Exception e){Plugin.Log?.LogWarning("Admission mode check failed: "+e.GetType().Name);}};
        }

        /// <summary>Crucible's own declaration (mixed: its services degrade for vanilla peers) and the peer capabilities.</summary>
        internal static void Register(ProductRegistration crucible){
            crucible.AddCapability<Func<int>>(AdmissionPolicy.VanillaSupportCapability,AdmissionPolicy.CapabilityVersion,()=>(int)VanillaSupport.Mixed);
            crucible.AddCapability<Func<int>>(AdmissionPolicy.VanillaCountCapability,AdmissionPolicy.CapabilityVersion,VanillaCount);
            crucible.AddCapability<Func<long,int>>(AdmissionPolicy.PeerKindCapability,AdmissionPolicy.CapabilityVersion,KindOf);
        }

        static bool Tracking=>NativePeerJoin.Installed&&ZNet.instance!=null&&ZNet.instance.IsServer();
        // Pending and unknown peers count as vanilla (vanilla-admission audit 2026-10-13); see PeerPresenceTracker.NotCrucibleCount.
        static int VanillaCount()=>Tracking?Peers.NotCrucibleCount(ZNet.instance.GetPeers().Where(p=>p!=null&&p.IsReady()).Select(p=>p.m_uid)):-1;
        static int KindOf(long peerId)=>Tracking?(int)Peers.KindOf(peerId):(int)PeerKind.Unknown;

        internal static AdmissionDecision Current()=>AdmissionPolicy.Decide(mode?.Value??AdmissionPolicy.DefaultMode,AdmissionPolicy.Products(Plugin.Services),JotunnBlockers());

        /// <summary>The current decision; logs it once whenever its summary changes.</summary>
        internal static AdmissionDecision Report(){
            var decision=Current();
            if(decision.Summary!=lastSummary){
                lastSummary=decision.Summary;
                if(decision.Requested!=decision.Effective)Plugin.Log?.LogWarning(decision.Summary);else Plugin.Log?.LogInfo(decision.Summary);
                if(decision.Requested==AdmissionMode.AllowVanilla&&!NativePeerJoin.Installed)Plugin.Log?.LogWarning("Admission mode AllowVanilla needs Network.PeerClassHandshake on this server to tell vanilla clients apart.");
            }
            return decision;
        }

        /// <summary>Installed mods whose Jotunn NetworkCompatibility requires them on clients.</summary>
        static IEnumerable<string> JotunnBlockers(){
            var list=new List<string>();
            foreach(var info in Chainloader.PluginInfos.Values){
                try{
                    var type=info?.Instance?.GetType();if(type==null)continue;
                    if(!(Attribute.GetCustomAttribute(type,typeof(NetworkCompatibilityAttribute),true) is NetworkCompatibilityAttribute attribute))continue;
                    var level=attribute.EnforceModOnClients.ToString();
                    if(level=="EveryoneMustHaveMod"||level=="ClientMustHaveMod")list.Add(info!.Metadata.Name+" ("+level+")");
                }catch(Exception e){Plugin.Log?.LogWarning("Reading a plugin's Jotunn compatibility failed: "+e.GetType().Name);}
            }
            return list;
        }

        internal static void Reset(){Peers.Reset();}
    }
}
