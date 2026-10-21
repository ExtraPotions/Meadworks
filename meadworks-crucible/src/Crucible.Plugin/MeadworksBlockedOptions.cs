using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// The one blocked-option service products call (C03). <see cref="Query"/> before acting; <see cref="TryUse"/> also
    /// tells the player why once per option per session (center message, or the log on a headless server);
    /// <see cref="Track"/> keeps configuration-manager entries read-only while the gate blocks them. Main thread only.
    /// </summary>
    public static class MeadworksBlockedOptions {
        static readonly BlockedOptionNotices notices=new BlockedOptionNotices();
        static readonly List<Tracked> tracked=new List<Tracked>();
        static readonly FieldInfo? TagsField=AccessTools.Field(typeof(ConfigDescription),"<Tags>k__BackingField");
        static readonly ConfigurationManagerAttributes ManagerMarker=new ConfigurationManagerAttributes();
        static readonly ReadOnlyTag CharterMarker=new ReadOnlyTag();
        static MeadworksLog? log;
        // Tracked entries are re-evaluated twice per second, a slice per frame, so no single frame walks every entry.
        static readonly SweepSlicer sweep=new SweepSlicer(0.5f,32);

        /// <summary>Language for player texts: the game's selected language.</summary>
        public static string Language=>MeadworksTranslations.GameLanguage();

        /// <summary>
        /// Whether the player may act on this option right now, with the reason when not. Cheats are admins only unless
        /// the admin let players use that option (<see cref="MeadworksMischief.PlayersMay(string,string)"/>);
        /// <see cref="QueryEdit"/> answers whether this player may change it.
        /// </summary>
        public static BlockedOption Query(string productId,string option,ConfigScope scope,bool serverLocked=true){
            var decision=MeadworksAuthority.Decide(productId,scope,serverLocked);
            return scope==ConfigScope.Cheat&&MeadworksMischief.PlayersMay(productId,option)
                ?BlockedOption.ForUse(productId,option,decision):BlockedOption.From(productId,option,decision);
        }

        /// <summary>Whether this player may change the option (admin, host or single player for synchronized scopes).</summary>
        public static BlockedOption QueryEdit(string productId,string option,ConfigScope scope,bool serverLocked=true)=>
            BlockedOption.From(productId,option,MeadworksAuthority.Decide(productId,scope,serverLocked));

        /// <summary><see cref="QueryEdit"/>, then tell the player when they may not change it (rate limited).</summary>
        public static bool TryEdit(string productId,string option,ConfigScope scope,string? label=null,bool serverLocked=true){
            var status=QueryEdit(productId,option,scope,serverLocked);
            Notify(status,label);
            return status.Allowed;
        }

        /// <summary>Localized reason text for the current game language; empty for None.</summary>
        public static string Reason(ConfigBlockReason reason)=>BlockedOptionText.Reason(reason,Language);

        /// <summary>
        /// Query, then tell the player when blocked (rate limited). Returns true when the product may act.
        /// <paramref name="label"/> is the player-facing option name (already localized); defaults to the option key.
        /// </summary>
        public static bool TryUse(string productId,string option,ConfigScope scope,string? label=null,bool serverLocked=true){
            var status=Query(productId,option,scope,serverLocked);
            Notify(status,label);
            return status.Allowed;
        }

        /// <summary>Shows the status to the player once per option per session; an allowed status re-arms the option.</summary>
        public static void Notify(BlockedOption status,string? label=null){
            if(status==null)throw new ArgumentNullException(nameof(status));
            var generation=Plugin.IsReady?Plugin.Session.Generation:0;
            if(notices.Observe(generation,status.Product,status.Option,status.Reason)!=NoticeOutcome.Show)return;
            var text=status.Message(string.IsNullOrWhiteSpace(label)?status.Option:label!,Language);
            Log.Info(status.ToString());
            if(MeadworksHeadless.Channel==NoticeChannel.Hud)MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,text);
            else Log.Info(text);
        }

        /// <summary>
        /// Keeps <paramref name="entry"/> read-only in configuration managers while the gate blocks it (BepInEx
        /// ConfigurationManager <c>ReadOnly</c> attribute convention plus a Charter-style marker). Entries sharing the
        /// empty description are skipped, as in Charter: the tag would leak to every such entry.
        /// </summary>
        public static void Track(string productId,ConfigEntryBase entry,ConfigScope scope,bool serverLocked=true){
            if(entry==null)throw new ArgumentNullException(nameof(entry));
            MeadworksConfig.RecordTracking(entry,productId,scope,serverLocked);
            var item=new Tracked(productId,entry,scope,serverLocked);
            tracked.Add(item);Refresh(item);
        }
        public static void Untrack(ConfigEntryBase entry){
            for(int i=tracked.Count-1;i>=0;i--)if(ReferenceEquals(tracked[i].Entry,entry)){Apply(tracked[i],false);tracked.RemoveAt(i);sweep.Removed(i);}
            MeadworksConfig.EndTracking(entry);
        }

        /// <summary>Called from the Crucible plugin's Update; re-evaluates every tracked entry twice per second, spread over frames.</summary>
        internal static void Update(float now,float deltaTime){
            int count=sweep.Take(now,deltaTime,tracked.Count,out var start);
            for(int i=start;i<start+count;i++)Refresh(tracked[i]);
        }
        internal static void Shutdown(){foreach(var item in tracked){Apply(item,false);MeadworksConfig.EndTracking(item.Entry);}tracked.Clear();notices.Reset();}

        static MeadworksLog Log=>log??=MeadworksLog.For(Plugin.Log??BepInEx.Logging.Logger.CreateLogSource("Meadworks"),BlockedOptionText.Product);

        static void Refresh(Tracked item){
            bool readOnly;
            // Same answer as Query(...).ReadOnly without building a BlockedOption: names are fixed per entry and checked once.
            try{var allowed=MeadworksAuthority.Decide(item.Product,item.Scope,item.ServerLocked).Allowed;readOnly=item.ValidNames&&!allowed;}
            catch(ArgumentException){readOnly=false;}
            if(ConfigTags.Changed(item.Applied,readOnly))Apply(item,readOnly);
        }
        static void Apply(Tracked item,bool readOnly){
            var description=item.Entry.Description;
            item.Applied=readOnly;
            if(TagsField==null||description==null||ReferenceEquals(description,ConfigDescription.Empty))return;
            var tags=ConfigTags.Apply(description.Tags,CharterMarker,readOnly);
            TagsField.SetValue(description,ConfigTags.Apply(tags,ManagerMarker,readOnly));
        }

        sealed class Tracked {
            public readonly string Product;public readonly ConfigEntryBase Entry;public readonly ConfigScope Scope;public readonly bool ServerLocked;public readonly bool ValidNames;public bool Applied;
            public Tracked(string product,ConfigEntryBase entry,ConfigScope scope,bool serverLocked){Product=product;Entry=entry;Scope=scope;ServerLocked=serverLocked;ValidNames=BlockedOptionNotices.IsValidName(product)&&BlockedOptionNotices.IsValidName(entry.Definition.Key);}
        }

        /// <summary>
        /// ConfigurationManager copies non-null fields from any tag whose type is named ConfigurationManagerAttributes;
        /// only ReadOnly is set, so the product's own attributes (order, category, admin-only) stay in effect.
        /// </summary>
        sealed class ConfigurationManagerAttributes { public bool? ReadOnly=true; }
        /// <summary>Charter-style read-only tag: managers that read tag objects by field name see ReadOnly.</summary>
        sealed class ReadOnlyTag { public bool ReadOnly=true; }
    }
}
