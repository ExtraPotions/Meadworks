using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExtraPotions.Burrow;
using HarmonyLib;

namespace Burrow.Resources {
    internal static class NativeInventoryGuards {
        internal static bool Applying;
        internal static bool OwnHeld(object value)=>value is Inventory inventory&&(Plugin.Instance?.Resources?.IsLocked(inventory)==true||Plugin.Instance?.Transfers?.IsLocked(inventory)==true);
        internal static bool Held(Inventory inventory)=>Held(inventory,false);
        internal static bool Held(Inventory inventory,bool admission)=>!Applying&&(OwnHeld(inventory)||ForeignInventoryHeld(inventory,admission));
        static bool warnedForeignHold;
        // Wildblood's inventory hold; it must come from its owner (capability owner pin).
        static readonly string[] HoldIds={"meadworks.wildblood.inventory_hold"};
        static readonly string[] HoldOwners={"com.meadworks.wildblood"},HoldNames={"wildblood"};
        const int WildbloodHold=0;
        static readonly Func<object,bool>?[] holds=new Func<object,bool>?[1];
        static readonly bool[] holdInvalid=new bool[1];
        static ExtraPotions.Meadworks.Crucible.CapabilityRegistry? holdRegistry;
        static int holdGeneration=-1;
        // Coordinator ruling 2026-10-13: Wildblood's meadworks.owns.elite_creatures stays registered while it is stopped and
        // answers false then; a stopped Wildblood may still hold unsaved native state, so new transfers refuse.
        static readonly ExtraPotions.Meadworks.Crucible.FeatureLink wildbloodRunning=new ExtraPotions.Meadworks.Crucible.FeatureLink(
            ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.Id(ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.EliteCreatures));

        /// <summary>The hold providers again only when the registry changed: the inventory asks several times a frame.</summary>
        static void ResolveHolds(ExtraPotions.Meadworks.Crucible.CapabilityRegistry capabilities){
            if(ReferenceEquals(capabilities,holdRegistry)&&capabilities.Generation==holdGeneration)return;
            holdRegistry=capabilities;holdGeneration=capabilities.Generation;
            for(int i=0;i<HoldIds.Length;i++){
                holds[i]=null;holdInvalid[i]=false;
                try{
                    var providers=capabilities.Providers(HoldIds[i]);
                    if(providers.Count==0)continue;
                    if(providers.Count==1&&providers[0].Owner==HoldOwners[i]&&capabilities.TryResolve<Func<object,bool>>(HoldIds[i],new System.Version(1,0),out var held)&&held!=null)holds[i]=held;
                    else holdInvalid[i]=true;
                }catch{holdInvalid[i]=true;}
            }
        }

        internal static bool ForeignInventoryHeld(Inventory inventory,bool admission=false){
            if(inventory==null||!ExtraPotions.Meadworks.Crucible.Plugin.IsReady)return false;
            ExtraPotions.Meadworks.Crucible.CapabilityRegistry capabilities;
            try{capabilities=ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities;ResolveHolds(capabilities);}
            catch{return Unavailable("foreign");}
            for(int i=0;i<HoldIds.Length;i++){
                var held=holds[i];
                if(held!=null){
                    try{if(held(inventory))return true;continue;}
                    catch{return Unavailable(HoldNames[i]);}
                }
                if(holdInvalid[i])return Unavailable(HoldNames[i]);
                // No hold provider: only new transfers refuse a Wildblood that is installed but not running.
                if(i==WildbloodHold&&admission&&wildbloodRunning.TryOwned(capabilities,out bool running)&&!running)return Unavailable(HoldNames[i]);
            }
            return false;
        }
        static bool Unavailable(string what){
            if(!warnedForeignHold){warnedForeignHold=true;Plugin.Log?.LogWarning(what+" inventory hold unavailable; inventory mutation or transfer admission is deferred.");}
            return true;
        }
        static readonly HashSet<Player> deferredDeaths=new HashSet<Player>();
        internal static void DrainDeaths(){if(deferredDeaths.Count==0)return;foreach(var player in deferredDeaths.ToArray()){if(!player||player!=Player.m_localPlayer){deferredDeaths.Remove(player);continue;}if(Held(player.GetInventory()))continue;deferredDeaths.Remove(player);if(!player.IsDead()&&player.GetHealth()<=0)player.OnDeath();}}
        [HarmonyPatch(typeof(Player),nameof(Player.OnDeath))]
        internal static class Death {
            [HarmonyPriority(Priority.First)]
            // A death another mod's earlier prefix already cancelled is not kept, so it is never replayed.
            static bool Prefix(Player __instance,bool __runOriginal){if(!__runOriginal)return false;if(__instance!=Player.m_localPlayer||!Held(__instance.GetInventory()))return true;deferredDeaths.Add(__instance);return false;}
        }
        /// <summary>
        /// A pickup while the local player's inventory is held waits on the ground. The held add answers false, which the
        /// game's Humanoid.Pickup reports as "no room" with free cells left; skipped here before that, it stays silent, and
        /// auto pickup, Magpie and Wildroot's bulk harvest try the drop again once the hold ends.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid),nameof(Humanoid.Pickup))]
        internal static class PickupWait {
            [HarmonyPriority(Priority.High)]
            static bool Prefix(Humanoid __instance,ref bool __result,bool __runOriginal){
                if(!__runOriginal)return false;
                if(__instance==null||__instance!=Player.m_localPlayer||!Held(__instance.GetInventory()))return true;
                __result=false;return false;
            }
        }
        internal static bool ForeignHold(ZNetView view){
            if(!view||!view.IsValid())return false;var zdo=view.GetZDO();if(string.IsNullOrEmpty(zdo.GetString(NativeBundleStore.HoldKey)))return false;
            long until=zdo.GetLong(NativeBundleStore.HoldUntil);return until==0||DateTime.UtcNow.Ticks<until;
        }
        // Void removals made while their inventory was held, applied in order once it is free (ExtraPotions.Burrow.InventoryHoldRules).
        static readonly List<(Inventory Inventory,MethodBase Method,object[] Args)> deferredRemovals=new List<(Inventory,MethodBase,object[])>();
        static bool replaying,warnedDeferLimit;
        internal static int DeferredRemovals=>deferredRemovals.Count;
        internal static void DrainRemovals(){
            if(deferredRemovals.Count==0)return;
            for(int i=0;i<deferredRemovals.Count;){
                var (inventory,method,args)=deferredRemovals[i];
                if(Held(inventory)){i++;continue;}
                deferredRemovals.RemoveAt(i);
                replaying=true;
                try{method.Invoke(inventory,args);}
                catch(Exception error){Plugin.Log?.LogWarning("Burrow: a removal kept during an inventory hold failed when applied: "+(error.InnerException??error).GetType().Name);}
                finally{replaying=false;}
            }
        }
        // Inventories whose Inventory.Load is running: the game clears the list and adds every loaded item back through AddItem.
        static readonly Dictionary<Inventory,int> loading=new Dictionary<Inventory,int>();
        internal static bool Loading(Inventory inventory)=>inventory!=null&&loading.ContainsKey(inventory);
        [HarmonyPatch]
        internal static class InventoryLoads {
            static IEnumerable<MethodBase> TargetMethods()=>typeof(Inventory).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name=="Load");
            [HarmonyPriority(Priority.First)]
            static void Prefix(Inventory __instance){loading.TryGetValue(__instance,out int depth);loading[__instance]=depth+1;}
            static Exception? Finalizer(Inventory __instance,Exception? __exception){if(loading.TryGetValue(__instance,out int depth)){if(depth<=1)loading.Remove(__instance);else loading[__instance]=depth-1;}return __exception;}
        }
        /// <summary>
        /// Writes to a held inventory. Methods that answer get false or null and change nothing; void removals are kept and
        /// applied when the hold ends; void moves are skipped on both sides; Load always runs (ExtraPotions.Burrow.InventoryHoldRules).
        /// A call an earlier prefix (another mod's) already skipped stays skipped and is never kept, so Burrow never applies
        /// it later; Wildblood's guard runs after this one (Priority.Last) and does the same, so a kept removal exists once.
        /// </summary>
        [HarmonyPatch]
        internal static class InventoryWrites {
            static IEnumerable<MethodBase> TargetMethods()=>typeof(Inventory).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>InventoryHoldRules.Guarded.Contains(m.Name));
            static bool Prefix(Inventory __instance,object[] __args,MethodBase __originalMethod,bool __runOriginal){
                if(!__runOriginal)return false;
                if(replaying)return true;
                Inventory? held=Held(__instance)?__instance:__args.OfType<Inventory>().FirstOrDefault(Held);
                if(held==null)return true;
                var rule=InventoryHoldRules.Classify(__originalMethod.Name,__originalMethod is MethodInfo info&&info.ReturnType==typeof(void),Loading(__instance));
                if(rule==HeldWrite.Allow)return true;
                if(rule==HeldWrite.Defer){
                    if(deferredRemovals.Count>=InventoryHoldRules.MaxDeferred){
                        if(!warnedDeferLimit){warnedDeferLimit=true;Plugin.Log?.LogWarning("Burrow: too many removals waiting for an inventory hold; further ones run at once.");}
                        return true;
                    }
                    deferredRemovals.Add((__instance,__originalMethod,(object[])__args.Clone()));
                }
                // Refuse: the skipped method answers false (bool) or null (added item); nothing changed. Neutral: neither side moved.
                return false;
            }
        }
        /// <summary>
        /// A drag and drop that touches a held inventory does not run. The game's swap (a whole stack dropped on a different
        /// item) removes the dragged item first and then makes two moves; with the target held the moves were skipped after the
        /// removal and the item was lost. Answering false keeps the item on the cursor and in its inventory; the drop works again
        /// once the hold ends. A viewed shared chest's drop is routed by PanelRouting before this runs.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGrid),nameof(InventoryGrid.DropItem))]
        internal static class DropWait {
            [HarmonyPriority(Priority.VeryHigh)]
            static bool Prefix(InventoryGrid __instance,Inventory fromInventory,ItemDrop.ItemData item,ref int amount,Vector2i pos,ref bool __result,bool __runOriginal){
                if(!__runOriginal)return false;
                if(fromInventory!=null&&item!=null&&!fromInventory.ContainsItem(item)){__result=false;return false;}
                if(Applying||!Held(__instance.GetInventory())&&!Held(fromInventory))return true;
                var player=Player.m_localPlayer;var chest=InventoryGui.instance?.m_currentContainer;
                if(player&&chest&&fromInventory==chest.GetInventory()&&__instance.GetInventory()==player.GetInventory()){
                    int wanted=amount;amount=0;
                    Burrow.Shared.ChestWriter.TakeTo(chest,item,wanted,pos,null);
                    __result=true;return false;
                }
                __result=false;return false;
            }
        }
        // Owned chests use vanilla moves instead of PanelRouting. Keep their take actions too, before a guarded move
        // can silently fail. Shared/view-only chests retain PanelRouting's authority checks.
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.OnSelectedItem))]
        internal static class TakeWait {
            [HarmonyPriority(Priority.VeryHigh)]
            static bool Prefix(InventoryGui __instance,InventoryGrid grid,ItemDrop.ItemData item,InventoryGrid.Modifier mod,bool __runOriginal){
                if(!__runOriginal)return false;
                var player=Player.m_localPlayer;var chest=__instance.m_currentContainer;
                if(!player||!chest||__instance.m_dragGo!=null||item==null||item.m_shared.m_questItem||mod!=InventoryGrid.Modifier.Move
                    ||grid.GetInventory()!=chest.GetInventory()||Burrow.Shared.SharedState.IsViewing(chest)
                    ||!Held(player.GetInventory())&&!Held(chest.GetInventory()))return true;
                Burrow.Shared.ChestWriter.Take(chest,item,item.m_stack,null);return false;
            }
        }
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.OnTakeAll))]
        internal static class TakeAllWait {
            [HarmonyPriority(Priority.VeryHigh)]
            static bool Prefix(InventoryGui __instance,bool __runOriginal){
                if(!__runOriginal)return false;
                var player=Player.m_localPlayer;var chest=__instance.m_currentContainer;
                if(!player||!chest||Burrow.Shared.SharedState.IsViewing(chest)
                    ||!Held(player.GetInventory())&&!Held(chest.GetInventory()))return true;
                __instance.SetupDragItem(null,null,1);Burrow.Shared.ChestWriter.TakeAll(chest,null);return false;
            }
        }
        [HarmonyPatch(typeof(ZNetView),nameof(ZNetView.ClaimOwnership))]
        internal static class Ownership {
            static bool Prefix(ZNetView __instance)=>__instance.IsOwner()||!ForeignHold(__instance);
        }
        [HarmonyPatch]
        internal static class PlayerActions {
            static IEnumerable<MethodBase> TargetMethods(){
                foreach(var type in new[]{typeof(Player),typeof(Humanoid),typeof(InventoryGui),typeof(StoreGui),typeof(Fireplace),typeof(Smelter),typeof(CookingStation),typeof(Fermenter),typeof(ItemStand),typeof(ArmorStand),typeof(OfferingBowl)})
                    foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                        if(new[]{"DropItem","ConsumeItem","UseItem","DoCrafting","TryPlacePiece","BuySelectedItem","SellSelectedItem","Interact","OnAddFuel","OnAddOre","OnAddItem"}.Contains(method.Name)&&!(type==typeof(Player)&&method.Name=="Interact"))yield return method;
            }
            [HarmonyPriority(Priority.First)]
            static bool Prefix(object __instance,object[] __args){
                if(Applying)return true;
                if(__instance is Humanoid user&&Held(user.GetInventory()))return false;
                if(__args.OfType<Humanoid>().Any(h=>Held(h.GetInventory()))||__args.OfType<Inventory>().Any(Held))return false;
                return !(__instance is InventoryGui||__instance is StoreGui)||!Player.m_localPlayer||!Held(Player.m_localPlayer.GetInventory());
            }
        }
        [HarmonyPatch]
        internal static class ContainerRequests {
            static IEnumerable<MethodBase> TargetMethods()=>typeof(Container).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name=="RPC_RequestOpen"||m.Name=="RPC_RequestTakeAll");
            [HarmonyPriority(Priority.First)]
            static bool Prefix(Container __instance)=>Applying||!ForeignHold(__instance.m_nview);
        }
        [HarmonyPatch]
        internal static class StorageWrites {
            static IEnumerable<MethodBase> TargetMethods()=>typeof(Burrow.Stow.Sorting).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name.StartsWith("Sort",StringComparison.Ordinal)).Cast<MethodBase>().Concat(typeof(Burrow.Shared.ChestWriter).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>new[]{"Take","TakeTo","Put","Move","TakeAll","StackAll"}.Contains(m.Name)));
            [HarmonyPriority(Priority.First)]
            static bool Prefix(object[] __args){
                if(Applying)return true;
                bool held=__args.OfType<Inventory>().Any(Held)||__args.OfType<Player>().Any(p=>Held(p.GetInventory()))||__args.OfType<Container>().Any(c=>Held(c.GetInventory())||ForeignHold(c.m_nview));
                if(held)foreach(var callback in __args.OfType<Action<bool>>())callback(false);
                return !held;
            }
        }
        [HarmonyPatch(typeof(Burrow.Core.ContainerScan),nameof(Burrow.Core.ContainerScan.IsUsable))]
        internal static class Discovery {
            static bool Prefix(Container container)=>Applying||container==null||!Held(container.GetInventory())&&!ForeignHold(container.m_nview);
        }
        [HarmonyPatch(typeof(Burrow.Reach.StationFeed),nameof(Burrow.Reach.StationFeed.Wanted))]
        internal static class Feeding {
            static bool Prefix(Humanoid user)=>Applying||user==null||!Held(user.GetInventory());
        }
    }
}
