// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using HarmonyLib;
using BepInEx.Configuration;

namespace ExtraPotions.Almanac.Runtime.Locks
{
    /// <summary>Optional native Jotunn route, reusing Crucible's cache/apply/send sequence. Almanac admin qualification is pending.</summary>
    internal sealed class NativeJotunn:IDisposable
    {
        const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        readonly Type? managerType=AppDomain.CurrentDomain.GetAssemblies().Select(assembly=>assembly.GetType("Jotunn.Managers.SynchronizationManager",false)).FirstOrDefault(type=>type!=null);
        readonly EventInfo? syncEvent;
        readonly Delegate? syncHandler;
        readonly HashSet<string> received=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,long> receipts=new Dictionary<string,long>(StringComparer.Ordinal);
        long sequence;
        ZNet? network;
        ZRpc? server;
        bool listening;
        static NativeJotunn? observer;
        readonly Harmony receivePatch=new Harmony("com.meadworks.almanac.jotunn-receipts");
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        int receiveDepth;
        internal bool Receiving=>receiveDepth!=0&&Thread.CurrentThread.ManagedThreadId==thread;
        internal NativeJotunn()
        {
            try
            {
                syncEvent=managerType?.GetEvent("OnConfigurationSynchronized",Members);
                if(syncEvent?.EventHandlerType==null)return;
                syncHandler=Delegate.CreateDelegate(syncEvent.EventHandlerType,this,GetType().GetMethod(nameof(Synchronized),Members)!);
                var receive=Method("ConfigRPC_OnClientReceive",typeof(long),typeof(ZPackage));
                if(receive==null||observer!=null)return;
                receivePatch.Patch(receive,postfix:new HarmonyMethod(GetType().GetMethod(nameof(ObserveReceive),Members)!));
                syncEvent.AddEventHandler(null,syncHandler);listening=true;observer=this;
            }
            catch
            {
                receivePatch.UnpatchSelf();
                if(syncEvent!=null&&syncHandler!=null)try{syncEvent.RemoveEventHandler(null,syncHandler);}catch{}
                listening=false;
            }
        }
        // Native Jotunn also emits OnConfigurationSynchronized synchronously after a local send.
        // Trust GUID receipts only inside its actual server-package receive iterator; do not alter
        // the package, apply path, coroutine result or Jotunn's relay (which excludes the sender).
        static void ObserveReceive(object __instance,long __0,ref IEnumerator __result)
        {
            var owner=observer;
            if(owner==null||!owner.listening||Thread.CurrentThread.ManagedThreadId!=owner.thread
                ||!ReferenceEquals(__instance,owner.Manager)||!owner.ObserveConnection()
                ||ZRoutedRpc.instance==null||__0!=ZRoutedRpc.instance.GetServerPeerID()||__0==0||__result==null)return;
            __result=new Receive(owner,__result,__0);
        }
        sealed class Receive:IEnumerator,IDisposable
        {
            readonly NativeJotunn owner;readonly IEnumerator original;readonly long sender;
            readonly ZNet? net;readonly ZRpc? rpc;readonly long generation,world;
            internal Receive(NativeJotunn owner,IEnumerator original,long sender)
            {
                this.owner=owner;this.original=original;this.sender=sender;net=owner.network;rpc=owner.server;
                var session=ExtraPotions.Meadworks.Crucible.Plugin.Session;generation=session.Generation;world=session.WorldId;
            }
            public object Current=>original.Current;
            public bool MoveNext()
            {
                var session=ExtraPotions.Meadworks.Crucible.Plugin.Session;
                bool valid=ReferenceEquals(observer,owner)&&owner.listening&&Thread.CurrentThread.ManagedThreadId==owner.thread
                    &&owner.ObserveConnection()&&ReferenceEquals(net,owner.network)&&ReferenceEquals(rpc,owner.server)
                    &&session.Generation==generation&&session.WorldId==world&&ZRoutedRpc.instance!=null
                    &&sender==ZRoutedRpc.instance.GetServerPeerID();
                if(!valid)return original.MoveNext();
                owner.receiveDepth++;
                try{return original.MoveNext();}finally{owner.receiveDepth--;}
            }
            public void Reset()=>original.Reset();
            public void Dispose(){(original as IDisposable)?.Dispose();}
        }
        bool ObserveConnection()
        {
            var net=ZNet.instance;var rpc=net!=null&&!net.IsServer()?net.GetServerRPC():null;
            if(!ReferenceEquals(network,net)||!ReferenceEquals(server,rpc)){network=net;server=rpc;received.Clear();receipts.Clear();}
            if(net==null||rpc==null||!rpc.IsConnected()){received.Clear();receipts.Clear();return false;}
            return true;
        }
        void Synchronized(object sender,EventArgs args)
        {
            if(!Receiving||!ObserveConnection()||LockResolver.Read(args,"UpdatedPluginGUIDs") is not IEnumerable guids)return;
            foreach(var guid in guids)if(guid is string value){received.Add(value);receipts[value]=++sequence;}
        }
        internal Func<bool> Confirm(ConfigEntryBase entry,object? value)
        {
            if(!ObserveConnection())return ()=>false;
            var manager=Manager;var identify=Method("GetPluginGUID",typeof(ConfigFile),typeof(string).MakeByRefType());object?[] args={entry.ConfigFile,null};
            if(manager==null||identify==null||Invoke(identify,manager,args) is not true||args[1] is not string guid)return ()=>false;
            receipts.TryGetValue(guid,out long before);var net=network;var rpc=server;
            var session=ExtraPotions.Meadworks.Crucible.Plugin.Session;long generation=session.Generation;var world=session.WorldId;
            string expected=TomlTypeConverter.ConvertToString(value,entry.SettingType);
            return ()=>{
                if(!ObserveConnection()||!ReferenceEquals(net,network)||!ReferenceEquals(rpc,server))return false;
                var current=ExtraPotions.Meadworks.Crucible.Plugin.Session;
                return current.AuthorityReady&&current.Generation==generation&&current.WorldId==world
                    &&receipts.TryGetValue(guid,out var after)&&after>before
                    &&string.Equals(TomlTypeConverter.ConvertToString(entry.BoxedValue,entry.SettingType),expected,StringComparison.Ordinal);
            };
        }
        object? Manager=>managerType==null?null:LockResolver.Read(managerType,"Instance");
        MethodInfo? Method(string name,params Type[] arguments)=>managerType?.GetMethod(name,Members,null,arguments,null);
        static object? Invoke(MethodInfo method,object manager,params object?[] arguments)
        {
            try{return method.Invoke(manager,arguments);}
            catch(TargetInvocationException error) when(error.InnerException!=null){ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }
        // Unknown means potentially synced: callers refuse or suppress saving rather than assuming own values.
        internal bool Manages(ConfigEntryBase entry)
        {
            try{var manager=Manager;var method=Method("ShouldManageConfig",typeof(ConfigFile));return manager==null||method==null||Invoke(method,manager,entry.ConfigFile) is not bool managed||managed;}
            catch{return true;}
        }
        internal bool CanAmend(ConfigEntryBase entry,out string reason)
        {
            reason="No verified Jotunn admin path; change this setting in the server configuration.";
            try
            {
                if(!listening||!ObserveConnection()||managerType==null)return false;
                var manager=Manager;if(manager==null||!Manages(entry))return false;
                var attributes=entry.Description.Tags.FirstOrDefault(tag=>tag!=null&&tag.GetType().Assembly==managerType.Assembly
                    &&tag.GetType().Name=="ConfigurationManagerAttributes");
                if(attributes==null||LockResolver.Read(attributes,"IsAdminOnly") is not true)return false;
                if(LockResolver.Read(manager,"PlayerIsAdmin") is not true||LockResolver.Read(attributes,"IsUnlocked") is not true)
                {reason="Locked by the server through Jotunn; administrator access is required.";return false;}
                var identify=Method("GetPluginGUID",typeof(ConfigFile),typeof(string).MakeByRefType());
                object?[] identity={entry.ConfigFile,null};
                if(identify==null||Invoke(identify,manager,identity) is not true||identity[1] is not string guid
                    ||!received.Contains(guid)||LockResolver.Read(manager,"localValues") is not IDictionary local||!local.Contains(entry))
                {reason="Waiting for this connection's Jotunn configuration.";return false;}
                if(Method("CacheConfigurationValues")==null||Method("SynchronizeChangedConfig")==null)return false;
                return true;
            }
            catch(Exception error){reason="Jotunn ownership unavailable: "+error.GetBaseException().Message;return false;}
        }
        internal bool Amend(ConfigEntryBase entry,object? value)
        {
            if(!CanAmend(entry,out _))return false;
            var manager=Manager;var cache=Method("CacheConfigurationValues");var send=Method("SynchronizeChangedConfig");
            if(manager==null||cache==null||send==null)return false;
            bool save=entry.ConfigFile.SaveOnConfigSet;entry.ConfigFile.SaveOnConfigSet=false;
            try
            {
                Invoke(cache,manager);
                if(!CanAmend(entry,out _)||!ReferenceEquals(manager,Manager))return false;
                string requested=TomlTypeConverter.ConvertToString(value,entry.SettingType);
                entry.BoxedValue=value;
                if(!string.Equals(requested,TomlTypeConverter.ConvertToString(entry.BoxedValue,entry.SettingType),StringComparison.Ordinal))
                    throw new InvalidOperationException("The mod did not retain the requested Jotunn value.");
                if(!CanAmend(entry,out _)||!ReferenceEquals(manager,Manager))
                    throw new InvalidOperationException("Jotunn administrator authority changed before publication.");
                Invoke(send,manager);return true; // Submission only; the server still owns acceptance/correction.
            }
            finally{entry.ConfigFile.SaveOnConfigSet=save;}
        }
        public void Dispose()
        {
            if(listening&&syncEvent!=null&&syncHandler!=null)syncEvent.RemoveEventHandler(null,syncHandler);
            if(ReferenceEquals(observer,this))observer=null;
            receivePatch.UnpatchSelf();listening=false;received.Clear();receipts.Clear();network=null;server=null;
        }
    }
}
