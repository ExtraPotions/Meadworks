// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Snapshot of an exact active first-party Charter clause. Requery before use.</summary>
    public sealed class ConfigClauseRegistration {
        public string ProductId { get; }
        public ConfigScope Scope { get; }
        public object OwnValue { get; }
        public bool IsBound { get; }
        public bool MayAmend { get; }
        internal ConfigClauseRegistration(string product,bool local,object own,bool bound,bool mayAmend){
            ProductId=product;Scope=local?ConfigScope.Local:ConfigScope.ServerAuthoritative;
            OwnValue=own;IsBound=bound;MayAmend=mayAmend;
        }
    }

    public static partial class MeadworksConfig {
        static object? ClauseMember(object target,string name){
            var type=target as Type??target.GetType();var instance=target is Type?null:target;
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
            for(var current=type;current!=null;current=current.BaseType){
                var property=current.GetProperty(name,flags);if(property!=null)return property.GetValue(instance);
                var field=current.GetField(name,flags);if(field!=null)return field.GetValue(instance);
            }
            return null;
        }

        /// <summary>Reads only Burrow/Mantle's established embedded Charter registrations; never infers ownership from a filename or an entry tag.</summary>
        public static bool TryGetRegisteredClause(ConfigEntryBase entry,out ConfigClauseRegistration registration){
            registration=null!;
            if(entry==null||!entry.ConfigFile.ContainsKey(entry.Definition)||!ReferenceEquals(entry.ConfigFile[entry.Definition],entry))return false;
            try{
                object? found=null;ConfigClauseRegistration? result=null;
                // These are the same two adopted registries supported by Almanac's existing LockResolver.
                foreach(var product in new[]{"Burrow","Mantle"}){
                    string guid="com.meadworks."+product.ToLowerInvariant();
                    if(!Chainloader.PluginInfos.TryGetValue(guid,out var plugin)||plugin.Instance==null
                        ||!ReferenceEquals(plugin.Instance.Config,entry.ConfigFile))continue;
                    var assembly=plugin.Instance.GetType().Assembly;
                    string ns="ExtraPotions."+product+".Embedded.Charter";
                    var registry=assembly.GetType(ns+".GameHooks",false);
                    if(registry==null||ClauseMember(registry,"charters") is not IEnumerable systems
                        ||ClauseMember(plugin.Instance.GetType(),"Synced") is not object facade
                        ||!ReferenceEquals(ClauseMember(facade,"Config"),entry.ConfigFile)
                        ||ClauseMember(facade,"Sync") is not object active)continue;
                    foreach(var system in systems){
                        if(system==null||!ReferenceEquals(system,active)||system.GetType().Assembly!=assembly
                            ||system.GetType().FullName!=ns+".Charter"||ClauseMember(system,"Guid") is not string id||id!=guid
                            ||ClauseMember(system,"Ledger") is not object ledger||ClauseMember(ledger,"Clauses") is not IEnumerable clauses)continue;
                        foreach(var clause in clauses){
                            if(clause==null||!ReferenceEquals(ClauseMember(clause,"Entry"),entry))continue;
                            var type=clause.GetType();
                            if(type.Assembly!=assembly||!type.IsGenericType||type.GetGenericTypeDefinition().FullName!=ns+".Clause`1"
                                ||type.GetGenericArguments()[0]!=entry.SettingType||ClauseMember(clause,"Local") is not bool local
                                ||ClauseMember(clause,"OwnValue") is not object own||own.GetType()!=entry.SettingType
                                ||ClauseMember(system,"IsBound") is not bool bound||ClauseMember(system,"MayAmend") is not bool mayAmend)return false;
                            if(found!=null&&!ReferenceEquals(found,clause))return false;
                            found=clause;result=new ConfigClauseRegistration(id,local,own,bound,mayAmend);
                        }
                    }
                }
                if(result==null)return false;registration=result;return true;
            }catch{return false;}
        }
    }
}
