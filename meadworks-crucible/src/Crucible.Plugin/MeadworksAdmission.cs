using System;
using System.Linq;
using BepInEx.Configuration;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Strict admission, the retained legacy configuration key, and peer-presence capabilities.
    /// Crucible never changes another mod's Jotunn metadata. Main thread only.
    /// </summary>
    internal static class MeadworksAdmission {
        /// <summary>Seconds a greeted peer has to answer once its send queue has drained. A busy queue restarts this, up to <see cref="PeerPresenceTracker.DefaultCeilingSeconds"/>.</summary>
        internal const double AnswerTimeoutSeconds=60;
        /// <summary>The fixed timeout this replaced. One warning is written when a busy queue keeps a peer waiting longer than this.</summary>
        internal const double FormerAnswerTimeoutSeconds=30;
        internal static PeerPresenceTracker Peers { get; }=new PeerPresenceTracker(AnswerTimeoutSeconds);
        static ConfigEntry<AdmissionMode>? mode;static string? lastSummary;

        internal static void Bind(ConfigFile config){
            lastSummary=null;
            mode=MeadworksConfig.BindHidden(config,"Network","AdmissionMode",AdmissionPolicy.DefaultMode,
                "Retained legacy preference. Strict always applies: every client must run Meadworks. AllowVanilla is no longer supported; its saved value is preserved.",
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

        internal static AdmissionDecision Current()=>AdmissionPolicy.Decide(mode?.Value??AdmissionPolicy.DefaultMode,Array.Empty<AdmissionProduct>(),Array.Empty<string>());

        /// <summary>The current decision; logs it once whenever its summary changes.</summary>
        internal static AdmissionDecision Report(){
            var decision=Current();
            if(decision.Summary!=lastSummary){
                lastSummary=decision.Summary;
                if(decision.Requested!=decision.Effective)Plugin.Log?.LogWarning(decision.Summary);else Plugin.Log?.LogInfo(decision.Summary);
            }
            return decision;
        }

        internal static void Reset(){Peers.Reset();}
    }
}
