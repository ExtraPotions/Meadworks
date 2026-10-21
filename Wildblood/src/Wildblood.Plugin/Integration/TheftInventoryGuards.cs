// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Reused from Timber8b9c5ad DeliveryInventoryGuards; player-only hold/hydration/deferred-death guards adapted from Burrow14f92c8 NativeInventoryGuards and SharedTransferClient.
#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Transfers;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Transfers {
    internal static class TheftInventoryGuards {
        internal static bool Applying;
        static readonly HashSet<Player> loading=new HashSet<Player>(),deaths=new HashSet<Player>();
        static readonly HashSet<Inventory> snapshots=new HashSet<Inventory>();
        internal static void LoadSnapshot(Inventory inventory,Action load){snapshots.Add(inventory);try{load();}finally{snapshots.Remove(inventory);}}
        static bool foreignWarned;
        static int mainThread;
        internal static void Initialize()=>mainThread=Thread.CurrentThread.ManagedThreadId;
        static void MainThread(){if(mainThread!=Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("Wildblood inventory hold requires the Unity thread.");}
        internal static bool Loading(Player player)=>loading.Contains(player);
        internal static bool Export(object value){MainThread();return value is Inventory inventory&&(TheftTransfers.RawHeld(inventory)||NativeRaidChest.Held(inventory));}
        internal static bool ForeignHeld(Inventory inventory,bool admission=false){
            MainThread();if(!CruciblePlugin.IsReady)return admission;
            var caps=CruciblePlugin.Services.Capabilities;
            foreach(var owner in new[]{"burrow"}) {
                string id="meadworks."+owner+".inventory_hold",guid="com.meadworks."+owner;
                var providers=caps.Providers(id);
                // Burrow registers its hold in Awake, before any hold can exist: no provider means no owner.
                if(providers.Count==0)continue;
                if(providers.Count!=1||providers[0].Owner!=guid||!caps.TryResolve<Func<object,bool>>(id,new System.Version(1,0),out var held)){Warn("Inventory hold capability is ambiguous or incompatible.");return true;}
                try{if(held(inventory))return true;}catch{Warn("Inventory hold capability failed.");return true;}
            }
            return false;
        }
        static void Warn(string text){if(foreignWarned)return;foreignWarned=true;TheftTransfers.Warn("Wildblood theft refused: "+text);}
        static bool OwnHeld(Inventory inventory)=>!Applying&&!snapshots.Contains(inventory)&&(TheftTransfers.RawHeld(inventory)||NativeRaidChest.Held(inventory));
        internal static bool Held(Inventory inventory)=>!Applying&&!snapshots.Contains(inventory)&&(TheftTransfers.RawHeld(inventory)||NativeRaidChest.Held(inventory)||ForeignHeld(inventory));
        internal static void DrainDeaths(){foreach(var player in deaths.ToArray()){if(!player||player!=Player.m_localPlayer){deaths.Remove(player);continue;}if(Held(player.GetInventory()))continue;deaths.Remove(player);if(!player.IsDead()&&player.GetHealth()<=0)player.OnDeath();}}
        [HarmonyPatch(typeof(PlayerProfile),nameof(PlayerProfile.LoadPlayerData))]
        internal static class TheftProfileLoadGuard {
            static void Prefix(Player __0){loading.Add(__0);TheftTransfers.ProfileLoading(__0);}
            static Exception Finalizer(Player __0,Exception __exception){loading.Remove(__0);return __exception;}
        }
        [HarmonyPatch(typeof(Player),nameof(Player.OnDeath))]
        internal static class TheftDeathGuard {
            [HarmonyPriority(Priority.First)]
            static bool Prefix(Player __instance){if(__instance!=Player.m_localPlayer||!Held(__instance.GetInventory()))return true;deaths.Add(__instance);return false;}
        }
        // Void removals made while their inventory was held, applied in order once it is free (ExtraPotions.Wildblood.Transfers.TheftHoldRules).
        static readonly DeferredWrites<Inventory,(MethodBase Method,object[] Args)> deferredRemovals=new DeferredWrites<Inventory,(MethodBase,object[])>();
        static bool replaying,warnedDeferLimit;
        internal static int DeferredRemovals=>deferredRemovals.Count;
        internal static void DrainRemovals()=>deferredRemovals.Drain(Held,Replay);
        /// <summary>Shutdown that removes the guard: kept removals can no longer wait, so they run now instead of being lost.</summary>
        internal static void FlushRemovals()=>deferredRemovals.Drain(_=>false,Replay);
        static void Replay(Inventory inventory,(MethodBase Method,object[] Args) call){
            replaying=true;
            try{call.Method.Invoke(inventory,call.Args);}
            catch(Exception error){TheftTransfers.Warn("Wildblood: a removal kept during an inventory hold failed when applied: "+(error.InnerException??error).GetType().Name);}
            finally{replaying=false;}
        }
        // Inventories whose Inventory.Load is running: the game clears the list and adds every loaded item back through AddItem.
        static readonly Dictionary<Inventory,int> inventoryLoads=new Dictionary<Inventory,int>();
        internal static bool InventoryLoading(Inventory inventory)=>inventory!=null&&inventoryLoads.ContainsKey(inventory);
        /// <summary>Marks the adds made by <c>Inventory.Load</c> as part of the load, so a held inventory reloads completely
        /// (same marker as Burrow's InventoryLoads). Load itself is never blocked.</summary>
        [HarmonyPatch]
        internal static class TheftInventoryLoadsGuard {
            static IEnumerable<MethodBase> TargetMethods()=>typeof(Inventory).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name=="Load");
            [HarmonyPriority(Priority.First)]
            static void Prefix(Inventory __instance){inventoryLoads.TryGetValue(__instance,out int depth);inventoryLoads[__instance]=depth+1;}
            static Exception Finalizer(Inventory __instance,Exception __exception){if(inventoryLoads.TryGetValue(__instance,out int depth)){if(depth<=1)inventoryLoads.Remove(__instance);else inventoryLoads[__instance]=depth-1;}return __exception;}
        }
        /// <summary>
        /// Writes to a held inventory. Methods that answer get false or null and change nothing; void removals are kept and
        /// applied when the hold ends; void moves are skipped on both sides; Load is not blocked and the adds it makes to the
        /// same inventory always run (TheftInventoryLoadsGuard), so a reload during a hold never empties it in memory.
        /// Runs after Burrow's guard (same rules): a call Burrow already refused, kept or skipped is left alone, so a kept
        /// removal is applied exactly once when both products are installed.
        /// </summary>
        [HarmonyPatch]
        internal static class TheftInventoryWritesGuard {
            static IEnumerable<MethodBase> TargetMethods()=>typeof(Inventory).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>TheftHoldRules.Guarded.Contains(m.Name));
            [HarmonyPriority(Priority.Last)]
            static bool Prefix(Inventory __instance,object[] __args,MethodBase __originalMethod,bool __runOriginal=true){
                if(!__runOriginal)return false;
                if(replaying)return true;
                // Only Wildblood's own holds are enforced here. Burrow's holds are enforced by Burrow's own guard, which runs first
                // (a call it refused, kept or skipped arrives with __runOriginal false); repeating them here refused the adds of
                // Inventory.Load on a chest Burrow holds, which Burrow lets through, and left the chest empty in memory.
                bool held=OwnHeld(__instance)||__args.OfType<Inventory>().Any(OwnHeld);
                switch(TheftHoldRules.Decide(__originalMethod.Name,__originalMethod is MethodInfo info&&info.ReturnType==typeof(void),held,__runOriginal,InventoryLoading(__instance))){
                    case HeldWrite.Run:return true;
                    case HeldWrite.Defer:
                        if(deferredRemovals.TryAdd(__instance,(__originalMethod,(object[])__args.Clone())))return false;
                        if(!warnedDeferLimit){warnedDeferLimit=true;Plugin.NativeLogger?.LogWarning("Wildblood: too many removals waiting for an inventory hold; further ones run at once.");}
                        return true;
                    default:return false; // Refuse: answers false or null, nothing changed. Neutral: neither side moved.
                }
            }
        }
        [HarmonyPatch]
        internal static class TheftPlayerActionsGuard {
            static IEnumerable<MethodBase> TargetMethods(){foreach(var type in new[]{typeof(Player),typeof(Humanoid),typeof(InventoryGui),typeof(StoreGui),typeof(Fireplace),typeof(Smelter),typeof(CookingStation),typeof(Fermenter),typeof(ItemStand),typeof(ArmorStand),typeof(OfferingBowl)})foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))if(new[]{"DropItem","ConsumeItem","UseItem","DoCrafting","TryPlacePiece","BuySelectedItem","SellSelectedItem","Interact","OnAddFuel","OnAddOre","OnAddItem"}.Contains(method.Name)&&!(type==typeof(Player)&&method.Name=="Interact"))yield return method;}
            [HarmonyPriority(Priority.First)]
            static bool Prefix(object __instance,object[] __args){if(Applying)return true;if(__instance is Humanoid user&&Held(user.GetInventory()))return false;if(__args.OfType<Humanoid>().Any(h=>Held(h.GetInventory()))||__args.OfType<Inventory>().Any(Held))return false;return !(__instance is InventoryGui||__instance is StoreGui)||!Player.m_localPlayer||!Held(Player.m_localPlayer.GetInventory());}
        }
    }
}
