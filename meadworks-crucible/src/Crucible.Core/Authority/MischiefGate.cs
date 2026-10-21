using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>The fixed cheat categories of the Mischief gate (Hex design section 6.2). Every cheat carries one.</summary>
    public enum MischiefCategory {
        /// <summary>Affects only the user: god mode, speed, size.</summary>
        Personal,
        /// <summary>Creates or saves materials: spawning, craft speed, loot multipliers, free build.</summary>
        Economy,
        /// <summary>Changes the shared world: summoned weather, unrestricted growth.</summary>
        World,
        /// <summary>Reveals information: ESP, map reveal, points of interest.</summary>
        Information,
        /// <summary>Moves players: teleport, portals.</summary>
        Travel,
        /// <summary>Progression: skills, boss powers, achievements, guaranteed upgrades.</summary>
        Progress,
    }

    /// <summary>Who may use a cheat category on a server. Single player and the host always may.</summary>
    public enum MischiefAccess { Nobody, Admins, Listed, Everyone }

    public static class MischiefCategories {
        public static IReadOnlyList<MischiefCategory> All { get; }=new[]{MischiefCategory.Personal,MischiefCategory.Economy,MischiefCategory.World,MischiefCategory.Information,MischiefCategory.Travel,MischiefCategory.Progress};
        /// <summary>Category assumed for a cheat bound without one: the strictest, so untagged cheats are never looser.</summary>
        public const MischiefCategory Untagged=MischiefCategory.World;
        /// <summary>Parses a category name (case-insensitive); numbers are refused so settings stay readable.</summary>
        public static bool TryParse(string? text,out MischiefCategory category){
            category=Untagged;var t=text?.Trim();
            if(string.IsNullOrEmpty(t)||char.IsDigit(t![0])||t[0]=='-')return false;
            return Enum.TryParse(t,true,out category)&&Enum.IsDefined(typeof(MischiefCategory),category);
        }
        internal static void Validate(MischiefCategory category){if(!Enum.IsDefined(typeof(MischiefCategory),category))throw new ArgumentException("Unknown cheat category.",nameof(category));}
    }

    /// <summary>
    /// The policy's Listed Players: platform user IDs as the game writes them (<c>Steam_76561198...</c>, <c>Xbox_...</c>).
    /// As in the game's admin list (<c>ZNet.ListContainsId</c>), a bare number means a Steam ID. Bounded and immutable.
    /// </summary>
    public sealed class MischiefPlayers {
        public const int MaxEntries=512;
        const string Steam="Steam";
        readonly HashSet<string> ids;readonly string[] ordered;
        MischiefPlayers(IEnumerable<string> normalized){ordered=normalized.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();ids=new HashSet<string>(ordered,StringComparer.Ordinal);}
        public static MischiefPlayers Empty { get; }=new MischiefPlayers(Array.Empty<string>());
        public int Count=>ordered.Length;
        public IReadOnlyList<string> Ids=>ordered;

        /// <summary>Canonical form <c>Platform_user</c>, Steam's platform name normalized; null when not an ID.</summary>
        public static string? Normalize(string? id){
            var t=id?.Trim();
            if(string.IsNullOrEmpty(t)||t!.Length>96)return null;
            foreach(var c in t)if(!(char.IsLetterOrDigit(c)||c=='_'||c=='-'||c=='.'))return null;
            int split=t.IndexOf('_');
            if(split<0)return t.All(char.IsDigit)?Steam+"_"+t:null;
            var platform=t.Substring(0,split);var user=t.Substring(split+1);
            if(platform.Length==0||user.Length==0)return null;
            if(string.Equals(platform,Steam,StringComparison.OrdinalIgnoreCase))platform=Steam;
            return platform+"_"+user;
        }

        /// <summary>Reads a list separated by commas, semicolons or new lines; invalid and overflowing entries are reported.</summary>
        public static MischiefPlayers Parse(string? text,out IReadOnlyList<string> rejected){
            var bad=new List<string>();var good=new List<string>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var raw in (text??"").Split(new[]{',',';','\n','\r'},StringSplitOptions.RemoveEmptyEntries)){
                var entry=raw.Trim();if(entry.Length==0)continue;
                var id=Normalize(entry);
                if(id==null){bad.Add(entry);continue;}
                if(seen.Contains(id))continue;
                if(good.Count>=MaxEntries){bad.Add(entry);continue;}
                seen.Add(id);good.Add(id);
            }
            rejected=bad;return good.Count==0?Empty:new MischiefPlayers(good);
        }

        public bool Contains(string? platformId){var id=Normalize(platformId);return id!=null&&ids.Contains(id);}
        /// <summary>Normalized, ordered, comma-separated: the form written back to the setting.</summary>
        public string Format()=>string.Join(",",ordered);
        public override string ToString()=>Format();
    }

    /// <summary>
    /// The authority's cheat policy: one <see cref="MischiefAccess"/> per category plus the Listed Players. Default:
    /// every category admins only (user decision, Hex design Q2; roster decision 1). Immutable; read from settings
    /// with <see cref="FromSettings"/>.
    /// </summary>
    public sealed class MischiefPolicy {
        public const MischiefAccess DefaultAccess=MischiefAccess.Admins;
        public const string Section="Mischief";
        public const string ListedKey="ListedPlayers";
        readonly MischiefAccess[] access;
        public MischiefPlayers Listed { get; }
        MischiefPolicy(MischiefAccess[] access,MischiefPlayers listed){this.access=access;Listed=listed;}
        public static MischiefPolicy Default { get; }=new MischiefPolicy(MischiefCategories.All.Select(_=>DefaultAccess).ToArray(),MischiefPlayers.Empty);

        public MischiefAccess Access(MischiefCategory category){MischiefCategories.Validate(category);return access[(int)category];}
        public MischiefPolicy With(MischiefCategory category,MischiefAccess value){
            MischiefCategories.Validate(category);
            if(!Enum.IsDefined(typeof(MischiefAccess),value))throw new ArgumentException("Unknown access level.",nameof(value));
            var copy=(MischiefAccess[])access.Clone();copy[(int)category]=value;return new MischiefPolicy(copy,Listed);
        }
        public MischiefPolicy WithListed(MischiefPlayers listed)=>new MischiefPolicy(access,listed??throw new ArgumentNullException(nameof(listed)));

        /// <summary>Setting key of a category inside <see cref="Section"/>: its name.</summary>
        public static string SettingKey(MischiefCategory category){MischiefCategories.Validate(category);return category.ToString();}

        /// <summary>
        /// Builds the policy from setting values (<paramref name="read"/> returns the text of a key, or null). Invalid
        /// or missing values fall back to <see cref="DefaultAccess"/> and are reported, never loosened.
        /// </summary>
        public static MischiefPolicy FromSettings(Func<string,string?> read,string? listed,out IReadOnlyList<string> invalid){
            if(read==null)throw new ArgumentNullException(nameof(read));
            var bad=new List<string>();var values=new MischiefAccess[MischiefCategories.All.Count];
            foreach(var c in MischiefCategories.All){
                var text=read(SettingKey(c))?.Trim();var value=DefaultAccess;
                if(!string.IsNullOrEmpty(text)){
                    if(!char.IsDigit(text![0])&&Enum.TryParse(text,true,out MischiefAccess parsed)&&Enum.IsDefined(typeof(MischiefAccess),parsed))value=parsed;
                    else bad.Add(SettingKey(c)+"="+text);
                }
                values[(int)c]=value;
            }
            var players=MischiefPlayers.Parse(listed,out var rejected);
            foreach(var r in rejected)bad.Add(ListedKey+": "+r);
            invalid=bad;return new MischiefPolicy(values,players);
        }

        /// <summary>
        /// Whether the policy grants a category to a remote player. Admins are always on the list; <c>Nobody</c> refuses
        /// admins too (single player and host never reach this). Refusals: admins only gives <see cref="ConfigBlockReason.NotAdmin"/>,
        /// otherwise <see cref="ConfigBlockReason.MischiefDenied"/>.
        /// </summary>
        public bool Grants(MischiefCategory category,bool isAdmin,string? platformId,out ConfigBlockReason reason){
            reason=ConfigBlockReason.None;
            switch(Access(category)){
                case MischiefAccess.Everyone:return true;
                case MischiefAccess.Listed:if(isAdmin||Listed.Contains(platformId))return true;reason=ConfigBlockReason.MischiefDenied;return false;
                case MischiefAccess.Admins:if(isAdmin)return true;reason=ConfigBlockReason.NotAdmin;return false;
                default:reason=ConfigBlockReason.MischiefDenied;return false;
            }
        }

        public override string ToString()=>string.Join(" ",MischiefCategories.All.Select(c=>c+"="+access[(int)c]))+" listed="+Listed.Count;
    }

    /// <summary>The gate's answer for one cheat category: C02 first, then the server's policy.</summary>
    public sealed class MischiefDecision {
        public MischiefCategory Category { get; }
        public bool Allowed { get; }
        public ConfigBlockReason Reason { get; }
        /// <summary>Server value when granted on a server, the player's own on an authority role, Locked (safe value) when refused.</summary>
        public ConfigValueSource Source { get; }
        /// <summary>Editing the cheat's value goes to the server (admins only); a policy grant lets a player use the value, not change it.</summary>
        public bool RequiresServerAmendment { get; }
        public bool ProductActive { get; }
        /// <summary>The policy's access for the category; null when the policy was not consulted (authority role or C02 refused first).</summary>
        public MischiefAccess? Access { get; }
        /// <summary>The C02 decision for the Cheat scope this answer started from.</summary>
        public AuthorityDecision Authority { get; }
        internal MischiefDecision(MischiefCategory category,bool allowed,ConfigBlockReason reason,ConfigValueSource source,bool amend,bool active,MischiefAccess? access,AuthorityDecision authority){
            Category=category;Allowed=allowed;Reason=reason;Source=source;RequiresServerAmendment=amend;ProductActive=active;Access=access;Authority=authority;
        }
        public T Effective<T>(ScopedValue<T> value)=>(value??throw new ArgumentNullException(nameof(value))).Effective(Source);
        public string Message=>AuthorityGate.Describe(Reason);
        public override string ToString()=>Category+": "+(Allowed?"Allowed":"Blocked:"+Reason)+" source="+Source+(Access.HasValue?" policy="+Access.Value:"")+(RequiresServerAmendment?" amend":"")+(ProductActive?"":" product-off");
    }

    /// <summary>
    /// The Mischief gate (C13, Hex design section 6): which players may use which cheats. Every product cheat asks it,
    /// with or without Hex installed. Order: the C02 <see cref="AuthorityGate"/> for the Cheat scope decides roles,
    /// handshake, vanilla server and product activity; single player, host and dedicated server are always allowed
    /// (the player or server is the authority). On a remote client with a Crucible authority the server's
    /// <see cref="MischiefPolicy"/> replaces C02's admin check. Servers evaluate peer requests with <see cref="DecideForPeer"/>.
    /// </summary>
    public static class MischiefGate {
        public static MischiefDecision Decide(MischiefCategory category,AuthorityFacts facts,MischiefPolicy policy,string? platformId=null){
            MischiefCategories.Validate(category);
            if(facts==null)throw new ArgumentNullException(nameof(facts));
            if(policy==null)throw new ArgumentNullException(nameof(policy));
            var authority=AuthorityGate.Decide(ConfigScope.Cheat,facts);
            if(facts.Role!=SessionRole.RemoteClient)return new MischiefDecision(category,authority.Allowed,authority.Reason,authority.Source,authority.RequiresServerAmendment,authority.ProductActive,null,authority);
            if(!authority.Allowed&&authority.Reason!=ConfigBlockReason.NotAdmin)
                return new MischiefDecision(category,false,authority.Reason,ConfigValueSource.Locked,false,authority.ProductActive,null,authority);
            var access=policy.Access(category);
            if(!policy.Grants(category,facts.IsAdmin,platformId,out var refused))
                return new MischiefDecision(category,false,refused,ConfigValueSource.Locked,false,authority.ProductActive,access,authority);
            return new MischiefDecision(category,true,ConfigBlockReason.None,ConfigValueSource.Server,authority.Allowed&&authority.RequiresServerAmendment,authority.ProductActive,access,authority);
        }

        /// <summary>The server's check of a connected peer's request for an action the server executes.</summary>
        public static MischiefDecision DecideForPeer(MischiefCategory category,MischiefPolicy policy,bool peerIsAdmin,string? peerPlatformId)=>
            Decide(category,new AuthorityFacts(SessionRole.RemoteClient,true,peerIsAdmin,ServerCrucible.Present,PeerClass.RequiredOnAllPeers,ServerProduct.Present),policy,peerPlatformId);
    }

    /// <summary>Which category each bound cheat carries (owner plus setting key). Untagged cheats count as World and are reported.</summary>
    public sealed class MischiefCheats {
        readonly object gate=new object();
        readonly Dictionary<(string Owner,string Key),MischiefCategory?> tags=new Dictionary<(string,string),MischiefCategory?>();
        public int Count{get{lock(gate)return tags.Count;}}
        public void Tag(string owner,string key,MischiefCategory? category){
            if(string.IsNullOrWhiteSpace(owner))throw new ArgumentException("Expected an owner.",nameof(owner));
            if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Expected a setting key.",nameof(key));
            if(category.HasValue)MischiefCategories.Validate(category.Value);
            lock(gate)tags[(owner,key)]=category;
        }
        /// <summary>Whether a cheat with this owner and key was tagged (bound through BindCheat or tagged by its product).</summary>
        public bool Contains(string owner,string key){lock(gate)return tags.ContainsKey((owner??"",key??""));}
        public MischiefCategory CategoryOf(string owner,string key){lock(gate)return tags.TryGetValue((owner??"",key??""),out var c)&&c.HasValue?c.Value:MischiefCategories.Untagged;}
        /// <summary>One line per cheat bound without a category, ordered.</summary>
        public IReadOnlyList<string> Review(){
            lock(gate)return tags.Where(t=>!t.Value.HasValue).Select(t=>CheatDefaults.ReviewCategory(t.Key.Owner+" "+t.Key.Key,null)!).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
    }
}
