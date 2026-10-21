using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Contracts;
using HarmonyLib;
namespace ExtraPotions.Wildroot.Runtime {
 internal sealed class InventoryResourceProvider:IResourceProvider {
  readonly Player actor;readonly object gate=new object();readonly Dictionary<ItemDrop.ItemData,int> held=new Dictionary<ItemDrop.ItemData,int>();
  public InventoryResourceProvider(Player actor){this.actor=actor;}
  bool Active(ResourceContext context)=>actor&&actor==Player.m_localPlayer&&!actor.IsDead()&&context.ActorId==actor.GetPlayerID().ToString(CultureInfo.InvariantCulture)&&NativeWorld.CurrentUid()!=0&&context.WorldId==NativeWorld.CurrentUid().ToString(CultureInfo.InvariantCulture);
  static string ItemName(string prefab){var obj=ObjectDB.instance?ObjectDB.instance.GetItemPrefab(prefab):null;var drop=obj?obj.GetComponent<ItemDrop>():null;return drop?drop.m_itemData.m_shared.m_name:"";}
  List<ItemDrop.ItemData> Matching(string prefab){var name=ItemName(prefab);return string.IsNullOrEmpty(name)?new List<ItemDrop.ItemData>():actor.GetInventory().GetAllItems().Where(i=>i.m_shared.m_name==name&&i.m_worldLevel>=Game.m_worldLevel).ToList();}
  public int GetAvailable(ResourceContext context,string itemId){lock(gate){if(!Active(context))return 0;return Matching(itemId).Sum(i=>Math.Max(0,i.m_stack-(held.TryGetValue(i,out var amount)?amount:0)));}}
  public bool TryReserve(ResourceContext context,IReadOnlyList<ResourceCost> costs,out IResourceReservation? reservation){
   lock(gate){reservation=null;if(!Active(context)||costs==null)return false;var allocation=new Dictionary<ItemDrop.ItemData,int>();
    foreach(var cost in costs){if(cost==null)return false;int left=cost.Amount;foreach(var item in Matching(cost.ItemId)){var occupied=held.TryGetValue(item,out var h)?h:0;var local=allocation.TryGetValue(item,out var a)?a:0;var take=Math.Min(left,Math.Max(0,item.m_stack-occupied-local));if(take>0){allocation[item]=local+take;left-=take;}if(left==0)break;}if(left!=0)return false;}
    foreach(var pair in allocation)held[pair.Key]=(held.TryGetValue(pair.Key,out var old)?old:0)+pair.Value;
    reservation=new Reservation(this,context,allocation);return true;
   }
  }
  sealed class Reservation:IResourceReservation {
   readonly InventoryResourceProvider owner;readonly ResourceContext context;readonly Dictionary<ItemDrop.ItemData,int> allocation;public ReservationState State{get;private set;}
   public Reservation(InventoryResourceProvider owner,ResourceContext context,Dictionary<ItemDrop.ItemData,int> allocation){this.owner=owner;this.context=context;this.allocation=allocation;}
   public void Commit(){lock(owner.gate){if(State==ReservationState.Committed)return;if(State==ReservationState.RolledBack)throw new InvalidOperationException("Reservation already rolled back");if(!owner.Active(context))throw new InvalidOperationException("Actor/world changed");var inventory=owner.actor.GetInventory();var items=inventory.GetAllItems();foreach(var pair in allocation)if(!items.Contains(pair.Key)||pair.Key.m_stack<pair.Value)throw new InvalidOperationException("Reserved inventory changed");
    // Validate everything before any mutation. Notify once after the atomic state
    // change; synchronous inventory callbacks cannot cause a partial deduction.
    foreach(var pair in allocation){pair.Key.m_stack-=pair.Value;if(pair.Key.m_stack==0)items.Remove(pair.Key);}
    Release();State=ReservationState.Committed;
    if(allocation.Count>0)AccessTools.Method(typeof(Inventory),"Changed",new[]{typeof(bool),typeof(bool)}).Invoke(inventory,new object[]{true,false});
   }}
   void Release(){foreach(var pair in allocation){var remaining=owner.held[pair.Key]-pair.Value;if(remaining==0)owner.held.Remove(pair.Key);else owner.held[pair.Key]=remaining;}}
   public void Rollback(){lock(owner.gate){if(State!=ReservationState.Reserved)return;Release();State=ReservationState.RolledBack;}}
   public void Dispose()=>Rollback();
  }
 }
}
