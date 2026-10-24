// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Native BepInEx adapter for the existing Almanac.Core index; no manager source is copied.
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Index;
using HarmonyLib;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Almanac.Runtime.Discovery
{
    /// <summary>
    /// A02b: retains live files weakly and feeds their identities to the already qualified portable index.
    /// Initialize in Awake; Discover after the chainloader finishes; Tick on the game's main thread.
    /// Configurations, synchronization policy and plugin load state are never changed here.
    /// </summary>
    public sealed class ConfigDiscovery : IDisposable
    {
        static WeakReference<ConfigDiscovery>? active;
        readonly Action<string>? warn;
        readonly MeadworksLog? log;
        bool traceRefresh;
        int refreshedFiles, refreshedEntries;
        long indexTicks;
        readonly bool windows=Path.DirectorySeparatorChar=='\\';
        readonly Dictionary<string,LiveRecord> live=new Dictionary<string,LiveRecord>();
        readonly Dictionary<string,bool> browsable=new Dictionary<string,bool>(StringComparer.Ordinal);
        readonly ConcurrentQueue<Constructed> constructed=new ConcurrentQueue<Constructed>();
        readonly ConcurrentQueue<Notice> notices=new ConcurrentQueue<Notice>();
        Harmony? patches;
        float nextRefresh;
        bool initialized,discovered;

        public ConfigDiscovery(Action<string>? warning=null, MeadworksLog? logger=null)
        {warn=warning;log=logger;Index=new ConfigIndex(windows);}
        public ConfigIndex Index {get;}
        /// <summary>Delivered on Tick, including a null entry for reload/poll invalidation.</summary>
        public event Action<ConfigFile,ConfigEntryBase?>? Changed;
        public bool IsPluginBrowsable(string guid)=>!browsable.TryGetValue(guid,out var shown)||shown;

        public void Initialize()
        {
            if(initialized)return;
            if(active!=null&&active.TryGetTarget(out var other)&&!ReferenceEquals(other,this)&&other.initialized)
                throw new InvalidOperationException("An Almanac discovery adapter is already active.");
            active=new WeakReference<ConfigDiscovery>(this);
            patches=new Harmony(AlmanacCore.Guid+".discovery");
            try
            {
                foreach(var ctor in typeof(ConfigFile).GetConstructors(BindingFlags.Public|BindingFlags.Instance))
                    patches.Patch(ctor,prefix:new HarmonyMethod(typeof(ConfigDiscovery),nameof(Constructing)),postfix:new HarmonyMethod(typeof(ConfigDiscovery),nameof(ConstructedFile)));
                initialized=true;
            }
            catch{patches.UnpatchSelf();patches=null;active=null;throw;}
        }
        // A constructor can run off the main thread; neither the index nor UI is touched from this hook.
        static void Constructing(object[] __args,out Origin __state)
        {
            __state=new Origin{StagingConstruction=MeadworksConfig.CurrentOwnValueStagingConstruction};
            try
            {
                var owner=__args.OfType<BepInPlugin>().FirstOrDefault();
                if(owner!=null){__state.Declared=Identity(owner);return;}
                var frames=new StackTrace().GetFrames();
                if(frames==null)return;
                __state.Calling=frames.Select(frame=>frame.GetMethod()?.DeclaringType)
                    .Where(type=>type!=null&&type!=typeof(ConfigDiscovery)&&type.DeclaringType!=typeof(ConfigDiscovery))
                    .Select(type=>type!.Assembly).Where(assembly=>assembly!=typeof(ConfigFile).Assembly&&assembly!=typeof(Harmony).Assembly&&assembly!=typeof(object).Assembly).Distinct().ToArray();
            }
            catch { } // Discovery must never break another plugin's ConfigFile construction.
        }
        static void ConstructedFile(ConfigFile __instance,Origin __state)
        {
            if(active!=null&&active.TryGetTarget(out var target)&&target.initialized)
                target.constructed.Enqueue(new Constructed(__instance,__state));
        }
        static PluginIdentity Identity(BepInPlugin metadata)=>new PluginIdentity(metadata.GUID,metadata.Name,metadata.Version.ToString());
        PluginIdentity? CallingOwner(IEnumerable<Assembly> assemblies)
        {
            foreach(var assembly in assemblies)
            {
                var owners=Chainloader.PluginInfos.Values.Where(p=>p.Instance!=null&&p.Instance.GetType().Assembly==assembly).ToArray();
                if(owners.Length==1)return Identity(owners[0].Metadata);
                if(owners.Length>1)return null; // Shared assemblies need explicit metadata.
            }
            return null;
        }
        string Key(string path)=>ConfigPaths.Key(Path.GetFullPath(path),windows);
        static ConfigEntryInfo[] Infos(ConfigFile file)=>file.Select(p=>new ConfigEntryInfo(p.Key.Section,p.Key.Key,p.Value.SettingType.FullName??p.Value.SettingType.Name)).ToArray();

        /// <summary>Called after all plugin Awakes/Starts; the constructor hook never determines correctness alone.</summary>
        public void Discover()
        {
            if(!initialized)throw new InvalidOperationException("Initialize discovery first.");
            foreach(var info in Chainloader.PluginInfos.Values.ToArray())
            {
                var instance=info.Instance;if(instance==null)continue;
                var owner=Identity(info.Metadata);var type=instance.GetType();
                browsable[owner.Guid]=type.GetCustomAttributes(typeof(BrowsableAttribute),true).OfType<BrowsableAttribute>().All(a=>a.Browsable);
                Try(()=>{Index.AddPluginConfig(owner,instance.Config.ConfigFilePath,Infos(instance.Config));Remember(instance.Config);});
                // One level of fields on the already instantiated plugin, including inherited fields.
                // Do not evaluate properties or traverse arbitrary plugin object graphs.
                for(var at=type;at!=null&&at!=typeof(object);at=at.BaseType)
                    foreach(var field in at.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))
                    {
                        if(!typeof(ConfigFile).IsAssignableFrom(field.FieldType))continue;
                        Try(()=>{if(field.GetValue(field.IsStatic?null:instance) is ConfigFile file){Index.AddFieldScanFile(owner,file.ConfigFilePath,Infos(file));Remember(file);}});
                    }
            }
            Try(()=>{
                if(AccessTools.Property(typeof(ConfigFile),"CoreConfig")?.GetValue(null) is ConfigFile core)
                {Index.AddCoreConfig(core.ConfigFilePath,Infos(core));Remember(core);}
            });
            Try(()=>{foreach(var path in Directory.GetFiles(Paths.ConfigPath,"*.cfg",SearchOption.AllDirectories))Index.AddDiskFile(path);});
            DrainConstructed();
            foreach(var file in live.Values.ToArray())if(file.File.TryGetTarget(out var target))Try(()=>Refresh(target,true));
            discovered=true;
        }
        // Another mod (or a test) may open a second ConfigFile on a plugin's own cfg just to read it. Keep showing the
        // plugin's live Config: the extra handle is not the one the plugin binds, edits or listens to.
        bool ShadowsPluginConfig(ConfigFile file)
        {
            if(!live.TryGetValue(Key(file.ConfigFilePath),out var held)||!held.File.TryGetTarget(out var current)||ReferenceEquals(current,file))return false;
            return IsPluginConfig(current)&&!IsPluginConfig(file);
        }
        static bool IsPluginConfig(ConfigFile file)=>Chainloader.PluginInfos.Values.Any(p=>p.Instance!=null&&ReferenceEquals(p.Instance.Config,file));
        void Remember(ConfigFile file)
        {
            var key=Key(file.ConfigFilePath);
            if(live.TryGetValue(key,out var held)&&held.File.TryGetTarget(out var same)&&ReferenceEquals(same,file))return;
            // A path can be reopened; replace the native handle rather than retaining an obsolete ConfigFile.
            if(held!=null)held.Detach();
            var weak=new WeakReference<ConfigDiscovery>(this);var record=new LiveRecord(file);
            record.SettingChanged=(sender,args)=>{
                if(weak.TryGetTarget(out var owner)&&record.File.TryGetTarget(out var current))owner.notices.Enqueue(new Notice(current,args.ChangedSetting));
            };
            record.Reloaded=(sender,args)=>{
                if(weak.TryGetTarget(out var owner)&&record.File.TryGetTarget(out var current))owner.notices.Enqueue(new Notice(current,null));
            };
            file.SettingChanged+=record.SettingChanged;file.ConfigReloaded+=record.Reloaded;
            live[key]=record;
        }
        void DrainConstructed()
        {
            int remaining=constructed.Count;
            while(remaining-->0&&constructed.TryDequeue(out var item))
            {
                // Requeue only this initial batch so an unfinished constructor cannot spin the drain.
                if(MeadworksConfig.IsOwnValueStagingConstructionPending(item.Owner.StagingConstruction)){constructed.Enqueue(item);continue;}
                if(item.File.TryGetTarget(out var file)&&!MeadworksConfig.IsOwnValueStaging(file)&&!ShadowsPluginConfig(file))Try(()=>{Index.AddHookedFile(file.ConfigFilePath,item.Owner.Declared,CallingOwner(item.Owner.Calling),Infos(file));Remember(file);Refresh(file,true);});
            }
        }
        public ConfigFile? LiveFile(string path)=>live.TryGetValue(Key(path),out var record)&&record.File.TryGetTarget(out var file)?file:null;
        public IReadOnlyList<ConfigEntryBase> Entries(string path)
        {var file=LiveFile(path);return file==null?Array.Empty<ConfigEntryBase>():file.Select(p=>p.Value).ToArray();}
        public IReadOnlyDictionary<EntryKey,string> Orphans(string path)
        {
            var file=LiveFile(path);var result=new Dictionary<EntryKey,string>();if(file==null)return result;
            if(AccessTools.Property(typeof(ConfigFile),"OrphanedEntries")?.GetValue(file) is IDictionary<ConfigDefinition,string> entries)
                foreach(var pair in entries.ToArray())result[new EntryKey(pair.Key.Section,pair.Key.Key)]=pair.Value;
            return result;
        }
        /// <summary>Re-read when a group expands or a search runs; native values stay on their ConfigEntry objects.</summary>
        public void Refresh(string path){var file=LiveFile(path);if(file!=null)Try(()=>Refresh(file,true));}
        /// <returns>True when the index was updated.</returns>
        bool Refresh(ConfigFile file,bool force=false)
        {
            if(Index.Find(file.ConfigFilePath)==null)return false;
            var record=live[Key(file.ConfigFilePath)];if(!force&&record.Count==file.Count)return false;
            long refreshStart=traceRefresh?Stopwatch.GetTimestamp():0;
            try
            {
                Index.UpdateEntries(file.ConfigFilePath,Infos(file));Index.AddOrphanedKeys(file.ConfigFilePath,Orphans(file.ConfigFilePath).Keys);
                record.Count=file.Count;
                if(traceRefresh){refreshedFiles++;refreshedEntries+=file.Count;}
                return true;
            }
            finally{if(traceRefresh)indexTicks+=Stopwatch.GetTimestamp()-refreshStart;}
        }
        public void Tick(bool visible)
        {
            if(!initialized)return;
            var timing=new StallTiming(log);
            int queuedConstructed=timing.Enabled?constructed.Count:0,queuedNotices=timing.Enabled?notices.Count:0;
            long begin=timing.Enabled?Stopwatch.GetTimestamp():0,drained=begin,noted=begin;
            traceRefresh=timing.Enabled;refreshedFiles=refreshedEntries=0;indexTicks=0;
            try
            {
                DrainConstructed();
                if(timing.Enabled)drained=Stopwatch.GetTimestamp();
                while(notices.TryDequeue(out var notice))if(notice.File.TryGetTarget(out var file))Try(()=>{
                    // Ignore notifications from an old handle superseded at the same path.
                    if(!ReferenceEquals(LiveFile(file.ConfigFilePath),file))return;
                    Refresh(file,true);Changed?.Invoke(file,notice.Entry!=null&&notice.Entry.TryGetTarget(out var current)?current:null);
                });
                if(timing.Enabled)noted=Stopwatch.GetTimestamp();
                if(!visible||!discovered||Time.unscaledTime<nextRefresh)return;
                nextRefresh=Time.unscaledTime+0.5f;
                foreach(var record in live.Values.ToArray())if(record.File.TryGetTarget(out var file))Try(()=>{if(Refresh(file))Changed?.Invoke(file,null);}); // An unchanged file must not force a full window rebuild.
            }
            finally
            {
                traceRefresh=false;
                if(timing.Enabled&&(queuedConstructed!=0||queuedNotices!=0||refreshedFiles!=0))
                {
                    long pollTicks=Stopwatch.GetTimestamp()-noted;
                    timing.Finish().Complete("Almanac.discovery", "open="+visible+" constructedQueued="+queuedConstructed+" noticesQueued="+queuedNotices
                        +" refreshes="+refreshedFiles+" entries="+refreshedEntries+" constructedMs="+StallTiming.Ms(drained-begin)
                        +" noticesMs="+StallTiming.Ms(noted-drained)+" pollMs="+StallTiming.Ms(pollTicks)+" indexMs="+StallTiming.Ms(indexTicks));
                }
            }
        }

        void Try(Action action){try{action();}catch(Exception error){warn?.Invoke("Configuration discovery: "+error.GetType().Name+": "+error.Message);}}
        public void Dispose()
        {
            initialized=discovered=false;patches?.UnpatchSelf();patches=null;
            foreach(var record in live.Values)record.Detach();live.Clear();browsable.Clear();
            while(constructed.TryDequeue(out _)){}while(notices.TryDequeue(out _)){}Changed=null;
            if(active!=null&&active.TryGetTarget(out var current)&&ReferenceEquals(current,this))active=null;
        }
        sealed class Origin {internal PluginIdentity? Declared;internal Assembly[] Calling=Array.Empty<Assembly>();internal object? StagingConstruction;}
        sealed class Constructed
        {internal readonly WeakReference<ConfigFile> File;internal readonly Origin Owner;internal Constructed(ConfigFile file,Origin origin){File=new WeakReference<ConfigFile>(file);Owner=origin;}}
        sealed class Notice
        {internal readonly WeakReference<ConfigFile> File;internal readonly WeakReference<ConfigEntryBase>? Entry;internal Notice(ConfigFile file,ConfigEntryBase? entry){File=new WeakReference<ConfigFile>(file);Entry=entry==null?null:new WeakReference<ConfigEntryBase>(entry);}}
        sealed class LiveRecord
        {
            internal readonly WeakReference<ConfigFile> File;
            internal int Count=-1;
            internal EventHandler<SettingChangedEventArgs>? SettingChanged;internal EventHandler? Reloaded;
            internal LiveRecord(ConfigFile file){File=new WeakReference<ConfigFile>(file);}
            internal void Detach(){if(File.TryGetTarget(out var file)){file.SettingChanged-=SettingChanged;file.ConfigReloaded-=Reloaded;}}
        }
    }
}
