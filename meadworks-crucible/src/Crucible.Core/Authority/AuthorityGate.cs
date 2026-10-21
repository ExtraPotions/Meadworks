using System;
using System.Collections.Generic;
using System.Globalization;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Roster peer classes. Mapped from <see cref="MultiplayerPolicy"/> by <see cref="PeerClasses.From"/>.</summary>
    public enum PeerClass {
        /// <summary>Changes world state, shared items or rules; every peer runs the same major.minor.</summary>
        RequiredOnAllPeers,
        /// <summary>UI or personal convenience; works on any server, the server may lock individual settings.</summary>
        ClientOptional,
        /// <summary>Runs only on the server; a client copy stays idle.</summary>
        ServerOnly,
    }

    public static class PeerClasses {
        /// <summary>ClientOnly and Optional products are client-optional; RequiredOnAllPeers and ServerOnly map one to one.</summary>
        public static PeerClass From(MultiplayerPolicy policy){
            switch(policy){
                case MultiplayerPolicy.RequiredOnAllPeers:return PeerClass.RequiredOnAllPeers;
                case MultiplayerPolicy.ClientOnly:case MultiplayerPolicy.Optional:return PeerClass.ClientOptional;
                case MultiplayerPolicy.ServerOnly:return PeerClass.ServerOnly;
                default:throw new ArgumentException("Unknown multiplayer policy.",nameof(policy));
            }
        }
        public static PeerClass Of(ProductMetadata product)=>From((product??throw new ArgumentNullException(nameof(product))).Multiplayer);
    }

    /// <summary>Whether the connected server runs this product. Unknown until the peer-class handshake (C04) reports.</summary>
    public enum ServerProduct { Unknown, Present, Missing }

    /// <summary>Everything the gate needs to know about the session and one product.</summary>
    public sealed class AuthorityFacts {
        public SessionRole Role { get; }
        public bool AuthorityReady { get; }
        public bool IsAdmin { get; }
        public ServerCrucible Server { get; }
        public PeerClass PeerClass { get; }
        public ServerProduct ServerProduct { get; }
        public AuthorityFacts(SessionRole role,bool authorityReady,bool isAdmin,ServerCrucible server,PeerClass peerClass,ServerProduct serverProduct=ServerProduct.Unknown){
            if(!Enum.IsDefined(typeof(SessionRole),role))throw new ArgumentException("Unknown session role.",nameof(role));
            if(!Enum.IsDefined(typeof(ServerCrucible),server))throw new ArgumentException("Unknown server state.",nameof(server));
            if(!Enum.IsDefined(typeof(PeerClass),peerClass))throw new ArgumentException("Unknown peer class.",nameof(peerClass));
            if(!Enum.IsDefined(typeof(ServerProduct),serverProduct))throw new ArgumentException("Unknown server product state.",nameof(serverProduct));
            Role=role;AuthorityReady=authorityReady;IsAdmin=isAdmin;Server=server;PeerClass=peerClass;ServerProduct=serverProduct;
        }
        public static AuthorityFacts From(SessionSnapshot session,ServerCrucible server,PeerClass peerClass,ServerProduct serverProduct=ServerProduct.Unknown){
            if(session==null)throw new ArgumentNullException(nameof(session));
            return new AuthorityFacts(session.Role,session.AuthorityReady,session.IsAdmin,server,peerClass,serverProduct);
        }
        internal ConfigAuthorityFacts Config=>new ConfigAuthorityFacts(Role,AuthorityReady,IsAdmin,Server);
    }

    public sealed class AuthorityDecision {
        public bool Allowed { get; }
        public ConfigBlockReason Reason { get; }
        public ConfigValueSource Source { get; }
        /// <summary>The edit changes the server's value, so it is sent to the server rather than written locally.</summary>
        public bool RequiresServerAmendment { get; }
        /// <summary>False when the product is off for this session (vanilla server, missing on server, server-only on a client).</summary>
        public bool ProductActive { get; }
        /// <summary>The scope the decision was made for (amendment routes publish only synchronized scopes).</summary>
        public ConfigScope Scope { get; }
        internal AuthorityDecision(ConfigScope scope,bool allowed,ConfigBlockReason reason,ConfigValueSource source,bool amend,bool active){Scope=scope;Allowed=allowed;Reason=reason;Source=source;RequiresServerAmendment=amend;ProductActive=active;}
        internal static AuthorityDecision From(ConfigScope scope,ConfigEditDecision d)=>new AuthorityDecision(scope,d.Allowed,d.Reason,d.Source,d.RequiresServerAmendment,true);
        /// <summary>
        /// Whether the player may act on the value in effect (use, not edit): the product is on and the value is not
        /// locked. A non-admin may use a cheat the server enabled, though only an admin may change it (<see cref="Allowed"/>).
        /// </summary>
        public bool MayUse=>ProductActive&&Source!=ConfigValueSource.Locked;
        /// <summary>The value in effect: blocked cheats evaluate to their safe value.</summary>
        public T Effective<T>(ScopedValue<T> value)=>(value??throw new ArgumentNullException(nameof(value))).Effective(Source);
        /// <summary>English text for the player; <see cref="BlockedOptionText.Reason"/> gives the localized one.</summary>
        public string Message=>AuthorityGate.Describe(Reason);
        public override string ToString()=>(Allowed?"Allowed":"Blocked:"+Reason)+" source="+Source+(RequiresServerAmendment?" amend":"")+(ProductActive?"":" product-off");
    }

    /// <summary>
    /// The one authority query products use (roster decision 1 and peer classes). The player decides in single player;
    /// in multiplayer the server controls every Meadworks setting and cheat; blocked options carry a reason. Product
    /// activity follows the peer class: a required-on-all-peers product turns off for the session on a vanilla server
    /// or on a Crucible server that lacks it; a server-only product idles on clients; a client-optional product works
    /// anywhere, with its cheats locked unless a server copy vouches for them. Setting rules come from
    /// <see cref="ConfigEditPolicy"/>.
    /// </summary>
    public static class AuthorityGate {
        public static bool ProductActive(AuthorityFacts facts,out ConfigBlockReason reason){
            if(facts==null)throw new ArgumentNullException(nameof(facts));
            reason=ConfigBlockReason.None;
            if(facts.Role!=SessionRole.RemoteClient)return true;
            if(facts.PeerClass==PeerClass.ServerOnly)reason=ConfigBlockReason.ServerOnly;
            else if(facts.PeerClass==PeerClass.RequiredOnAllPeers&&facts.Server==ServerCrucible.Absent)reason=ConfigBlockReason.VanillaServer;
            else if(facts.PeerClass==PeerClass.RequiredOnAllPeers&&facts.ServerProduct==ServerProduct.Missing)reason=ConfigBlockReason.ProductDisabled;
            return reason==ConfigBlockReason.None;
        }

        /// <param name="serverLocked">Whether the server binds this ServerAuthoritative setting; honored only for client-optional products.</param>
        public static AuthorityDecision Decide(ConfigScope scope,AuthorityFacts facts,bool serverLocked=true){
            if(!Enum.IsDefined(typeof(ConfigScope),scope))throw new ArgumentException("Unknown configuration scope.",nameof(scope));
            if(facts==null)throw new ArgumentNullException(nameof(facts));
            bool cheat=scope==ConfigScope.Cheat;
            if(scope==ConfigScope.Local||facts.Role!=SessionRole.RemoteClient)return AuthorityDecision.From(scope,ConfigEditPolicy.Decide(scope,facts.Config,serverLocked));
            if(!ProductActive(facts,out var off))return new AuthorityDecision(scope,false,off,cheat?ConfigValueSource.Locked:ConfigValueSource.Own,false,false);
            bool optional=facts.PeerClass==PeerClass.ClientOptional;
            if(optional&&facts.ServerProduct==ServerProduct.Missing)
                return cheat?new AuthorityDecision(scope,false,ConfigBlockReason.ProductDisabled,ConfigValueSource.Locked,false,true):new AuthorityDecision(scope,true,ConfigBlockReason.None,ConfigValueSource.Own,false,true);
            return AuthorityDecision.From(scope,ConfigEditPolicy.Decide(scope,facts.Config,!optional||serverLocked));
        }

        /// <summary>Authoritative English reason text (empty for None); localized texts come from <see cref="BlockedOptionText"/>.</summary>
        public static string Describe(ConfigBlockReason reason)=>BlockedOptionText.English(reason);
    }

    /// <summary>Review rule for cheat defaults: a cheat ships with its safe value, so a fresh install never cheats.</summary>
    public static class CheatDefaults {
        /// <returns>Null when the default is the safe value; otherwise one line naming the offending key.</returns>
        public static string? Review<T>(string key,T defaultValue,T safeValue){
            if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Expected a setting key.",nameof(key));
            if(EqualityComparer<T>.Default.Equals(defaultValue,safeValue))return null;
            return key+" defaults to "+Convert.ToString(defaultValue,CultureInfo.InvariantCulture)+"; a cheat must default to its safe value "+Convert.ToString(safeValue,CultureInfo.InvariantCulture)+".";
        }
        /// <summary>C13 review rule: every cheat carries a Mischief category. Null when tagged; otherwise one line naming the key.</summary>
        public static string? ReviewCategory(string key,MischiefCategory? category){
            if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Expected a setting key.",nameof(key));
            return category.HasValue?null:key+" has no Mischief category; it is treated as "+MischiefCategories.Untagged+" (strictest). Bind it with a category.";
        }
    }

    /// <summary>
    /// Turns a missing Crucible Hello into <see cref="ServerCrucible.Absent"/> (vanilla server). The plugin calls
    /// <see cref="Connect"/> once per server connection, <see cref="Hello"/> when the server's Hello is accepted,
    /// <see cref="Disconnect"/> when the connection ends and <see cref="Tick"/> every frame with a monotonic clock.
    /// A late Hello still upgrades to Present: the server proved itself, only slower.
    /// </summary>
    public sealed class ServerPresenceTimer {
        double started;
        double timeout;
        /// <summary>How long to wait for a Hello; a change applies to the current wait as well (setting changed in game).</summary>
        public double TimeoutSeconds { get=>timeout; set{if(double.IsNaN(value)||value<=0||value>300)throw new ArgumentOutOfRangeException(nameof(value),"Timeout must be in (0, 300] seconds.");timeout=value;} }
        public ServerCrucible State { get; private set; }=ServerCrucible.Unknown;
        public bool Waiting { get; private set; }
        public ServerPresenceTimer(double timeoutSeconds=10){
            TimeoutSeconds=timeoutSeconds;
        }
        /// <summary>Starts waiting for a Hello; ignored while already waiting for this connection.</summary>
        public void Connect(double now){if(Waiting)return;State=ServerCrucible.Unknown;Waiting=true;started=now;}
        public void Hello(){State=ServerCrucible.Present;Waiting=false;}
        public void Disconnect(){State=ServerCrucible.Unknown;Waiting=false;}
        public ServerCrucible Tick(double now){if(Waiting&&now-started>=TimeoutSeconds){State=ServerCrucible.Absent;Waiting=false;}return State;}
        /// <summary>The presence a product should use: authority roles are their own server; a server epoch proves Crucible.</summary>
        public static ServerCrucible For(SessionRole role,ServerCrucible timerState,bool authorityReady){
            if(role==SessionRole.Menu)return ServerCrucible.Unknown;
            if(role!=SessionRole.RemoteClient||authorityReady)return ServerCrucible.Present;
            return timerState;
        }
    }
}
