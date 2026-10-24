using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Server admission mode (config <c>Network.AdmissionMode</c>, admin-only, server-authoritative).</summary>
    public enum AdmissionMode {
        /// <summary>Every client must run Crucible and the required products (the shipped behavior).</summary>
        Strict,
        /// <summary>Retained for legacy configuration parsing. The effective admission policy is Strict.</summary>
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
        /// <summary>Why a retained legacy request is not supported.</summary>
        public IReadOnlyList<string> Reasons { get; }
        public bool AdmitsVanilla=>Effective==AdmissionMode.AllowVanilla;
        internal AdmissionDecision(AdmissionMode requested,AdmissionMode effective,IReadOnlyList<string> reasons){Requested=requested;Effective=effective;Reasons=reasons;}
        /// <summary>One line for the server log.</summary>
        public string Summary=>Requested==AdmissionMode.Strict?"Admission mode Strict: every client must run Meadworks.":AdmissionPolicy.RetiredModeNotice;
    }

    /// <summary>
    /// Every client must run Meadworks. Legacy admission values and capability identifiers remain readable,
    /// but product declarations and Jotunn metadata cannot enable unmodded-client admission.
    /// </summary>
    public static class AdmissionPolicy {
        public const string VanillaSupportCapability="meadworks.admission.vanillasupport";
        public const string VanillaCountCapability="meadworks.peers.vanillacount";
        public const string PeerKindCapability="meadworks.peers.kind";
        public static readonly Version CapabilityVersion=new Version(1,0);
        /// <summary>Default of <c>Network.AdmissionMode</c>: every client must run Meadworks.</summary>
        public const AdmissionMode DefaultMode=AdmissionMode.Strict;
        public const string RetiredModeNotice="Network.AdmissionMode AllowVanilla is no longer supported; Strict applies and every client must run Meadworks. The saved value is retained.";
        /// <summary>
        /// Literal text for the native center message before a vanilla client is refused. The server cannot read a vanilla
        /// client's language, so one line carries English, Spanish, French, German, Russian, Chinese and Korean.
        /// </summary>
        public const string VanillaRefusalNotice="This server requires the Meadworks mod pack. Install it to join. / Este servidor requiere el paquete de mods Meadworks. Instálalo para unirte. / Ce serveur nécessite le pack de mods Meadworks. Installez-le pour rejoindre la partie. / Dieser Server erfordert die Meadworks-Mods. Installiere sie, um beizutreten. / Для этого сервера нужен набор модов Meadworks. Установите его, чтобы присоединиться. / 此服务器需要 Meadworks 模组包。请安装后再加入。 / 이 서버는 Meadworks 모드 팩이 필요합니다. 참가하려면 설치하세요.";
        /// <summary>Literal text for the native center message a vanilla client receives once admitted. One line, same seven languages.</summary>
        public const string VanillaWelcomeNotice="This server runs Meadworks in server-only mode. Install the mod pack for the full experience. / Este servidor usa Meadworks solo en el servidor. Instala el paquete de mods para la experiencia completa. / Ce serveur utilise Meadworks uniquement sur le serveur. Installez le pack de mods pour l'expérience complète. / Dieser Server führt Meadworks nur auf dem Server aus. Installiere die Mods für das volle Spiel. / На этом сервере Meadworks работает только на сервере. Установите моды, чтобы получить все возможности. / 此服务器仅在服务器端运行 Meadworks。安装模组包即可获得完整体验。 / 이 서버는 서버에서만 Meadworks를 실행합니다. 전체 내용을 보려면 모드 팩을 설치하세요.";

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
            if(jotunnBlockers==null)throw new ArgumentNullException(nameof(jotunnBlockers));
            if(requested==AdmissionMode.Strict)return new AdmissionDecision(requested,AdmissionMode.Strict,Array.Empty<string>());
            return new AdmissionDecision(requested,AdmissionMode.Strict,Array.AsReadOnly(new[]{RetiredModeNotice}));
        }

        /// <summary>Strict refuses a peer that never answered the greeting (bounded missing-handshake handling).</summary>
        public static SilentPeerAction ForSilentPeer(AdmissionDecision decision)=>
            (decision??throw new ArgumentNullException(nameof(decision))).AdmitsVanilla?SilentPeerAction.AdmitAsVanilla:SilentPeerAction.Refuse;

        /// <summary>
        /// Peer-info gate: every modded client (any MeadWorks version, since every product needs Jotunn) sends Jotunn's
        /// mod list on that connection before its peer info, so a peer that sent none runs no mods. Strict refuses it
        /// there, before it spawns. Version differences are not judged
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
    /// it answers (Crucible) or its quiet wait passes (vanilla). A busy send queue restarts that wait
    /// (<see cref="Extend"/>), and the wait never runs past <see cref="CeilingSeconds"/> from the greeting.
    /// A late answer never upgrades a vanilla verdict. Main thread only.
    /// </summary>
    public sealed class PeerPresenceTracker {
        /// <summary>Five minutes from the greeting. A peer that has not answered by then is vanilla.</summary>
        public const double DefaultCeilingSeconds=300;
        /// <summary>Above this many unsent bytes the greeting may still be queued, so the quiet wait has not started.</summary>
        public const int SendBacklogBytes=64*1024;
        sealed class Entry { internal PeerKind Kind; internal double Due; internal double Greeted; internal bool SlowNoted; }
        readonly Dictionary<long,Entry> peers=new Dictionary<long,Entry>();
        public double AnswerTimeoutSeconds { get; }
        public double CeilingSeconds { get; }
        public PeerPresenceTracker(double answerTimeoutSeconds,double ceilingSeconds=DefaultCeilingSeconds){
            if(double.IsNaN(answerTimeoutSeconds)||double.IsInfinity(answerTimeoutSeconds)||answerTimeoutSeconds<=0)throw new ArgumentException("Expected a positive finite timeout.",nameof(answerTimeoutSeconds));
            if(double.IsNaN(ceilingSeconds)||double.IsInfinity(ceilingSeconds)||ceilingSeconds<=0)throw new ArgumentException("Expected a positive finite ceiling.",nameof(ceilingSeconds));
            AnswerTimeoutSeconds=answerTimeoutSeconds;CeilingSeconds=ceilingSeconds;
        }
        public void Greeted(long peerId,double now){if(!peers.ContainsKey(peerId))peers[peerId]=new Entry{Kind=PeerKind.Pending,Greeted=now,Due=now+AnswerTimeoutSeconds};}
        /// <summary>The send queue is still busy, so the quiet wait starts again from <paramref name="now"/> unless the ceiling has been reached.</summary>
        public bool Extend(long peerId,double now){
            if(!peers.TryGetValue(peerId,out var e)||e.Kind!=PeerKind.Pending||now>=e.Greeted+CeilingSeconds)return false;
            var due=Math.Max(e.Due,now+AnswerTimeoutSeconds);if(due==e.Due)return false;
            e.Due=due;return true;
        }
        /// <summary>True once, the first time a pending peer has waited longer than <paramref name="formerTimeoutSeconds"/>.</summary>
        public bool NoteSlow(long peerId,double now,double formerTimeoutSeconds){
            if(!peers.TryGetValue(peerId,out var e)||e.Kind!=PeerKind.Pending||e.SlowNoted||!(now-e.Greeted>formerTimeoutSeconds))return false;
            e.SlowNoted=true;return true;
        }
        /// <summary>Seconds from the greeting to <paramref name="now"/>, or the quiet wait when the peer was never greeted.</summary>
        public double Waited(long peerId,double now)=>peers.TryGetValue(peerId,out var e)?Math.Max(0,now-e.Greeted):AnswerTimeoutSeconds;
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
            var due=peers.Where(p=>p.Value.Kind==PeerKind.Pending&&(now>=p.Value.Due||now>=p.Value.Greeted+CeilingSeconds)).Select(p=>p.Key).OrderBy(i=>i).ToList();
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
