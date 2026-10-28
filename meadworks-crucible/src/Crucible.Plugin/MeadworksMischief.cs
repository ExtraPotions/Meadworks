using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// The Mischief gate (C13) for products. Crucible owns the policy as ServerAuthoritative settings in section
    /// <c>Mischief</c> (one access level per category, default Admins; <c>ListedPlayers</c>), bound through
    /// <see cref="MeadworksConfig"/> so Jotunn mirrors the server's policy to every client. Products ask
    /// <see cref="Allows"/> before a cheat takes effect, whether or not Hex is installed; servers check peer requests
    /// with <see cref="DecideForPeer"/>. Main thread only.
    /// </summary>
    public static class MeadworksMischief {
        static readonly Dictionary<MischiefCategory,ConfigEntry<MischiefAccess>> access=new Dictionary<MischiefCategory,ConfigEntry<MischiefAccess>>();
        static ConfigEntry<string>? listed,playerCheats;
        static MischiefPolicy? cached;
        static MeadworksLog? log;
        static readonly System.Reflection.FieldInfo? UserIdField=HarmonyLib.AccessTools.Field(typeof(UserInfo),"UserId");
        /// <summary>Category tags of every cheat bound through <see cref="MeadworksConfig.BindCheat{T}(ConfigFile,string,string,T,string,AcceptableValueBase)"/>.</summary>
        public static MischiefCheats Cheats { get; }=new MischiefCheats();

        internal static void Bind(ConfigFile config,ManualLogSource logger){
            log=MeadworksLog.For(logger,"Crucible");
            foreach(var category in MischiefCategories.All){
                var entry=MeadworksConfig.Bind(config,MischiefPolicy.Section,MischiefPolicy.SettingKey(category),MischiefPolicy.DefaultAccess,
                    "Who may use "+category+" cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. "+Describe(category)+" Crucible owns this server setting; its value applies to joining clients.",ConfigScope.ServerAuthoritative);
                entry.SettingChanged+=Changed;access[category]=entry;
            }
            listed=MeadworksConfig.Bind(config,MischiefPolicy.Section,MischiefPolicy.ListedKey,"",
                "Platform user IDs granted Listed cheat categories, separated by commas (for example Steam_76561198000000000; a bare number means a Steam ID, as in adminlist.txt). Crucible owns this server setting; its value applies to joining clients.",ConfigScope.ServerAuthoritative);
            listed.SettingChanged+=Changed;
            playerCheats=MeadworksConfig.BindHidden(config,MischiefPolicy.Section,"Players May Use","",
                "Managed by the MeadWorks menus: cheat settings the admin lets every player use (config|Section.Key, separated by commas). Cheats not listed stay admins only.",ConfigScope.ServerAuthoritative);
            Changed(null,EventArgs.Empty);
        }

        static string Describe(MischiefCategory category){
            switch(category){
                case MischiefCategory.Personal:return "Personal: affects only the user (god mode, speed, size).";
                case MischiefCategory.Economy:return "Economy: creates or saves materials (spawning, craft speed, loot multipliers, free build).";
                case MischiefCategory.World:return "World: changes the shared world (weather, unrestricted growth).";
                case MischiefCategory.Information:return "Information: reveals what is hidden (ESP, map reveal, points of interest).";
                case MischiefCategory.Travel:return "Travel: moves players (teleport, portals).";
                default:return "Progress: skills, boss powers, achievements, guaranteed upgrades.";
            }
        }

        static void Changed(object? sender,EventArgs args){
            var policy=MischiefPolicy.FromSettings(Read,listed?.Value,out var invalid);
            cached=policy;
            if(invalid.Count>0)log?.Warning("Mischief policy: ignored invalid values ("+string.Join(", ",invalid)+"); those categories stay admins only.");
            log?.Verbose("Mischief policy: "+policy);
        }
        static string? Read(string key){foreach(var pair in access)if(MischiefPolicy.SettingKey(pair.Key)==key)return pair.Value.Value.ToString();return null;}

        /// <summary>The policy in effect: on a client, the server's (mirrored with config); default admins only before binding.</summary>
        public static MischiefPolicy Policy=>cached??MischiefPolicy.Default;

        /// <summary>This player's platform user ID (<c>Steam_...</c>), or null when unknown.</summary>
        public static string? LocalPlatformId(){
            if(platformId!=null)return platformId;
            // UserInfo.UserId is a Splatform PlatformUserID; read it as object so Crucible needs no Splatform reference.
            // shortcut: the platform user is fixed for the process, so the first known ID is kept (each read built a UserInfo, a boxed ID and a string); re-read per session if a platform ever switches users in-process.
            try{var user=UserInfo.GetLocalUser();return platformId=user==null?null:UserIdField?.GetValue(user)?.ToString();}catch(Exception){return null;}
        }
        static string? platformId;
        static readonly Func<string?> readPlatformId=LocalPlatformId;
        static readonly MischiefMemo memo=new MischiefMemo();

        /// <summary>The gate's decision for the local player (reused while the session, policy and platform ID are unchanged).</summary>
        public static MischiefDecision Decide(string productId,MischiefCategory category)=>memo.Decide(productId,category,MeadworksAuthority.Facts(productId),Policy,readPlatformId);

        /// <summary>Whether the local player may use a cheat of this category now; the reason (C03 text) when not.</summary>
        public static bool Allows(string productId,MischiefCategory category,out ConfigBlockReason reason){
            var decision=Decide(productId,category);reason=decision.Reason;return decision.Allowed;
        }

        /// <summary>The id a cheat setting has in the Players May Use list: its config owner, section and key.</summary>
        public static string PlayerCheatId(ConfigEntryBase entry)=>OwnerOf(entry.ConfigFile)+"|"+entry.Definition.Section+"."+entry.Definition.Key;
        static string[] PlayerCheatIds()=>(playerCheats?.Value??"").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// Whether the server's admin let players use this cheat setting (default no: admins only). Single player and
        /// the host always may use their cheats regardless.
        /// </summary>
        public static bool PlayersMay(ConfigEntryBase entry)=>entry!=null&&Array.IndexOf(PlayerCheatIds(),PlayerCheatId(entry))>=0;
        /// <summary><see cref="PlayersMay(ConfigEntryBase)"/> for a product's option id (<c>Section.Key</c>).</summary>
        public static bool PlayersMay(string productId,string option)=>Array.IndexOf(PlayerCheatIds(),productId+"|"+option)>=0;

        /// <summary>
        /// Lets players use this cheat setting, or makes it admin-only again. Only someone who may change server settings
        /// (host, admin, single player) can; returns false otherwise.
        /// </summary>
        public static bool SetPlayersMay(ConfigEntryBase entry,bool allowed){
            if(entry==null||playerCheats==null)return false;
            if(PlayersMay(entry)==allowed)return true;
            var ids=new List<string>(PlayerCheatIds());var id=PlayerCheatId(entry);
            if(allowed)ids.Add(id);else ids.Remove(id);
            ids.Sort(StringComparer.Ordinal);
            return MeadworksConfig.Amend(playerCheats,string.Join(",",ids)).Applied;
        }
        /// <summary>The setting that holds the Players May Use list (raised when it changes, for menus to refresh).</summary>
        public static ConfigEntry<string>? PlayerCheats=>playerCheats;

        /// <summary>Whether the setting is a cheat (bound through BindCheat or tagged by its product), with its category.</summary>
        public static bool IsCheat(ConfigEntryBase entry,out MischiefCategory category){
            category=MischiefCategories.Untagged;
            if(entry?.ConfigFile==null)return false;
            string owner=OwnerOf(entry.ConfigFile),key=entry.Definition.Section+"."+entry.Definition.Key;
            if(!Cheats.Contains(owner,key))return false;
            category=Cheats.CategoryOf(owner,key);return true;
        }

        /// <summary>Whether the local player may use this cheat setting: the category policy grants it, or the admin let players use it.</summary>
        public static bool AllowsCheat(ConfigEntryBase entry,string productId,MischiefCategory category)=>
            Decide(productId,category).Allowed||PlayersMay(entry)&&MeadworksAuthority.Decide(productId,ConfigScope.Cheat).MayUse;

        /// <summary>Server side: <see cref="DecideForPeer"/>, or the admin let players use this cheat setting.</summary>
        public static bool AllowsCheatForPeer(ZRpc? rpc,ConfigEntryBase entry,MischiefCategory category)=>
            PlayersMay(entry)||DecideForPeer(rpc,category).Allowed;

        /// <summary>The cheat's value to act on: the entry's value when allowed, else <paramref name="safe"/>.</summary>
        public static T Effective<T>(ConfigEntry<T> entry,string productId,MischiefCategory category,T safe){
            if(entry==null)throw new ArgumentNullException(nameof(entry));
            return AllowsCheat(entry,productId,category)?entry.Value:safe;
        }

        /// <summary>C03 status of a cheat for configuration managers and menus.</summary>
        public static BlockedOption Query(string productId,string cheat,MischiefCategory category)=>memo.Query(productId,cheat,Decide(productId,category));

        /// <summary>Asks the gate and tells the player why when refused (once per cheat and session, C03 rate limit).</summary>
        public static bool TryUse(string productId,string cheat,MischiefCategory category,string? label=null){
            var status=Query(productId,cheat,category);
            MeadworksBlockedOptions.Notify(status,label);
            return status.Allowed;
        }

        /// <summary>
        /// Server side: may this peer use the category for an action the server executes? The peer's admin status and
        /// platform ID come from the connection (<c>ZNet.IsAdmin(socket host name)</c>). Refuses when this machine is not
        /// the server or the peer is unknown.
        /// </summary>
        public static MischiefDecision DecideForPeer(ZRpc? rpc,MischiefCategory category){
            var net=ZNet.instance;
            string? id=null;bool admin=false;
            if(net!=null&&net.IsServer()&&rpc!=null&&rpc.IsConnected()){id=rpc.GetSocket()?.GetHostName();admin=!string.IsNullOrEmpty(id)&&net.IsAdmin(id);}
            if(id==null)return MischiefGate.Decide(category,new AuthorityFacts(SessionRole.RemoteClient,false,false,ServerCrucible.Unknown,PeerClass.RequiredOnAllPeers),Policy);
            return MischiefGate.DecideForPeer(category,Policy,admin,id);
        }

        internal static string OwnerOf(ConfigFile config)=>Path.GetFileNameWithoutExtension(config.ConfigFilePath)??"";

        /// <summary>One diagnostics line: the policy and any cheats bound without a category.</summary>
        public static string Diagnostics(){
            var review=Cheats.Review();
            return "Mischief "+Policy+(review.Count==0?"":" | untagged: "+review.Count);
        }
    }
}
