using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
namespace ExtraPotions.Meadworks.Crucible {
    public static partial class MeadworksConfig {
        /// <summary>A Meadworks-bound entry whose value comes from the server on a connected client: cheats, and locked server settings.</summary>
        internal static bool ServerBound(ConfigEntryBase entry,out string product){
            product="";
            if(entry==null||!managedEntries.TryGetValue(entry,out var data)||!data.Bound)return false;
            product=data.Product;
            return data.Scope==ConfigScope.Cheat||data.Scope==ConfigScope.ServerAuthoritative&&data.ServerLocked;
        }
    }

    /// <summary>
    /// Keeps the server's values on a connected client (C01, in-game reports 2026-10-08). Jotunn writes the server's
    /// values into synchronized entries and blocks file reloads for them, but a direct write (another mod, a configuration
    /// UI, code) replaced the server value on that client only, and products read the entry. This guard records the
    /// values Jotunn applies (<c>SynchronizationManager.ApplyConfigZPackage</c>, and every entry of the products in the
    /// initial synchronization) and writes the server value back when a non-administrator changes a server-bound entry.
    /// Administrators' writes are amendments and stand. Forgotten on disconnect (before Jotunn restores the player's own
    /// values in its <c>ZNet.OnDestroy</c> prefix). Off, with a warning, when Jotunn's apply step cannot be hooked.
    /// </summary>
    internal static class SyncedConfigGuard {
        static readonly SyncedValueLedger Ledger=new SyncedValueLedger();
        static readonly HashSet<ConfigFile> Files=new HashSet<ConfigFile>();
        static readonly HashSet<ConfigEntryBase> Reported=new HashSet<ConfigEntryBase>();
        static Harmony? harmony;
        static bool active;
        static int applying;
        static bool restoring;

        internal static void Install(){
            if(harmony!=null)return;
            harmony=new Harmony(Plugin.Guid+".synced-config");
            var apply=AccessTools.Method(typeof(SynchronizationManager),"ApplyConfigZPackage");
            if(apply==null){Plugin.Log?.LogWarning("[Meadworks:Crucible] Jotunn's configuration apply step was not found; server values on clients are not re-applied after local changes.");return;}
            try{
                harmony.Patch(apply,prefix:new HarmonyMethod(typeof(SyncedConfigGuard),nameof(ApplyPrefix)),finalizer:new HarmonyMethod(typeof(SyncedConfigGuard),nameof(ApplyFinalizer)));
                var forget=new HarmonyMethod(typeof(SyncedConfigGuard),nameof(ForgetPrefix)){priority=Priority.First};
                foreach(var name in new[]{"Shutdown","OnDestroy"}){var target=AccessTools.Method(typeof(ZNet),name);if(target!=null)harmony.Patch(target,prefix:forget);}
                SynchronizationManager.OnConfigurationSynchronized+=OnSynchronized;
                active=true;
            }catch(Exception error){
                harmony.UnpatchSelf();active=false;
                Plugin.Log?.LogWarning("[Meadworks:Crucible] Server-value guard is off: "+error.GetType().Name+": "+error.Message);
            }
        }

        internal static void Uninstall(){
            SynchronizationManager.OnConfigurationSynchronized-=OnSynchronized;
            foreach(var file in Files)file.SettingChanged-=OnChanged;
            Files.Clear();Forget();active=false;
            harmony?.UnpatchSelf();harmony=null;
        }

        /// <summary>Starts watching the file of a synchronized Meadworks entry.</summary>
        internal static void Watch(ConfigEntryBase entry){
            var file=entry?.ConfigFile;
            if(file!=null&&Files.Add(file))file.SettingChanged+=OnChanged;
        }

        /// <summary>Runs this machine's own published change (Crucible's Amend over Jotunn) as a server write.</summary>
        internal static void AsServer(Action apply){
            applying++;
            try{apply();}finally{applying--;}
        }

        static void ApplyPrefix()=>applying++;
        static void ApplyFinalizer()=>applying=Math.Max(0,applying-1);
        static void ForgetPrefix()=>Forget();

        static void Forget(){Ledger.Forget();Reported.Clear();}

        static bool RemoteClient(){
            var net=ZNet.instance;
            return net!=null&&!net.IsServer()&&net.GetServerRPC()!=null;
        }

        static void OnSynchronized(object sender,ConfigurationSynchronizationEventArgs args){
            if(!active||!args.InitialSynchronization||!RemoteClient())return;
            // The initial package carries every synchronized value of each listed product, also those equal to the
            // player's own (which raise no change event), so record them all.
            var products=args.UpdatedPluginGUIDs;
            foreach(var file in Files)
                foreach(var pair in file)
                    if(MeadworksConfig.ServerBound(pair.Value,out var product)&&products!=null&&products.Contains(product))Ledger.Record(pair.Value,pair.Value.BoxedValue);
        }

        static void OnChanged(object sender,SettingChangedEventArgs args){
            var entry=args?.ChangedSetting;
            if(!active||restoring||entry==null)return;
            var remote=RemoteClient();
            var bound=MeadworksConfig.ServerBound(entry,out var product);
            var writer=!remote?SyncedWriter.Inactive
                :applying>0?(bound?SyncedWriter.Server:SyncedWriter.Inactive)
                :!bound?SyncedWriter.Inactive
                :SynchronizationManager.Instance.PlayerIsAdmin?SyncedWriter.Administrator
                :SyncedWriter.Player;
            if(Ledger.Observe(entry,entry.BoxedValue,writer,out var server)!=SyncedChange.Restore)return;
            var file=entry.ConfigFile;var save=file.SaveOnConfigSet;
            restoring=true;file.SaveOnConfigSet=false;
            try{entry.BoxedValue=server;}
            finally{file.SaveOnConfigSet=save;restoring=false;}
            if(Reported.Add(entry))
                Plugin.Log?.LogInfo("[Meadworks:Crucible] "+product+" "+entry.Definition.Section+" / "+entry.Definition.Key+": the server's value "+server+" stays in effect; a local change is ignored while connected.");
        }
    }
}
