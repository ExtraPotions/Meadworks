// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Reuses Kindle 9420d181 Config/KindleConfig PublishLockedValues/ReadLocks/ServerBinds/OnSynchronized pattern.
// Timber uses exact bound Local entries and C01 hidden-carrier bindings, without changing player config values.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Timber.Config;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Timber.Runtime {
    public sealed class PlayerSettingsLocks : IDisposable {
        readonly ConfigFile file;
        readonly MeadworksLog log;
        readonly ConfigEntry<string> listEntry, carrier;
        readonly Dictionary<string,ConfigEntryBase> locals = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,LockTarget> known = new(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<ConfigEntryBase> tracked = new();
        readonly TimberSyncReceipt receipt = new();
        LockList locks=LockList.Empty;
        IReadOnlyDictionary<string,string> values = new Dictionary<string,string>();
        string listText="", valuesText="";
        float next;
        bool disposed,refreshing,dirty=true;
        public PlayerSettingsLocks(ConfigFile file,MeadworksLog log) {
            this.file=file;this.log=log;
            foreach(var key in file.Keys) {
                var entry=file[key];string id=key.Section+"."+key.Key;
                bool local=MeadworksConfig.TryGetMetadata(entry,out var data)&&data.ProductId==Plugin.Guid&&data.Scope==ConfigScope.Local;
                known[id]=new LockTarget(id,local);if(local)locals[id]=entry;
            }
            listEntry=MeadworksConfig.Bind(file,"Server","Locked Player Settings","","Comma-separated Local Section.Key ids whose runtime values follow this server. Empty preserves player choices.",ConfigScope.ServerAuthoritative);
            carrier=MeadworksConfig.BindHidden(file,"Server","Locked Player Values","","Managed serialized server values for Locked Player Settings; do not edit.",ConfigScope.ServerAuthoritative);
            MeadworksBlockedOptions.Track(Plugin.Guid,listEntry,ConfigScope.ServerAuthoritative);
            file.SettingChanged+=OnChanged;SynchronizationManager.OnConfigurationSynchronized+=OnSynchronized;
            Refresh();
        }
        void OnChanged(object sender,SettingChangedEventArgs args)=>dirty=true;
        void OnSynchronized(object sender,ConfigurationSynchronizationEventArgs args) {
            if(args.UpdatedPluginGUIDs==null||!args.UpdatedPluginGUIDs.Contains(Plugin.Guid)||!CruciblePlugin.IsReady)return;
            receipt.Observe();dirty=true;Refresh();
        }
        LockList Parse(string text)=>LockList.Parse(text,id=>known.TryGetValue(id,out var target)?target:(LockTarget?)null);
        bool ServerBinds()=>CruciblePlugin.IsReady&&CruciblePlugin.Session.Role==SessionRole.RemoteClient&&receipt.CurrentGeneration==CruciblePlugin.Session.Generation
            && MeadworksAuthority.Decide(Plugin.Guid,ConfigScope.ServerAuthoritative).Source==ConfigValueSource.Server;
        public void Tick() { if(disposed)return;float now=Time.unscaledTime;if(!dirty&&now<next)return;next=now+.5f;Refresh(); }
        void Refresh() {
            if(disposed||refreshing)return;refreshing=true;
            try {
                dirty=false;
                if(CruciblePlugin.IsReady&&CruciblePlugin.Session.Role!=SessionRole.RemoteClient) {
                    var own=Parse(listEntry.Value);string text=LockedValues.Compose(own,id=>locals.TryGetValue(id,out var entry)?entry.GetSerializedValue():null);
                    if(text!=carrier.Value)MeadworksConfig.Amend(carrier,Plugin.Guid,ConfigScope.ServerAuthoritative,text);
                }
                string list=MeadworksConfig.Effective(listEntry,Plugin.Guid,ConfigScope.ServerAuthoritative,"");
                if(list!=listText) { listText=list;locks=list.Length<=1024*1024?Parse(list):LockList.Empty;
                    if(locks.Unknown.Count>0||locks.NotLockable.Count>0)log.Warning("Locked Player Settings: "+locks.Describe()); }
                string carried=MeadworksConfig.Effective(carrier,Plugin.Guid,ConfigScope.ServerAuthoritative,"");
                if(carried!=valuesText) { valuesText=carried;values=carried.Length<=1024*1024?LockedValues.Decode(carried):new Dictionary<string,string>(); }
                bool binds=ServerBinds();
                foreach(var pair in locals) {
                    bool locked=PlayerLocks.Evaluate(pair.Key,binds,locks,values).Locked;
                    if(locked&&tracked.Add(pair.Value)) { MeadworksBlockedOptions.Untrack(pair.Value);MeadworksBlockedOptions.Track(Plugin.Guid,pair.Value,ConfigScope.ServerAuthoritative); }
                    else if(!locked&&tracked.Remove(pair.Value)) { MeadworksBlockedOptions.Untrack(pair.Value);MeadworksBlockedOptions.Track(Plugin.Guid,pair.Value,ConfigScope.Local); }
                }
            } catch(Exception error) { log.Warning("Player setting lock refresh: "+error.GetType().Name+": "+error.Message); }
            finally { refreshing=false; }
        }
        public bool IsLocked(ConfigEntryBase entry) {
            if(disposed||entry==null)return false;
            string id=entry.Definition.Section+"."+entry.Definition.Key;
            if(!locals.TryGetValue(id,out var own)||!ReferenceEquals(own,entry))return false;
            if(dirty)Refresh();
            return PlayerLocks.Evaluate(id,ServerBinds(),locks,values).Locked;
        }
        public T Effective<T>(ConfigEntry<T> entry,T own) {
            if(disposed)return own;
            var held=PlayerLocks.Evaluate(entry.Definition.Section+"."+entry.Definition.Key,ServerBinds(),locks,values);
            if(!held.Locked)return own;
            try {
                object parsed=TomlTypeConverter.ConvertToValue(held.ServerText!,entry.SettingType);
                if(entry.Description.AcceptableValues!=null&&!entry.Description.AcceptableValues.IsValid(parsed))return (T)entry.DefaultValue;
                return (T)parsed;
            } catch(Exception) { return (T)entry.DefaultValue; }
        }
        public string Describe()=>"Local locks: "+locks.Describe()+"; current server receipt: "+ServerBinds();
        public void Dispose() {
            if(disposed)return;disposed=true;file.SettingChanged-=OnChanged;SynchronizationManager.OnConfigurationSynchronized-=OnSynchronized;
            foreach(var entry in tracked) { MeadworksBlockedOptions.Untrack(entry);MeadworksBlockedOptions.Track(Plugin.Guid,entry,ConfigScope.Local); }
            tracked.Clear();MeadworksBlockedOptions.Untrack(listEntry);
        }
    }
}
