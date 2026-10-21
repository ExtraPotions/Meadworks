using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Contracts;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Newtonsoft.Json;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 internal sealed class PlacementBackend:IDurablePlacementBackend {
  readonly Player actor;readonly Piece piece;readonly Vector3 position;readonly Quaternion rotation;readonly long world;readonly string session;readonly bool durable,recovery;
  readonly string recoveryData;
  readonly PlacementController? originatingController=Plugin.Instance?.Planting;
  readonly string? harvestedSource;
  sealed class Snapshot {public string Prefab="";public float X,Y,Z,QX,QY,QZ,QW;public string? HarvestedSource;}
  public string RecoveryData=>recoveryData;
  public PlacementBackend(Player actor,Piece piece,Vector3 position,Quaternion rotation,bool durable=false,string session="",bool recovery=false,string? savedData=null,string? harvestedSource=null){this.harvestedSource=harvestedSource;this.actor=actor;this.piece=piece;this.position=position;this.rotation=rotation;this.durable=durable;this.session=session;this.recovery=recovery;world=NativeWorld.CurrentUid();recoveryData=savedData??JsonConvert.SerializeObject(new Snapshot{Prefab=piece.name.Replace("(Clone)",""),X=position.x,Y=position.y,Z=position.z,QX=rotation.x,QY=rotation.y,QZ=rotation.z,QW=rotation.w,HarvestedSource=harvestedSource});}
  internal static PlacementBackend Restore(Player actor,string data,string session){var snapshot=JsonConvert.DeserializeObject<Snapshot>(data)??throw new InvalidOperationException("Missing native placement data.");if(string.IsNullOrEmpty(snapshot.Prefab)||new[]{snapshot.X,snapshot.Y,snapshot.Z,snapshot.QX,snapshot.QY,snapshot.QZ,snapshot.QW}.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))throw new InvalidOperationException("Invalid native placement data.");var prefab=ZNetScene.instance.GetPrefab(snapshot.Prefab);var piece=prefab?prefab.GetComponent<Piece>():null;if(!piece)throw new InvalidOperationException("Original placement prefab unavailable.");return new PlacementBackend(actor,piece,new Vector3(snapshot.X,snapshot.Y,snapshot.Z),new Quaternion(snapshot.QX,snapshot.QY,snapshot.QZ,snapshot.QW),true,session,true,data,snapshot.HarvestedSource);}
  internal static Dictionary<ZDO,ZNetView> Instances=>(Dictionary<ZDO,ZNetView>)AccessTools.Field(typeof(ZNetScene),"m_instances").GetValue(ZNetScene.instance);
  bool Available=>actor&&actor==Player.m_localPlayer&&!actor.IsDead()&&!actor.IsTeleporting()&&NativeWorld.IsCurrent(world)&&ZNetScene.instance&&ZNetScene.instance.IsAreaReady(position);
  bool Permitted=>Available&&PrivateArea.CheckAccess(position,0,false,true)&&!Location.IsInsideNoBuildLocation(position);
  bool Matches(ZNetView view,string request){if(!view||!view.IsValid())return false;var zdo=view.GetZDO();return zdo.GetString("wildroot.request")==request&&zdo.GetString("wildroot.actor")==actor.GetPlayerID().ToString(CultureInfo.InvariantCulture)&&zdo.GetString("wildroot.world")==world.ToString(CultureInfo.InvariantCulture)&&zdo.GetString("wildroot.session")==session&&zdo.GetString("wildroot.source")==piece.name.Replace("(Clone)","");}
  ZNetView? Receipt(string request){var matches=Instances.Values.Where(v=>v&&PrefabName(v.gameObject)==NativePlacementReceipts.Prefab&&Matches(v,request)&&Vector3.Distance(v.transform.position,position)<0.05f).ToArray();if(matches.Length>1)throw new InvalidOperationException("Conflicting placement receipts.");return matches.SingleOrDefault();}
  public PlacementProbe Probe(PlacementCellRecord cell){
   if(!Available)return new PlacementProbe(PlacementPresence.Unavailable);
   var tagged=Instances.Values.Where(v=>v&&v.IsValid()&&v.GetZDO().GetString("wildroot.request")==cell.RequestId&&v.GetZDO().GetString("wildroot.actor")==actor.GetPlayerID().ToString(CultureInfo.InvariantCulture)&&PrefabName(v.gameObject)!=NativePlacementReceipts.Prefab).ToArray();
   if(tagged.Any(v=>!Matches(v,cell.RequestId)||PrefabName(v.gameObject)!=piece.name.Replace("(Clone)","")||Vector3.Distance(v.transform.position,position)>0.05f||!string.IsNullOrEmpty(cell.NativeId)&&v.GetZDO().GetString(NativePlacementReceipts.Instance)!=cell.NativeId))return new PlacementProbe(PlacementPresence.Ambiguous);
   var receipt=Receipt(cell.RequestId);var zdo=receipt?receipt.GetZDO():null;
   var presence=PlacementReceiptPolicy.Evaluate(true,Permitted,tagged.Length,tagged.All(v=>v.IsOwner()),receipt!=null,zdo?.GetString("wildroot.lifecycle")??"",zdo?.GetString("wildroot.native")??"",cell.NativeId,cell.Retired);
   if(presence==PlacementPresence.ProvenMissing&&recovery){var tool=PlacementController.RightItem(actor);if(harvestedSource==null&&tool?.m_dropPrefab?.name!="Cultivator"||Plugin.Instance?.Planting?.RecoveryPrefabAllowed(piece.name.Replace("(Clone)",""))!=true)return new PlacementProbe(PlacementPresence.Unavailable);}
   return new PlacementProbe(presence,presence==PlacementPresence.Present?new Handle(this,tagged[0],cell.RequestId):null);
  }
  ZNetView EnsureReceipt(PlacementOperation operation){
   var existing=Receipt(operation.Context.RequestId);if(existing){if(!existing.IsOwner())throw new InvalidOperationException("Placement receipt ownership unavailable.");return existing;}
   if(originatingController==null||originatingController.IsRetiring||!ReferenceEquals(Plugin.Instance?.Planting,originatingController))throw new InvalidOperationException("Placement receipt creation unavailable during shutdown.");
   var prefab=PrefabManager.Instance.GetPrefab(NativePlacementReceipts.Prefab);if(!prefab)throw new InvalidOperationException("Placement receipt prefab unavailable.");
   using(var scope=new NativePlacementReceipts.Scope(NativePlacementReceipts.Prefab,piece.name.Replace("(Clone)",""),position,operation.Context,session)){
    var obj=UnityEngine.Object.Instantiate(prefab,position,Quaternion.identity);obj.SetActive(true);var view=obj.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner()||scope.Created.Count!=1)throw new InvalidOperationException("Placement receipt creation failed.");return view;
   }
  }
  public IPlacementHandle Create(PlacementOperation operation){
   if(!Permitted||operation.Context.ActorId!=actor.GetPlayerID().ToString(CultureInfo.InvariantCulture)||operation.Context.WorldId!=world.ToString(CultureInfo.InvariantCulture))throw new InvalidOperationException("Original placement context unavailable.");
   if(harvestedSource!=null&&!NativePlanting.ReplantClear(position,harvestedSource))throw new InvalidOperationException("Replant position occupied.");
   if(recovery){var tool=PlacementController.RightItem(actor);if(harvestedSource==null&&tool?.m_dropPrefab?.name!="Cultivator"||Plugin.Instance?.Planting?.RecoveryPrefabAllowed(piece.name.Replace("(Clone)",""))!=true)throw new InvalidOperationException("Original placement tool/prefab unavailable.");}
   var receipt=durable?EnsureReceipt(operation):null;
   using(var scope=new NativePlacementReceipts.Scope(piece.name.Replace("(Clone)",""),piece.name.Replace("(Clone)",""),position,operation.Context,session)){
    try{
     AccessTools.Method(typeof(Player),"PlacePiece").Invoke(actor,new object[]{piece,position,rotation,false,false});
     if(scope.Created.Count!=1)throw new InvalidOperationException("Placement did not create exactly one correlated network object.");
     var created=scope.Created[0];if(!created||!created.IsValid()||!created.IsOwner())throw new InvalidOperationException("Created placement owner unavailable.");
     Plugin.Instance?.Catalog?.AfterCreated(created.gameObject);
     if(receipt){receipt.GetZDO().Set("wildroot.native",created.GetZDO().GetString(NativePlacementReceipts.Instance));receipt.GetZDO().Set("wildroot.lifecycle","created");}
     return new Handle(this,created,operation.Context.RequestId);
    }catch{
     using(NativePlacementReceipts.SuppressRemoval())foreach(var created in scope.Created.ToArray())if(created&&created.IsValid()&&created.IsOwner())ZNetScene.instance.Destroy(created.gameObject);
     if(receipt&&receipt.IsOwner())receipt.GetZDO().Set("wildroot.lifecycle","aborted");throw;
    }
   }
  }
  internal static string PrefabName(GameObject obj){var name=obj.name;return name.EndsWith("(Clone)",StringComparison.Ordinal)?name.Substring(0,name.Length-7):name;}
  sealed class Handle:IDurablePlacementHandle {
   readonly PlacementBackend backend;readonly ZNetView instance;readonly string request;bool removed;
   public string NativeId{get;}
   public Handle(PlacementBackend backend,ZNetView instance,string request){this.backend=backend;this.instance=instance;this.request=request;NativeId=instance.GetZDO().GetString(NativePlacementReceipts.Instance);}
   public void Confirm(){
    var receipt=backend.durable?backend.Receipt(request):null;if(receipt&&receipt.GetZDO().GetString("wildroot.lifecycle")=="removed")return;
    if(!backend.Permitted||!instance||!instance.IsValid()||!instance.IsOwner()||!backend.Matches(instance,request))throw new InvalidOperationException("Placement receipt owner/context unavailable.");
    if(backend.durable){if(!receipt)receipt=backend.EnsureReceipt(new PlacementOperation(new ResourceContext(backend.actor.GetPlayerID().ToString(CultureInfo.InvariantCulture),backend.world.ToString(CultureInfo.InvariantCulture),request),Array.Empty<ResourceCost>(),()=>false));if(!receipt.IsOwner())throw new InvalidOperationException("Placement world receipt owner unavailable.");receipt.GetZDO().Set("wildroot.native",NativeId);receipt.GetZDO().Set("wildroot.lifecycle","committed");}
    instance.GetZDO().Set("wildroot.payment","committed");
    Plugin.Instance?.Catalog?.AfterCommitted(instance.gameObject);
    // Farming experience for the planting (#21); a recovered placement was planted in an earlier session.
    if(!backend.recovery)try{FarmingExperience.Planted(backend.actor,backend.piece);}catch(Exception error){Debug.LogError("Wildroot planting experience failed: "+error.GetType().Name+": "+error.Message);}
   }
   public void Compensate(){if(removed)return;if(!backend.Permitted)throw new InvalidOperationException("Placement compensation area/context unavailable.");if(instance){if(!instance.IsValid()||!instance.IsOwner()||!backend.Matches(instance,request))throw new InvalidOperationException("Placement ownership changed.");using(NativePlacementReceipts.SuppressRemoval())ZNetScene.instance.Destroy(instance.gameObject);}var receipt=backend.durable?backend.Receipt(request):null;if(receipt){if(!receipt.IsOwner())throw new InvalidOperationException("Placement receipt ownership changed.");receipt.GetZDO().Set("wildroot.lifecycle","aborted");}removed=true;}
  }
 }
 internal static class NativePlacementReceipts {
  internal const string Prefab="Wildroot_PlacementReceipt";
  // Persisted per-object placement identity. A ZDOID is session-scoped: Valheim reassigns it when a world
  // is loaded, so it can never correlate a journal cell or receipt with its object after a reload.
  internal const string Instance="wildroot.instance";static Scope? current;static int suppressed;
  internal const string PendingSafetyOwner=Plugin.Guid+".pending-safety";
  static Harmony? pendingSafety;
  // Inspect raw native identities as well as loaded instances: an unloaded pending crop still needs protection when it returns.
  static bool PendingNativeObjects(){
   if(ZDOMan.instance==null)return false;
   var objects=(Dictionary<ZDOID,ZDO>)AccessTools.Field(typeof(ZDOMan),"m_objectsByID").GetValue(ZDOMan.instance);
   return objects.Values.Any(zdo=>zdo.GetPrefab()!=Prefab.GetStableHashCode()&&!string.IsNullOrEmpty(zdo.GetString("wildroot.request"))&&!PlacementReceiptPolicy.CanUse(zdo.GetString("wildroot.payment"),zdo.GetString("wildroot.session")));
  }
  internal static object? PreservePendingSafety(bool canInspectNative){
   if(pendingSafety!=null||canInspectNative&&ZDOMan.instance!=null&&!PendingNativeObjects())return null;
   var safety=new Harmony(PendingSafetyOwner);
   // HarmonyX UnpatchSelf selects by owner, then removes by patch MethodInfo. Sharing the
   // gameplay prefix would remove the safety owner's copy too; these delegates are distinct.
   safety.Patch(AccessTools.Method(typeof(Plant),"Grow"),prefix:new HarmonyMethod(AccessTools.Method(typeof(NativePlacementReceipts),nameof(PendingSafetyGrowth))));
   safety.Patch(AccessTools.Method(typeof(Pickable),"RPC_Pick"),prefix:new HarmonyMethod(AccessTools.Method(typeof(NativePlacementReceipts),nameof(PendingSafetyYield))));
   pendingSafety=safety;
   return safety;
  }
  // Called only on the captured Unity owner context after settlement, or after ordinary startup patches are back in place.
  internal static void ReleasePendingSafetyIfClear(object? ownership){if(ownership!=null&&ReferenceEquals(pendingSafety,ownership)&&ZDOMan.instance!=null&&!PendingNativeObjects())ReleasePendingSafety();}
  internal static void ReleasePendingSafety(){pendingSafety?.UnpatchSelf();pendingSafety=null;}
  internal static void Register(){if(PrefabManager.Instance.GetPrefab(Prefab))return;var prefab=new GameObject(Prefab);prefab.SetActive(false);var view=prefab.AddComponent<ZNetView>();view.m_persistent=true;PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab,false));prefab.SetActive(true);}
  internal sealed class Scope:IDisposable {
   readonly Scope? previous;readonly string expected,source,session;readonly Vector3 position;readonly ResourceContext context;
   internal readonly List<ZNetView> Created=new List<ZNetView>();
   internal Scope(string expected,string source,Vector3 position,ResourceContext context,string session){previous=current;this.expected=expected;this.source=source;this.position=position;this.context=context;this.session=session;current=this;}
   internal void Tag(ZNetView view){if(!view||!view.IsValid()||!view.IsOwner()||PlacementBackend.PrefabName(view.gameObject)!=expected||Vector3.Distance(view.transform.position,position)>0.05f)return;var zdo=view.GetZDO();if(!string.IsNullOrEmpty(zdo.GetString("wildroot.request")))return;zdo.Set("wildroot.source",source);zdo.Set("wildroot.request",context.RequestId);zdo.Set("wildroot.actor",context.ActorId);zdo.Set("wildroot.world",context.WorldId);zdo.Set("wildroot.session",session);zdo.Set("wildroot.payment","pending");if(expected==Prefab)zdo.Set("wildroot.lifecycle","intent");else zdo.Set(Instance,Guid.NewGuid().ToString("N"));Created.Add(view);}
   public void Dispose(){current=previous;}
  }
  sealed class Suppression:IDisposable {internal Suppression(){suppressed++;}public void Dispose(){suppressed--;}}
  internal static IDisposable SuppressRemoval()=>new Suppression();
  static bool CanUse(Component source){var view=source.GetComponent<ZNetView>();if(!view||!view.IsValid())return true;var zdo=view.GetZDO();return PlacementReceiptPolicy.CanUse(zdo.GetString("wildroot.payment"),zdo.GetString("wildroot.session"));}
  static bool PendingSafetyGrowth(Plant __instance)=>CanUse(__instance);
  static bool PendingSafetyYield(Pickable __instance)=>CanUse(__instance);
  [HarmonyPatch(typeof(Plant),"Grow")]
  sealed class PendingGrowthPatch {static bool Prefix(Plant __instance)=>CanUse(__instance);}
  [HarmonyPatch(typeof(Pickable),"RPC_Pick")]
  sealed class PendingYieldPatch {static bool Prefix(Pickable __instance)=>CanUse(__instance);}
  [HarmonyPatch(typeof(ZNetView),"Awake")]
  sealed class CreationPatch {static void Postfix(ZNetView __instance)=>current?.Tag(__instance);}
  [HarmonyPatch(typeof(ZNetScene),"Destroy")]
  sealed class RemovalPatch {
   static void Prefix(GameObject go){
    if(suppressed>0||!go||PlacementBackend.PrefabName(go)==Prefab)return;var view=go.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner())return;var zdo=view.GetZDO();string request=zdo.GetString("wildroot.request");if(string.IsNullOrEmpty(request))return;
    foreach(var receipt in PlacementBackend.Instances.Values.Where(v=>v&&v.IsValid()&&v.IsOwner()&&PlacementBackend.PrefabName(v.gameObject)==Prefab&&v.GetZDO().GetString("wildroot.request")==request&&v.GetZDO().GetString("wildroot.actor")==zdo.GetString("wildroot.actor")&&v.GetZDO().GetString("wildroot.world")==zdo.GetString("wildroot.world")&&v.GetZDO().GetString("wildroot.session")==zdo.GetString("wildroot.session")&&v.GetZDO().GetString("wildroot.native")==zdo.GetString(Instance)).ToArray())receipt.GetZDO().Set("wildroot.lifecycle","removed");
    if(long.TryParse(zdo.GetString("wildroot.world"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var world))Plugin.Instance?.Planting?.RecordRemoval(zdo.GetString("wildroot.actor"),world,zdo.GetString("wildroot.session"),request,zdo.GetString(Instance));
   }
  }
 }
}
