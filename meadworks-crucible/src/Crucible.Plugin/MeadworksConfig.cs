using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using Jotunn.Extensions;
using Jotunn.Managers;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Outcome of <see cref="MeadworksConfig.Amend{T}"/>: the route taken, the gate's decision, and whether the change was applied and (for Publish and Server) handed to the synchronization layer.</summary>
    public sealed class ConfigAmendResult {
        public AmendRoute Route { get; }
        public AuthorityDecision Decision { get; }
        public bool Applied { get; }
        internal ConfigAmendResult(AmendRoute route,AuthorityDecision decision,bool applied){Route=route;Decision=decision;Applied=applied;}
        public override string ToString()=>Route+(Applied?" applied":" not applied")+" ("+Decision+")";
    }

    /// <summary>
    /// The bridge to Jotunn's synchronized configuration (C01 design: Crucible's API, Charter's semantics, Jotunn's
    /// transport): caches the current values, applies a change, then sends only the difference the way Jotunn does when
    /// an administrator closes the configuration manager. A server relays it to every client; a client sends it to the
    /// server, which applies it only from an administrator and relays it.
    /// </summary>
    internal static class JotunnConfigBridge {
        static readonly System.Reflection.MethodInfo? Cache=HarmonyLib.AccessTools.Method(typeof(SynchronizationManager),"CacheConfigurationValues");
        static readonly System.Reflection.MethodInfo? Send=HarmonyLib.AccessTools.Method(typeof(SynchronizationManager),"SynchronizeChangedConfig");
        internal static bool Available=>Cache!=null&&Send!=null;
        internal static bool Publish(Action apply){
            if(!Available){apply();Plugin.Log?.LogWarning("[Meadworks:Crucible] Jotunn configuration synchronization is unavailable; the change stays on this machine.");return false;}
            Cache!.Invoke(SynchronizationManager.Instance,null);
            SyncedConfigGuard.AsServer(apply);
            Send!.Invoke(SynchronizationManager.Instance,null);
            return true;
        }
    }

    public static partial class MeadworksConfig {
        public static ConfigEntry<T> Bind<T>(ConfigFile config,string section,string key,T defaultValue,string description,ConfigScope scope=ConfigScope.Local,AcceptableValueBase? acceptableValues=null) {
            if(!Enum.IsDefined(typeof(ConfigScope),scope))throw new ArgumentException("Unknown configuration scope.");
            var entry=config.BindConfig(section,key,defaultValue,description,synced:scope!=ConfigScope.Local,acceptableValues:acceptableValues);
            RecordBinding(entry,MeadworksMischief.OwnerOf(config),scope,defaultValue);
            return entry;
        }
        /// <summary>
        /// Binds a product-managed entry (a synchronized carrier, an import marker) with full C01 ownership that configuration
        /// UIs do not list: the entry carries <c>ConfigurationManagerAttributes</c> <c>Browsable=false</c> and <c>ReadOnly=true</c>,
        /// which BepInEx ConfigurationManager and Almanac both honor (see <see cref="ConfigVisibilityRules"/>). The scope
        /// works as in <see cref="Bind{T}(ConfigFile,string,string,T,string,ConfigScope,AcceptableValueBase)"/>; a
        /// ServerAuthoritative hidden entry is Jotunn-synchronized. Cheats cannot be hidden (<see cref="ArgumentException"/>).
        /// </summary>
        public static ConfigEntry<T> BindHidden<T>(ConfigFile config,string section,string key,T defaultValue,string description,ConfigScope scope=ConfigScope.Local,AcceptableValueBase? acceptableValues=null) {
            ConfigVisibilityRules.Require(scope,ConfigVisibility.Hidden);
            var attributes=new ConfigurationManagerAttributes{Browsable=ConfigVisibilityRules.Browsable(ConfigVisibility.Hidden),ReadOnly=ConfigVisibilityRules.ReadOnly(ConfigVisibility.Hidden)};
            var entry=config.BindConfig(section,key,defaultValue,description,synced:scope!=ConfigScope.Local,acceptableValues:acceptableValues,configAttributes:attributes);
            RecordBinding(entry,MeadworksMischief.OwnerOf(config),scope,defaultValue,ConfigVisibility.Hidden);
            return entry;
        }
        /// <summary>
        /// Binds a cheat with its safe value as the default (C02 review rule: a fresh install never cheats). While the
        /// authority gate blocks the cheat, products read the safe value through <see cref="AuthorityDecision.Effective{T}"/>.
        /// </summary>
        /// <remarks>Without a Mischief category the cheat counts as <see cref="MischiefCategories.Untagged"/> (World) and is listed by <see cref="MeadworksMischief.Cheats"/> review.</remarks>
        public static ConfigEntry<T> BindCheat<T>(ConfigFile config,string section,string key,T safeValue,string description,AcceptableValueBase? acceptableValues=null){
            var entry=Bind(config,section,key,safeValue,description,ConfigScope.Cheat,acceptableValues);
            MeadworksMischief.Cheats.Tag(MeadworksMischief.OwnerOf(config),section+"."+key,null);
            return entry;
        }
        /// <summary>Binds a cheat with its safe value as the default and tags it with its Mischief gate category (C13).</summary>
        public static ConfigEntry<T> BindCheat<T>(ConfigFile config,string section,string key,T safeValue,string description,MischiefCategory category,AcceptableValueBase? acceptableValues=null){
            var entry=Bind(config,section,key,safeValue,description,ConfigScope.Cheat,acceptableValues);
            MeadworksMischief.Cheats.Tag(MeadworksMischief.OwnerOf(config),section+"."+key,category);
            return entry;
        }
        public static bool CanEdit(ConfigScope scope)=>Plugin.IsReady&&ConfigAuthority.CanEdit(scope,Plugin.Session);
        /// <summary>
        /// Consolidated edit decision (<see cref="ConfigEditPolicy"/>). Server presence comes from the authority gate
        /// (<see cref="MeadworksAuthority.Server"/>): Unknown while waiting for the Hello (cheats stay locked), Absent
        /// after the vanilla-server timeout. Products that know their peer class use <see cref="MeadworksAuthority.Decide"/>.
        /// </summary>
        public static ConfigEditDecision EditDecision(ConfigScope scope,bool serverLocked=true,ServerCrucible? server=null){
            if(!Plugin.IsReady)return ConfigEditPolicy.Decide(scope,new ConfigAuthorityFacts(SessionRole.RemoteClient,false,false,ServerCrucible.Unknown),serverLocked);
            var session=Plugin.Session;
            return ConfigEditPolicy.Decide(scope,ConfigAuthorityFacts.From(session,server??MeadworksAuthority.Server),serverLocked);
        }
        /// <summary>
        /// The value a product acts on (C01): the entry's value (a synchronized entry holds the server's value in memory;
        /// Jotunn restores the player's own value on disconnect) unless the gate locks it, then <paramref name="safe"/>:
        /// cheats while authority is pending, on vanilla servers and for turned-off products.
        /// </summary>
        public static T Effective<T>(ConfigEntry<T> entry,string productId,ConfigScope scope,T safe,bool serverLocked=true){
            if(entry==null)throw new ArgumentNullException(nameof(entry));
            return ConfigValues.Resolve(MeadworksAuthority.Decide(productId,scope,serverLocked),entry.Value,safe);
        }

        /// <summary>
        /// Changes a setting through the authority gate (C01 amendments, Charter steward model over Jotunn sync): the
        /// menu, single player and own-value settings write this machine's file; a host or dedicated server writes and
        /// publishes to connected clients; a remote administrator amends the server's value (the server applies it and
        /// relays it); everybody else is refused and told why (<see cref="MeadworksBlockedOptions"/>).
        /// </summary>
        public static ConfigAmendResult Amend<T>(ConfigEntry<T> entry,string productId,ConfigScope scope,T value,string? label=null,bool serverLocked=true){
            using var timing=EditTiming.Begin("Amend<T>");
            if(entry==null)throw new ArgumentNullException(nameof(entry));
            AuthorityDecision decision;AmendRoute route;
            using(timing.Step(EditStep.Authority)){
                decision=MeadworksAuthority.Decide(productId,scope,serverLocked);
                route=ConfigAmendment.Route(Plugin.IsReady?Plugin.Session.Role:SessionRole.Menu,decision);
            }
            timing.Describe(productId,route);
            ConfigAmendResult result;
            switch(route){
                case AmendRoute.Refused:MeadworksBlockedOptions.Notify(BlockedOption.From(productId,entry.Definition.Key,decision),label);result=new ConfigAmendResult(route,decision,false);break;
                case AmendRoute.Own:AssignOwn(entry,value);result=new ConfigAmendResult(route,decision,true);break;
                case AmendRoute.Publish:result=new ConfigAmendResult(route,decision,JotunnConfigBridge.Publish(()=>AssignOwn(entry,value)));break;
                default:result=new ConfigAmendResult(route,decision,JotunnConfigBridge.Publish(()=>entry.Value=value));break;
            }
            timing.Success();return result;
        }

        public static int EnsureSchema(ConfigFile config,int target,IReadOnlyList<MigrationStep>? steps=null) {
            var original=config.SaveOnConfigSet;config.SaveOnConfigSet=false;
            try {
                var schema=config.Bind("Meadworks","SchemaVersion",0,new ConfigDescription("Configuration schema; managed by Meadworks.",null,new ConfigurationManagerAttributes{Browsable=false}));
                // Schema 0 → 1 adds metadata only and preserves every existing gameplay setting.
                steps=steps??new[]{new MigrationStep(0,1,()=>{})};
                ConfigMigration.Run(schema.Value,target,steps,()=>{if(File.Exists(config.ConfigFilePath))File.Copy(config.ConfigFilePath,config.ConfigFilePath+".schema-"+schema.Value+"."+DateTime.UtcNow.Ticks+".bak",false);},v=>{schema.Value=v;config.Save();});
                return schema.Value;
            } finally {config.SaveOnConfigSet=original;}
        }
    }
}
