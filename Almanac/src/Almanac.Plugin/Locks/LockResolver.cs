// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Almanac.Runtime.Locks
{
    // Optional systems are read by their actual public conventions, with registry
    // membership checked by exact BaseConfig identity. Their own handlers route writes.
    public sealed class LockResolver : IDisposable
    {
        const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        readonly Dictionary<ConfigEntryBase,Owner> owners=new Dictionary<ConfigEntryBase,Owner>();
        readonly HashSet<ConfigEntryBase> ambiguous=new HashSet<ConfigEntryBase>();
        readonly NativeJotunn jotunn=new NativeJotunn();
        // Per file and per owner: "this file holds a synced entry" is computed once per rebuild; a per-entry scan is O(n^2).
        readonly Dictionary<ConfigFile,(int Count,bool Synced)> syncedFiles=new Dictionary<ConfigFile,(int Count,bool Synced)>();
        float nextScan;
        bool disposed,scanFailed;
        sealed class Owner
        {
            internal object System,Data;
            internal bool Conditional;
            internal Type? CharterRegistry;
            internal string? CharterGuid;
            internal bool Charter=>CharterRegistry!=null;
            internal Owner(object system,object data,bool conditional){System=system;Data=data;Conditional=conditional;}
        }
        internal static object? Read(object target,string name)
        {
            var type=target as Type??target.GetType();var instance=target is Type?null:target;
            // CCS's ConfigSync alias inherits its private registry fields from ConditionalConfigSync.
            for(var current=type;current!=null;current=current.BaseType)
            {
                var property=current.GetProperty(name,Members|BindingFlags.DeclaredOnly);
                if(property!=null)return property.GetValue(instance);
                var field=current.GetField(name,Members|BindingFlags.DeclaredOnly);
                if(field!=null)return field.GetValue(instance);
            }
            return null;
        }
        static bool Flag(object target,string name,bool fallback=false)=>Read(target,name) is bool value?value:fallback;
        bool Remote=>CruciblePlugin.IsReady&&CruciblePlugin.Session.Role==SessionRole.RemoteClient
            ||ZNet.instance!=null&&!ZNet.instance.IsServer()&&ZNet.instance.GetServerRPC()?.IsConnected()==true;
        void Refresh()
        {
            if(disposed)throw new ObjectDisposedException(nameof(LockResolver));
            if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.5f;
            owners.Clear();ambiguous.Clear();syncedFiles.Clear();scanFailed=false;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach(string name in new[]{"ServerSync.ConfigSync","ConditionalConfigSync.ConditionalConfigSync"})
                {
                    var type=assembly.GetType(name,false);if(type==null)continue;
                    try
                    {
                        if(Read(type,"configSyncs") is not IEnumerable systems){scanFailed=true;continue;}
                        foreach(var system in systems)
                        {
                            if(system==null)continue;
                            if(Read(system,"allConfigs") is not IEnumerable entries){scanFailed=true;continue;}
                            foreach(var data in entries)
                            {
                                if(data==null||Read(data,"BaseConfig") is not ConfigEntryBase entry)continue;
                                AddOwner(entry,new Owner(system,data,name.StartsWith("Conditional",StringComparison.Ordinal)));
                            }
                        }
                    }
                    catch{scanFailed=true;}
                }
                foreach(string product in new[]{"Burrow","Mantle"})
                {
                    string guid="com.meadworks."+product.ToLowerInvariant();
                    var registry=assembly.GetType("ExtraPotions."+product+".Embedded.Charter.GameHooks",false);
                    if(registry==null||!Chainloader.PluginInfos.TryGetValue(guid,out var plugin)
                        ||!ReferenceEquals(plugin.Instance.GetType().Assembly,assembly))continue;
                    try
                    {
                        if(Read(registry,"charters") is not IEnumerable systems){scanFailed=true;continue;}
                        foreach(var system in systems)
                        {
                            if(system==null||system.GetType().FullName!=registry.Namespace+".Charter"
                                ||Read(system,"Guid") is not string id||id!=guid)continue;
                            var ledger=Read(system,"Ledger");
                            if(ledger==null||Read(ledger,"Clauses") is not IEnumerable clauses){scanFailed=true;continue;}
                            foreach(var clause in clauses)
                                if(clause!=null&&Read(clause,"Entry") is ConfigEntryBase entry)
                                    AddOwner(entry,new Owner(system,clause,false){CharterRegistry=registry,CharterGuid=guid});
                        }
                    }
                    catch{scanFailed=true;}
                }
            }
        }
        void AddOwner(ConfigEntryBase entry,Owner owner)
        {
            if(ambiguous.Contains(entry))return;
            if(owners.TryGetValue(entry,out var previous)
                &&(!ReferenceEquals(previous.System,owner.System)||!ReferenceEquals(previous.Data,owner.Data)))
            {scanFailed=true;owners.Remove(entry);ambiguous.Add(entry);return;}
            owners[entry]=owner;
        }
        bool Synced(ConfigEntryBase entry)
        {
            if(ambiguous.Contains(entry))return true;
            if(MeadworksConfig.TryGetMetadata(entry,out var metadata))return metadata.Scope!=ConfigScope.Local;
            if(owners.TryGetValue(entry,out var owner))return OwnerSynced(owner);
            var tags=NativeTags.Read(entry.Description.Tags);
            return tags.Attributes.IsAdminOnly==true&&jotunn.Manages(entry)
                ||entry.Description.Tags.Any(tag=>tag!=null&&tag.GetType().Name.StartsWith("SyncedConfigEntry",StringComparison.Ordinal));
        }
        bool FileSynced(ConfigFile file)
        {
            // A changed entry count (a late Bind) recomputes, so a new synced entry never escapes save suppression.
            if(syncedFiles.TryGetValue(file,out var known)&&known.Count==file.Count)return known.Synced;
            bool synced=file.Any(pair=>Synced(pair.Value));syncedFiles[file]=(file.Count,synced);return synced;
        }
        static bool OwnerSynced(Owner owner)=>owner.Charter?!Flag(owner.Data,"Local",true):Flag(owner.Data,owner.Conditional?"IsServerControlled":"SynchronizedConfig",true);
        static bool CurrentOwner(Owner owner,ConfigEntryBase entry)
        {
            if(owner.Charter)
            {
                if(owner.CharterGuid==null||!Chainloader.PluginInfos.TryGetValue(owner.CharterGuid,out var plugin)
                    ||!ReferenceEquals(plugin.Instance.GetType().Assembly,owner.CharterRegistry!.Assembly)
                    ||Read(owner.System,"Guid") is not string id||id!=owner.CharterGuid
                    ||Read(plugin.Instance.GetType(),"Synced") is not object facade||!ReferenceEquals(Read(facade,"Sync"),owner.System)
                    ||Read(owner.CharterRegistry,"charters") is not IEnumerable charters
                    ||!charters.Cast<object>().Any(system=>ReferenceEquals(system,owner.System))
                    ||Read(owner.System,"Ledger") is not object ledger||Read(ledger,"Clauses") is not IEnumerable clauses
                    ||Read(owner.Data,"Local") is not bool||!ReferenceEquals(Read(owner.Data,"Entry"),entry))return false;
                return clauses.Cast<object>().Any(clause=>ReferenceEquals(clause,owner.Data));
            }
            if(!ReferenceEquals(Read(owner.Data,"BaseConfig"),entry)||Read(owner.System.GetType(),"configSyncs") is not IEnumerable systems
                ||!systems.Cast<object>().Any(system=>ReferenceEquals(system,owner.System))||Read(owner.System,"allConfigs") is not IEnumerable entries)return false;
            return entries.Cast<object>().Any(data=>ReferenceEquals(data,owner.Data));
        }
        static bool Writable(Owner owner,ConfigEntryBase entry)
        {
            if(!CurrentOwner(owner,entry))return false;
            if(owner.Charter)return !OwnerSynced(owner)||Flag(owner.System,"MayAmend");
            string name=owner.Conditional?"IsWritableConfig":"isWritableConfig";
            for(var current=owner.System.GetType();current!=null;current=current.BaseType)
            {
                var method=current.GetMethods(Members|BindingFlags.DeclaredOnly).SingleOrDefault(candidate=>candidate.Name==name
                    &&candidate.ReturnType==typeof(bool)&&candidate.GetParameters().Length==1
                    &&candidate.GetParameters()[0].ParameterType.IsInstanceOfType(owner.Data));
                if(method!=null)return method.Invoke(method.IsStatic?null:owner.System,new[]{owner.Data}) is bool allowed&&allowed;
            }
            return false;
        }
        bool WriteThroughOwner(Owner owner,ConfigEntryBase entry,object? value)
        {
            if(owner.Charter)
            {
                nextScan=0;Refresh();
                if(ambiguous.Contains(entry)||!owners.TryGetValue(entry,out var current)
                    ||!ReferenceEquals(current.System,owner.System)||!ReferenceEquals(current.Data,owner.Data))return false;
            }
            if(!Writable(owner,entry)||owner.Conditional&&(Flag(owner.Data,"IsHidden",true)
                ||Remote&&!Flag(owner.Data,"IsPolicyStateInitialized")))return false;
            if(Remote&&OwnerSynced(owner)&&!(owner.Charter?CharterOwnershipReady(owner):Flag(owner.System,"InitialSyncDone")))return false;
            entry.BoxedValue=value;return true; // Its own SettingChanged handler validates/forwards the change.
        }
        static bool CharterReady(Owner owner)
        {
            var net=ZNet.instance;var rpc=net!=null&&!net.IsServer()?net.GetServerRPC():null;
            var receiver=Read(owner.System,"Receiver");
            return rpc!=null&&rpc.IsConnected()&&receiver!=null&&ReferenceEquals(Read(receiver,"Rpc"),rpc)
                &&Read(receiver,"PushCount") is int count&&count>0;
        }
        static bool CharterOwnershipReady(Owner owner)
        {
            if(CharterReady(owner))return true;
            if(Flag(owner.System,"IsBound",true))return false;
            // Bound=false also precedes the first push. Only established server absence makes it an own-value state.
            var rpc=ZNet.instance?.GetServerRPC();
            if(rpc==null||!rpc.IsConnected()||!Chainloader.PluginInfos.TryGetValue(CruciblePlugin.Guid,out var plugin)
                ||Read(plugin.Instance,"transport") is not object transport||!ReferenceEquals(Read(transport,"presenceRpc"),rpc))return false;
            return MeadworksAuthority.Server==ServerCrucible.Absent
                ||MeadworksAuthority.ServerProductOf(owner.CharterGuid??"")==ServerProduct.Missing;
        }
        Func<bool> CharterConfirmation(Owner owner,ConfigEntryBase entry,object? value)
        {
            var net=ZNet.instance;var rpc=net!=null&&!net.IsServer()?net.GetServerRPC():null;
            var receiver=Read(owner.System,"Receiver");var before=receiver==null?null:Read(receiver,"PushCount");
            var session=CruciblePlugin.Session;long generation=session.Generation,world=session.WorldId;
            string expected=TomlTypeConverter.ConvertToString(value,entry.SettingType);
            return ()=>{
                var current=CruciblePlugin.Session;
                return before is int count&&receiver!=null&&ReferenceEquals(ZNet.instance,net)
                    &&ReferenceEquals(net?.GetServerRPC(),rpc)&&ReferenceEquals(Read(owner.System,"Receiver"),receiver)
                    &&current.Generation==generation&&current.WorldId==world&&CurrentOwner(owner,entry)&&CharterReady(owner)
                    &&Read(receiver,"PushCount") is int after&&after>count
                    &&Read(owner.Data,"AuthorToml") is string author&&string.Equals(author,expected,StringComparison.Ordinal)
                    &&string.Equals(TomlTypeConverter.ConvertToString(entry.BoxedValue,entry.SettingType),expected,StringComparison.Ordinal);
            };
        }
        public bool HiddenByServerPolicy(ConfigEntryBase entry)
        {
            try{Refresh();return ambiguous.Contains(entry)||owners.TryGetValue(entry,out var owner)&&owner.Conditional
                &&(Flag(owner.Data,"IsHidden",true)||(Remote&&!Flag(owner.Data,"IsPolicyStateInitialized")));}
            catch{return true;}
        }
        public NativeWriteFacts Resolve(ConfigEntryBase entry)
        {
            var facts=new NativeWriteFacts{ConnectedAsClient=Remote,Known=true};
            try
            {
                Refresh();
                facts.FileHasSyncedEntries=FileSynced(entry.ConfigFile);
                var attributes=NativeTags.Read(entry.Description.Tags).Attributes;
                if(ambiguous.Contains(entry))
                {facts.Known=false;facts.Editable=false;facts.Reason="More than one sync system claims this entry.";}
                else if(owners.TryGetValue(entry,out var policy)&&policy.Conditional
                    &&(Flag(policy.Data,"IsHidden",true)||Remote&&!Flag(policy.Data,"IsPolicyStateInitialized")))
                {facts.Editable=false;facts.Reason="Hidden or awaiting server ownership policy.";}
                else if(MeadworksConfig.TryGetMetadata(entry,out var metadata))
                {
                    facts.DeclaredScope=metadata.Scope;
                    var decision=metadata.Decision;facts.EntrySynced=Remote&&metadata.Scope!=ConfigScope.Local;
                    facts.RequiresOwnerWrite=true;facts.OwnerWrite=value=>MeadworksConfig.Amend(entry,value).Applied;
                    facts.Editable=decision.Allowed;facts.Reason=decision.Allowed?null:MeadworksBlockedOptions.Reason(decision.Reason);
                    // A cheat the admin keeps to admins only is shown disabled to players, even for their own preference.
                    if(facts.Editable&&MeadworksMischief.IsCheat(entry,out var category)&&!MeadworksMischief.AllowsCheat(entry,metadata.ProductId,category))
                    {facts.Editable=false;facts.Reason="Admins only on this server.";}
                }
                else if(owners.TryGetValue(entry,out var owner))
                {
                    bool synced=OwnerSynced(owner);
                    facts.DeclaredScope=synced?ConfigScope.ServerAuthoritative:ConfigScope.Local;
                    facts.EntrySynced=Remote&&synced&&(!owner.Charter||Flag(owner.System,"IsBound",true));
                    if(!CurrentOwner(owner,entry))
                    {facts.Known=false;facts.Editable=false;facts.Reason="The sync owner is no longer registered.";}
                    else if(owner.Conditional&&Flag(owner.Data,"IsHidden",true))
                    {facts.Editable=false;facts.Reason="Hidden by server policy.";}
                    else if(Remote&&synced&&!(owner.Charter?CharterOwnershipReady(owner):Flag(owner.System,"InitialSyncDone")))
                    {facts.Editable=false;facts.Reason="Waiting for server configuration.";}
                    else if(owner.Conditional&&!Flag(owner.Data,"IsPolicyStateInitialized",!Remote))
                    {facts.Editable=false;facts.Reason="Waiting for server ownership policy.";}
                    else
                    {
                        facts.Editable=Writable(owner,entry);
                        facts.Reason=facts.Editable?null:"Locked by the server through "+(owner.Charter?"Charter":owner.Conditional?"ConditionalConfigSync":"ServerSync")+".";
                        // Both systems' native entry handlers own validation, forwarding and rejection.
                        if(facts.Editable){facts.RequiresOwnerWrite=true;facts.OwnerWrite=value=>WriteThroughOwner(owner,entry,value);}
                        if(facts.Editable&&facts.EntrySynced&&owner.Charter)facts.ConfirmWrite=value=>CharterConfirmation(owner,entry,value);
                    }
                }
                else if(attributes.IsAdminOnly==true)
                {
                    facts.DeclaredScope=jotunn.Manages(entry)?ConfigScope.ServerAuthoritative:ConfigScope.Local;
                    facts.EntrySynced=Remote&&jotunn.Manages(entry);facts.Editable=!facts.EntrySynced;
                    if(facts.EntrySynced)
                    {
                        facts.Editable=jotunn.CanAmend(entry,out var reason);facts.Reason=facts.Editable?null:reason;
                        if(facts.Editable){facts.RequiresOwnerWrite=true;facts.OwnerWrite=value=>jotunn.Amend(entry,value);}
                    }
                }
                else
                {
                    bool unknownSynced=entry.Description.Tags.Any(tag=>tag!=null&&tag.GetType().Name.StartsWith("SyncedConfigEntry",StringComparison.Ordinal));
                    bool managedName=System.IO.Path.GetFileName(entry.ConfigFile.ConfigFilePath).StartsWith("com.meadworks.",StringComparison.OrdinalIgnoreCase);
                    facts.Editable=!unknownSynced&&!managedName&&!scanFailed;
                    facts.Known=facts.Editable;
                    if(facts.Known)facts.DeclaredScope=ConfigScope.Local;
                    facts.Reason=facts.Editable?null:"The configuration owner could not be verified.";
                }
                if(attributes.ReadOnly==true||attributes.CharterReadOnly||attributes.GenericReadOnly)
                {
                    // A more specific lock already named its reason (a server lock the blocked-options layer also tags read-only): keep it.
                    if(facts.Editable||!facts.Known||string.IsNullOrEmpty(facts.Reason))facts.Reason="The owning mod or server marks this setting read-only.";
                    facts.Editable=false;
                }
                if(facts.ConnectedAsClient&&facts.EntrySynced&&facts.Editable&&!owners.ContainsKey(entry))facts.ConfirmWrite=value=>jotunn.Confirm(entry,value);
                return facts;
            }
            catch(Exception error){facts.Known=false;facts.Editable=false;facts.Reason="Configuration ownership unavailable: "+error.GetBaseException().Message;return facts;}
        }
        /// <summary>A MeadWorks cheat whose "Players may use" choice this player may change (host, admin or single player).</summary>
        static bool PlayerChoice(ConfigEntryBase entry)=>MeadworksMischief.IsCheat(entry,out _)&&MeadworksConfig.TryGetMetadata(entry,out var metadata)
            &&MeadworksBlockedOptions.QueryEdit(metadata.ProductId,"Mischief.Players May Use",ConfigScope.ServerAuthoritative).Allowed;
        /// <summary>Owner button caption for MeadWorks cheats: who may use it on this server; null for other entries.</summary>
        public static string? OwnershipLabel(ConfigEntryBase entry)=>MeadworksMischief.IsCheat(entry,out _)?(MeadworksMischief.PlayersMay(entry)?"Players may use":"Admins only"):null;
        public bool CanChangeOwnership(ConfigEntryBase entry)
        {
            try{if(PlayerChoice(entry))return true;}catch{return false;}
            try{Refresh();return owners.TryGetValue(entry,out var owner)&&owner.Conditional
                &&CurrentOwner(owner,entry)&&Flag(owner.Data,"CanChangeSynchronizationPolicy")&&!HiddenByServerPolicy(entry);}
            catch{return false;}
        }
        public bool ToggleOwnership(ConfigEntryBase entry)
        {
            if(!CanChangeOwnership(entry))return false;
            if(PlayerChoice(entry))return MeadworksMischief.SetPlayersMay(entry,!MeadworksMischief.PlayersMay(entry));
            var owner=owners[entry];var toggle=owner.Data.GetType().GetMethod("ToggleSynchronizationPolicy",Members,null,Type.EmptyTypes,null);
            if(toggle==null)return false;toggle.Invoke(owner.Data,null);nextScan=0;return true;
        }
        public void Dispose(){disposed=true;owners.Clear();ambiguous.Clear();syncedFiles.Clear();jotunn.Dispose();}
    }
}
