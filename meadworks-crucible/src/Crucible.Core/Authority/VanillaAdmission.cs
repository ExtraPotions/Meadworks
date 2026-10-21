using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Server admission mode (config <c>Network.AdmissionMode</c>, admin-only, server-authoritative).</summary>
    public enum AdmissionMode {
        /// <summary>Every client must run Crucible and the required products (the shipped behavior).</summary>
        Strict,
        /// <summary>The admin opts in to vanilla/console clients; effective only for a compatible feature set.</summary>
        AllowVanilla,
    }

    /// <summary>
    /// What a product declares about clients without Meadworks; values equal
    /// <c>ExtraPotions.Meadworks.Contracts.VanillaSupportContract</c>.
    /// </summary>
    public enum VanillaSupport { ClientRequired=0, Mixed=1, ServerOnlyCapable=2 }

    /// <summary>What the server knows about one connected peer; values equal <c>PeerKindContract</c>.</summary>
    public enum PeerKind { Unknown=0, Pending=1, Crucible=2, Vanilla=3 }

    /// <summary>What to do with a peer that never answered the join greeting.</summary>
    public enum SilentPeerAction { Refuse, AdmitAsVanilla }

    /// <summary>What to do with a peer at the game's peer-info step, before the world loads for it.</summary>
    public enum HandshakePeerAction { Continue, RefuseBeforeSpawn }

    /// <summary>One installed product as the admission decision sees it.</summary>
    public sealed class AdmissionProduct {
        public string Id { get; }
        public string Name { get; }
        public MultiplayerPolicy Policy { get; }
        /// <summary>The product's own declaration, null when it declared nothing readable.</summary>
        public VanillaSupport? Declared { get; }
        public AdmissionProduct(string id,string name,MultiplayerPolicy policy,VanillaSupport? declared){
            Validation.Id(id);
            if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Invalid product name.",nameof(name));
            if(!Enum.IsDefined(typeof(MultiplayerPolicy),policy))throw new ArgumentException("Unknown multiplayer policy.",nameof(policy));
            if(declared!=null&&!Enum.IsDefined(typeof(VanillaSupport),declared.Value))throw new ArgumentException("Unknown vanilla support.",nameof(declared));
            Id=id;Name=name;Policy=policy;Declared=declared;
        }
    }

    public sealed class AdmissionDecision {
        public AdmissionMode Requested { get; }
        public AdmissionMode Effective { get; }
        /// <summary>Why AllowVanilla is not in effect: one line per product or Jotunn entry that must be disabled.</summary>
        public IReadOnlyList<string> Reasons { get; }
        public bool AdmitsVanilla=>Effective==AdmissionMode.AllowVanilla;
        internal AdmissionDecision(AdmissionMode requested,AdmissionMode effective,IReadOnlyList<string> reasons){Requested=requested;Effective=effective;Reasons=reasons;}
        /// <summary>One line for the server log.</summary>
        public string Summary=>Requested==AdmissionMode.Strict?"Admission mode Strict: every client must run Meadworks."
            :Effective==AdmissionMode.AllowVanilla?"Admission mode AllowVanilla: clients without Meadworks may join and receive server-side features."
            :"Admission mode AllowVanilla requested, staying Strict until these are disabled or declare vanilla support: "+string.Join(" | ",Reasons);
    }

    /// <summary>
    /// Optional server-only mode (decision 2026-10-08; user policy: refuse vanilla admission while incompatible
    /// content/features are active, until the admin selects a compatible feature set; saves and items are untouched).
    /// AllowVanilla is effective only when every installed product is server-only-capable or mixed and no installed
    /// mod's Jotunn metadata requires the mod on clients; otherwise the server stays Strict and names each blocker.
    /// </summary>
    public static class AdmissionPolicy {
        public const string VanillaSupportCapability="meadworks.admission.vanillasupport";
        public const string VanillaCountCapability="meadworks.peers.vanillacount";
        public const string PeerKindCapability="meadworks.peers.kind";
        public static readonly Version CapabilityVersion=new Version(1,0);
        /// <summary>Default of <c>Network.AdmissionMode</c>: every client must run Meadworks.</summary>
        public const AdmissionMode DefaultMode=AdmissionMode.Strict;
        /// <summary>
        /// Literal text for the native center message before a vanilla client is refused. The server cannot read a vanilla
        /// client's language, so one line carries English, Spanish and French.
        /// </summary>
        public const string VanillaRefusalNotice="This server requires the Meadworks mod pack. Install it to join. / Este servidor requiere el paquete de mods Meadworks. Instálalo para unirte. / Ce serveur nécessite le pack de mods Meadworks. Installez-le pour rejoindre la partie.";
        /// <summary>Literal text for the native center message a vanilla client receives once admitted.</summary>
        public const string VanillaWelcomeNotice="This server runs Meadworks in server-only mode. Install the mod pack for the full experience.";

        /// <summary>
        /// The support a product effectively offers: its explicit declaration, else server-only products are capable,
        /// optional and client-only products are mixed (vanilla clients simply lack them), required products are
        /// client-required.
        /// </summary>
        public static VanillaSupport SupportOf(AdmissionProduct product){
            if(product==null)throw new ArgumentNullException(nameof(product));
            if(product.Declared!=null)return product.Declared.Value;
            switch(product.Policy){
                case MultiplayerPolicy.ServerOnly:return VanillaSupport.ServerOnlyCapable;
                case MultiplayerPolicy.RequiredOnAllPeers:return VanillaSupport.ClientRequired;
                default:return VanillaSupport.Mixed;
            }
        }

        /// <param name="jotunnBlockers">Installed mods whose Jotunn NetworkCompatibility requires the mod on clients, as display text.</param>
        public static AdmissionDecision Decide(AdmissionMode requested,IEnumerable<AdmissionProduct> products,IEnumerable<string> jotunnBlockers){
            if(!Enum.IsDefined(typeof(AdmissionMode),requested))throw new ArgumentException("Unknown admission mode.",nameof(requested));
            var list=(products??throw new ArgumentNullException(nameof(products))).ToList();
            if(list.Any(p=>p==null))throw new ArgumentException("Null product.",nameof(products));
            if(list.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=list.Count)throw new ArgumentException("Duplicate product id.",nameof(products));
            var blockers=(jotunnBlockers??throw new ArgumentNullException(nameof(jotunnBlockers))).Where(b=>!string.IsNullOrWhiteSpace(b)).Select(b=>b.Trim()).Distinct(StringComparer.Ordinal).OrderBy(b=>b,StringComparer.Ordinal).ToList();
            if(requested==AdmissionMode.Strict)return new AdmissionDecision(requested,AdmissionMode.Strict,Array.Empty<string>());
            var reasons=new List<string>();
            foreach(var p in list.OrderBy(p=>p.Id,StringComparer.Ordinal)){
                if(SupportOf(p)!=VanillaSupport.ClientRequired)continue;
                reasons.Add(p.Declared==null
                    ?p.Name+" has not declared vanilla support; disable it or keep Strict."
                    :p.Name+" requires the mod on every client; disable it or keep Strict.");
            }
            foreach(var b in blockers)reasons.Add("Jotunn: "+b+" requires the mod on every client.");
            return reasons.Count==0
                ?new AdmissionDecision(requested,AdmissionMode.AllowVanilla,Array.Empty<string>())
                :new AdmissionDecision(requested,AdmissionMode.Strict,reasons.AsReadOnly());
        }

        /// <summary>Strict refuses a peer that never answered the greeting (bounded missing-handshake handling).</summary>
        public static SilentPeerAction ForSilentPeer(AdmissionDecision decision)=>
            (decision??throw new ArgumentNullException(nameof(decision))).AdmitsVanilla?SilentPeerAction.AdmitAsVanilla:SilentPeerAction.Refuse;

        /// <summary>
        /// Peer-info gate: every modded client (any MeadWorks version, since every product needs Jotunn) sends Jotunn's
        /// mod list on that connection before its peer info, so a peer that sent none runs no mods. Strict refuses it
        /// there, before it spawns; AllowVanilla lets it continue to the greeting. Version differences are not judged
        /// here: older MeadWorks clients continue to the existing greeting and Charter checks.
        /// </summary>
        public static HandshakePeerAction ForPeerInfo(AdmissionDecision decision,bool sentModList)=>
            (decision??throw new ArgumentNullException(nameof(decision))).AdmitsVanilla||sentModList?HandshakePeerAction.Continue:HandshakePeerAction.RefuseBeforeSpawn;

        /// <summary>Every registered product with its <see cref="VanillaSupportCapability"/> declaration, if readable.</summary>
        public static IReadOnlyList<AdmissionProduct> Products(ProductRegistry registry){
            if(registry==null)throw new ArgumentNullException(nameof(registry));
            var declared=new Dictionary<string,VanillaSupport>(StringComparer.Ordinal);
            foreach(var pair in registry.Capabilities.ResolveEach<Func<int>>(VanillaSupportCapability,CapabilityVersion)){
                int raw;
                try{raw=pair.Value();}catch{continue;}
                if(Enum.IsDefined(typeof(VanillaSupport),raw))declared[pair.Key]=(VanillaSupport)raw;
            }
            return registry.Snapshot().Select(m=>new AdmissionProduct(m.Id,m.Name,m.Multiplayer,declared.TryGetValue(m.Id,out var s)?s:(VanillaSupport?)null)).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Server-side record of which connected peers run Crucible, from the join greeting: a greeted peer is pending until
    /// it answers (Crucible) or the answer timeout passes (vanilla). A late answer never upgrades a vanilla verdict.
    /// Main thread only.
    /// </summary>
    public sealed class PeerPresenceTracker {
        sealed class Entry { internal PeerKind Kind; internal double Due; }
        readonly Dictionary<long,Entry> peers=new Dictionary<long,Entry>();
        public double AnswerTimeoutSeconds { get; }
        public PeerPresenceTracker(double answerTimeoutSeconds){
            if(double.IsNaN(answerTimeoutSeconds)||double.IsInfinity(answerTimeoutSeconds)||answerTimeoutSeconds<=0)throw new ArgumentException("Expected a positive finite timeout.",nameof(answerTimeoutSeconds));
            AnswerTimeoutSeconds=answerTimeoutSeconds;
        }
        public void Greeted(long peerId,double now){if(!peers.ContainsKey(peerId))peers[peerId]=new Entry{Kind=PeerKind.Pending,Due=now+AnswerTimeoutSeconds};}
        /// <summary>
        /// Records that the peer runs Crucible (an answer or any Crucible frame, possibly before the greeting); false when it
        /// was already known as Crucible or already judged vanilla.
        /// </summary>
        public bool Answered(long peerId){
            if(!peers.TryGetValue(peerId,out var e)){peers[peerId]=new Entry{Kind=PeerKind.Crucible};return true;}
            if(e.Kind!=PeerKind.Pending)return false;
            e.Kind=PeerKind.Crucible;return true;
        }
        /// <summary>Pending peers whose answer is overdue become vanilla; returns them once, ordered by id.</summary>
        public IReadOnlyList<long> Expire(double now){
            var due=peers.Where(p=>p.Value.Kind==PeerKind.Pending&&now>=p.Value.Due).Select(p=>p.Key).OrderBy(i=>i).ToList();
            foreach(var id in due)peers[id].Kind=PeerKind.Vanilla;
            return due.AsReadOnly();
        }
        public void Left(long peerId)=>peers.Remove(peerId);
        /// <summary>Forgets every peer not in <paramref name="connected"/>.</summary>
        public void Retain(IEnumerable<long> connected){
            var keep=new HashSet<long>(connected??throw new ArgumentNullException(nameof(connected)));
            foreach(var id in peers.Keys.Where(k=>!keep.Contains(k)).ToList())peers.Remove(id);
        }
        public void Reset()=>peers.Clear();
        public PeerKind KindOf(long peerId)=>peers.TryGetValue(peerId,out var e)?e.Kind:PeerKind.Unknown;
        public int VanillaCount=>peers.Values.Count(e=>e.Kind==PeerKind.Vanilla);
        public int PendingCount=>peers.Values.Count(e=>e.Kind==PeerKind.Pending);
        public bool AnyVanilla=>VanillaCount>0;
        /// <summary>
        /// Connected peers not confirmed as Crucible: vanilla, still pending (greeted, no answer yet) or unknown (never
        /// greeted). This is what <c>meadworks.peers.vanillacount</c> reports, so a joining vanilla client counts from its
        /// first ready frame instead of after the answer timeout. A Meadworks client counts too until its answer or first
        /// Crucible frame arrives (one round trip).
        /// </summary>
        public int NotCrucibleCount(IEnumerable<long> connected)=>
            (connected??throw new ArgumentNullException(nameof(connected))).Distinct().Count(id=>KindOf(id)!=PeerKind.Crucible);
        public string Describe()=>string.Format(CultureInfo.InvariantCulture,"{0} Crucible, {1} vanilla, {2} pending",peers.Values.Count(e=>e.Kind==PeerKind.Crucible),VanillaCount,PendingCount);
    }
}
