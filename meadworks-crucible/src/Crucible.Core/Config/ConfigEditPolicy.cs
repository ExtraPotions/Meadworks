using System;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Whether the connected server runs Crucible. Filled by the handshake; Absent only after the authority gate times out.</summary>
    public enum ServerCrucible { Unknown, Present, Absent }

    /// <summary>Why an edit is refused; products show a localized "disabled by server" message per reason.</summary>
    public enum ConfigBlockReason { None, AuthorityPending, NotAdmin, ServerLocked, VanillaServer, ProductDisabled, ServerOnly, MischiefDenied }

    /// <summary>Which value is in effect for a setting right now.</summary>
    public enum ConfigValueSource {
        /// <summary>The player's own value from their config file.</summary>
        Own,
        /// <summary>The server's value; the player's own value is kept untouched and restored on disconnect.</summary>
        Server,
        /// <summary>The setting's safe value (cheats off) because no authority can vouch for it.</summary>
        Locked,
    }

    public sealed class ConfigAuthorityFacts {
        public SessionRole Role { get; }
        public bool AuthorityReady { get; }
        public bool IsAdmin { get; }
        public ServerCrucible Server { get; }
        public ConfigAuthorityFacts(SessionRole role,bool authorityReady,bool isAdmin,ServerCrucible server){Role=role;AuthorityReady=authorityReady;IsAdmin=isAdmin;Server=server;}
        public static ConfigAuthorityFacts From(SessionSnapshot session,ServerCrucible server){
            if(session==null)throw new ArgumentNullException(nameof(session));
            return new ConfigAuthorityFacts(session.Role,session.AuthorityReady,session.IsAdmin,server);
        }
        /// <summary>
        /// Best inference without the authority gate: authority roles are their own server; a remote client with a
        /// server epoch has proven Crucible. A vanilla server stays Unknown (fail closed) until C02 reports Absent.
        /// </summary>
        public static ServerCrucible InferServer(SessionRole role,bool authorityReady)=>role!=SessionRole.RemoteClient||authorityReady?ServerCrucible.Present:ServerCrucible.Unknown;
    }

    public sealed class ConfigEditDecision {
        public bool Allowed { get; }
        public ConfigBlockReason Reason { get; }
        public ConfigValueSource Source { get; }
        /// <summary>The edit changes the server's value, so it must be sent to the server rather than written locally.</summary>
        public bool RequiresServerAmendment { get; }
        ConfigEditDecision(bool allowed,ConfigBlockReason reason,ConfigValueSource source,bool amend){Allowed=allowed;Reason=reason;Source=source;RequiresServerAmendment=amend;}
        internal static ConfigEditDecision Edit(ConfigValueSource source,bool amend=false)=>new ConfigEditDecision(true,ConfigBlockReason.None,source,amend);
        internal static ConfigEditDecision Block(ConfigBlockReason reason,ConfigValueSource source)=>new ConfigEditDecision(false,reason,source,false);
        public override string ToString()=>(Allowed?"Allowed":"Blocked:"+Reason)+" source="+Source+(RequiresServerAmendment?" amend":"");
    }

    /// <summary>
    /// The consolidated edit-permission rule (roster decision 1). Single player, host, dedicated server and the main
    /// menu edit their own file: that file is the authority when this machine runs the world. A remote client edits
    /// Local settings freely; ServerAuthoritative settings follow the server (admins amend them, others see why not,
    /// an unlocked client-optional setting stays personal); Cheat settings are always server-bound and locked to their
    /// safe value while no Crucible authority vouches for them, including on vanilla servers.
    /// </summary>
    public static class ConfigEditPolicy {
        /// <param name="serverLocked">
        /// For ServerAuthoritative settings of client-optional products: whether the server binds this setting
        /// (Charter "binding"). Required-on-all-peers products always pass true. Ignored for Local and Cheat.
        /// </param>
        public static ConfigEditDecision Decide(ConfigScope scope,ConfigAuthorityFacts facts,bool serverLocked=true){
            if(!Enum.IsDefined(typeof(ConfigScope),scope))throw new ArgumentException("Unknown configuration scope.",nameof(scope));
            if(facts==null)throw new ArgumentNullException(nameof(facts));
            if(scope==ConfigScope.Local||facts.Role!=SessionRole.RemoteClient)return ConfigEditDecision.Edit(ConfigValueSource.Own);
            bool cheat=scope==ConfigScope.Cheat;
            if(facts.Server==ServerCrucible.Absent)return cheat?ConfigEditDecision.Block(ConfigBlockReason.VanillaServer,ConfigValueSource.Locked):ConfigEditDecision.Edit(ConfigValueSource.Own);
            if(!facts.AuthorityReady)return ConfigEditDecision.Block(ConfigBlockReason.AuthorityPending,cheat?ConfigValueSource.Locked:ConfigValueSource.Own);
            if(!cheat&&!serverLocked)return ConfigEditDecision.Edit(ConfigValueSource.Own);
            if(facts.IsAdmin)return ConfigEditDecision.Edit(ConfigValueSource.Server,amend:true);
            return ConfigEditDecision.Block(cheat?ConfigBlockReason.NotAdmin:ConfigBlockReason.ServerLocked,ConfigValueSource.Server);
        }
    }

    /// <summary>
    /// A setting's own and server values kept apart (Charter Clause model): server values are applied in memory only,
    /// never written over the player's file, and dropping them restores the player's own value.
    /// </summary>
    public sealed class ScopedValue<T> {
        T server=default!;
        public ConfigScope Scope { get; }
        public T Own { get; private set; }
        /// <summary>Value used when the source is Locked (cheats: off).</summary>
        public T Safe { get; }
        public bool HasServerValue { get; private set; }
        public ScopedValue(ConfigScope scope,T own,T safe){
            if(!Enum.IsDefined(typeof(ConfigScope),scope))throw new ArgumentException("Unknown configuration scope.",nameof(scope));
            Scope=scope;Own=own;Safe=safe;
        }
        public void SetOwn(T value)=>Own=value;
        public void ReceiveServer(T value){if(Scope==ConfigScope.Local)throw new InvalidOperationException("Local settings never take server values.");server=value;HasServerValue=true;}
        public void ClearServer(){server=default!;HasServerValue=false;}
        public T Effective(ConfigValueSource source){
            switch(source){
                case ConfigValueSource.Locked:return Safe;
                case ConfigValueSource.Server:return HasServerValue?server:Scope==ConfigScope.Cheat?Safe:Own;
                default:return Own;
            }
        }
        public T Effective(ConfigEditDecision decision)=>Effective((decision??throw new ArgumentNullException(nameof(decision))).Source);
    }
}
