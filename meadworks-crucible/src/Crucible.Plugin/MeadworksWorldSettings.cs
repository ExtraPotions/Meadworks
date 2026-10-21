// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Read-only Almanac/Thing metadata. Config remains the C01 server-authoritative fallback.</summary>
    public sealed class WorldSettingMetadata {
        readonly WorldSettingBinding binding;
        public string ProductId{get;}public string Key{get;}public ConfigEntryBase Config{get;}
        public long WorldId{get;}public long Generation{get;}public string? WorldText{get;}public WorldSettingSource Source=>WorldText==null?WorldSettingSource.Config:WorldSettingSource.World;
        public AuthorityDecision Decision{get;}
        /// <summary>Edit this exact captured binding and displayed world/session; stale windows fail closed.</summary>
        public WorldSettingWriteResult Set(string text)=>binding.Set(text,WorldId,Generation);
        public WorldSettingWriteResult Clear()=>binding.Clear(WorldId,Generation);
        internal WorldSettingMetadata(WorldSettingBinding binding){this.binding=binding;ProductId=binding.ProductId;Key=binding.Key;Config=binding.Config;
            WorldId=MeadworksWorldSettings.LiveWorld;Generation=Plugin.IsReady?Plugin.Session.Generation:0;WorldText=binding.WorldText;Decision=binding.EditDecision;}
    }
    public sealed class WorldSettingWriteResult {
        public WorldSettingWriteStatus Status{get;}public AuthorityDecision Decision{get;}
        public bool Applied=>Status==WorldSettingWriteStatus.Applied;
        internal WorldSettingWriteResult(WorldSettingWriteStatus status,AuthorityDecision decision){Status=status;Decision=decision;}
    }
    /// <summary>A product-owned key. Resolve delegates to the product's existing typed resolver, including validation/defaults.</summary>
    public sealed class WorldSettingBinding:IDisposable {
        public string ProductId{get;}public string Key{get;}public ConfigEntryBase Config{get;}
        readonly Func<string,string?> canonicalize;internal bool Disposed;
        internal WorldSettingBinding(string product,string key,ConfigEntryBase config,Func<string,string?> canonicalize){ProductId=product;Key=key;Config=config;this.canonicalize=canonicalize;}
        internal string? Canonical(string text){try{return canonicalize(text);}catch{return null;}}
        public string? WorldText=>Disposed?null:MeadworksWorldSettings.Read(this);
        public AuthorityDecision EditDecision=>WorldSettingStorage.Decide(MeadworksAuthority.Facts(ProductId),MeadworksWorldSettings.LiveWorld,!Disposed&&MeadworksWorldSettings.StorageReady);
        public WorldSettingMetadata Metadata=>new WorldSettingMetadata(this);
        /// <summary>For Wildblood: text => WorldSettings.Resolve(setting,text,currentCfg); no second parser or resolution formula.</summary>
        public T Resolve<T>(Func<string?,T> resolve){if(resolve==null)throw new ArgumentNullException(nameof(resolve));return resolve(WorldText);}
        public WorldSettingWriteResult Set(string text)=>MeadworksWorldSettings.Write(this,text);
        public WorldSettingWriteResult Clear()=>MeadworksWorldSettings.Write(this,null);
        /// <summary>Editors pass the displayed metadata stamp so a stale window cannot edit a newly joined world.</summary>
        public WorldSettingWriteResult Set(string text,long expectedWorld,long expectedGeneration)=>MeadworksWorldSettings.Write(this,text,expectedWorld,expectedGeneration);
        public WorldSettingWriteResult Clear(long expectedWorld,long expectedGeneration)=>MeadworksWorldSettings.Write(this,null,expectedWorld,expectedGeneration);
        public void Dispose(){if(Disposed)return;Disposed=true;MeadworksWorldSettings.Release(this);}
    }
    /// <summary>CS4 uses Valheim's saved/synchronized ZoneSystem keys; never writes an override into the product cfg.</summary>
    public static class MeadworksWorldSettings {
        static readonly Dictionary<string,WorldSettingBinding> bindings=new Dictionary<string,WorldSettingBinding>(StringComparer.Ordinal);
        static Harmony? harmony;
        internal static long LiveWorld=>Plugin.IsReady&&Plugin.Session.Role!=SessionRole.Menu?Plugin.Session.WorldId:0;
        internal static bool StorageReady=>LiveWorld!=0&&ZoneSystem.instance!=null&&ZNet.instance!=null
            &&Plugin.Session.AuthorityReady&&ZNet.instance.GetWorld()!=null&&ZNet.instance.GetWorldUID()==LiveWorld;
        /// <summary>Called once by Crucible after Services/session setup. Does not register a new RPC.</summary>
        public static void Install(){if(harmony!=null)return;var next=new Harmony(Plugin.Guid+".world-settings");
            try{next.CreateClassProcessor(typeof(WorldSettingRoutedContext)).Patch();next.CreateClassProcessor(typeof(WorldSettingSetGuard)).Patch();
                next.CreateClassProcessor(typeof(WorldSettingRemoveGuard)).Patch();next.CreateClassProcessor(typeof(WorldSettingReceiveGuard)).Patch();
                next.CreateClassProcessor(typeof(WorldSettingRelayGuard)).Patch();next.CreateClassProcessor(typeof(WorldSettingNativeDispatch)).Patch();harmony=next;}
            catch{next.UnpatchSelf();throw;}}
        /// <summary>Call after stopping product producers, before disposing Services. World keys remain in the save.</summary>
        public static void Shutdown(){harmony?.UnpatchSelf();harmony=null;foreach(var binding in bindings.Values)binding.Disposed=true;bindings.Clear();WorldSettingRoutedContext.Reset();WorldSettingNativeDispatch.Depth=0;}
        public static WorldSettingBinding Register(string productId,string key,ConfigEntryBase config,Func<string,string?> canonicalize){
            WorldSettingStorage.RequireKey(key);
            if(config==null)throw new ArgumentNullException(nameof(config));if(canonicalize==null)throw new ArgumentNullException(nameof(canonicalize));
            if(!MeadworksConfig.TryGetMetadata(config,out var metadata)||metadata.ProductId!=productId||metadata.Scope!=ConfigScope.ServerAuthoritative)
                throw new ArgumentException("Register the exact product-owned C01 ServerAuthoritative fallback entry.",nameof(config));
            if(bindings.ContainsKey(key))throw new InvalidOperationException("World key already registered: "+key);
            var binding=new WorldSettingBinding(productId,key,config,canonicalize);bindings.Add(key,binding);return binding;
        }
        public static IReadOnlyList<WorldSettingMetadata> Snapshot()=>Array.AsReadOnly(bindings.Values.OrderBy(b=>b.ProductId,StringComparer.Ordinal).ThenBy(b=>b.Key,StringComparer.Ordinal).Select(b=>b.Metadata).ToArray());
        internal static void Release(WorldSettingBinding binding){if(bindings.TryGetValue(binding.Key,out var current)&&ReferenceEquals(current,binding))bindings.Remove(binding.Key);}
        internal static string? Read(WorldSettingBinding binding){
            if(!StorageReady||!MeadworksAuthority.ProductActive(binding.ProductId,out _))return null;
            return ZoneSystem.instance.GetGlobalKey(binding.Key,out string stored)&&WorldSettingStorage.TryDecode(LiveWorld,stored,out string text)?binding.Canonical(text):null;
        }
        internal static WorldSettingWriteResult Write(WorldSettingBinding binding,string? value,long? expectedWorld=null,long? expectedGeneration=null){
            var decision=binding.EditDecision;
            if(!decision.Allowed)return new WorldSettingWriteResult(WorldSettingWriteStatus.Refused,decision);
            if(binding.Disposed||harmony==null||!StorageReady)return new WorldSettingWriteResult(WorldSettingWriteStatus.Unavailable,decision);
            if(expectedWorld.HasValue&&(expectedWorld.Value!=LiveWorld||expectedGeneration!=Plugin.Session.Generation))
                return new WorldSettingWriteResult(WorldSettingWriteStatus.Unavailable,decision);
            string? canonical=value==null?null:binding.Canonical(value);
            if(value!=null&&canonical==null)return new WorldSettingWriteResult(WorldSettingWriteStatus.Invalid,decision);
            string? stored;
            try{stored=canonical==null?null:WorldSettingStorage.Encode(LiveWorld,canonical);}catch(ArgumentException){return new WorldSettingWriteResult(WorldSettingWriteStatus.Invalid,decision);}
            var zone=ZoneSystem.instance;
            if(stored==null)zone.RemoveGlobalKey(binding.Key+" "+WorldSettingStorage.Encode(LiveWorld,""));else zone.SetGlobalKey(binding.Key+" "+stored);
            // The native RPC executes locally on authority, remotely on an admin client. Never claim a remote acknowledgment.
            bool applied=stored==null?!zone.GetGlobalKey(binding.Key):zone.GetGlobalKey(binding.Key,out string current)&&current==stored;
            return new WorldSettingWriteResult(applied?WorldSettingWriteStatus.Applied:WorldSettingWriteStatus.Queued,decision);
        }
        internal static bool AllowMutation(string input,bool remove,long claimedSender){
            string key=input==null?"":input.Split(new[]{' '},2)[0];
            if(!WorldSettingStorage.Reserved(key))return true;
            var net=ZNet.instance;
            if(harmony==null||net==null||!net.IsServer()||!StorageReady)return false;
            key=key.ToLowerInvariant();if(!bindings.TryGetValue(key,out var binding)||binding.Disposed)return false;
            if(!MeadworksAuthority.ProductActive(binding.ProductId,out _))return false;
            var incoming=WorldSettingRoutedContext.Current;
            if(WorldSettingNativeDispatch.Depth>0){if(claimedSender!=ZNet.GetUID())return false;}
            else if(incoming!=null){var peer=net.GetPeers().FirstOrDefault(p=>ReferenceEquals(p.m_rpc,incoming));
                string? host=incoming.GetSocket()?.GetHostName();
                if(peer==null||!peer.IsReady()||!incoming.IsConnected()||peer.m_uid!=claimedSender||string.IsNullOrEmpty(host)||!net.IsAdmin(host))return false;}
            else if(claimedSender!=ZNet.GetUID())return false;
            int split=input!.IndexOf(' ');
            return split>0&&WorldSettingStorage.TryDecode(LiveWorld,input.Substring(split+1),out string text,requireVersion:true)
                &&(remove?text.Length==0:binding.Canonical(text)!=null);
        }
    }
}
