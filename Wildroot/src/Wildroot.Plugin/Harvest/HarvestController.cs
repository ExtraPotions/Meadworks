using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Contracts;
using HarmonyLib;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 public sealed class HarvestBatchHandle {readonly List<string> results=new List<string>();internal readonly PlacementTransaction ReplantTransactions=new PlacementTransaction();internal readonly string Id=System.Guid.NewGuid().ToString("N");public int Pending{get;internal set;}public int Harvested{get;internal set;}public int Cancelled{get;internal set;}public int Collected{get;internal set;}public int Replanted{get;internal set;}public IReadOnlyList<string> Results=>results.AsReadOnly();internal void Record(string result)=>results.Add(result);}
 public sealed class HarvestController:IDisposable {
  readonly WildrootConfig settings;readonly ManualLogSource log;readonly PrefabRegistrar catalog;readonly PlacementController planting;readonly Queue<Job> queue=new Queue<Job>();readonly List<Job> awaiting=new List<Job>();readonly HashSet<string> reserved=new HashSet<string>(StringComparer.Ordinal);readonly Dictionary<string,Job> jobs=new Dictionary<string,Job>(StringComparer.Ordinal);Player? actor;long world;
  public bool Executing{get;private set;}
  public HarvestBatchHandle? LastSubmission{get;private set;}
  readonly NativeHarvestService native;
  internal HarvestController(WildrootConfig settings,ManualLogSource log,PrefabRegistrar catalog,PlacementController planting,ProductRegistration registration){this.settings=settings;this.log=log;this.catalog=catalog;this.planting=planting;native=new NativeHarvestService(settings,registration);}
  static bool Permission(Vector3 position)=>ZNetScene.instance&&ZNetScene.instance.IsAreaReady(position)&&PrivateArea.CheckAccess(position,0,false,true);
  static bool Ready(Component target){if(!target)return false;if(target is Pickable p)return (bool)NativeFields.CanBePicked.Invoke(p,Array.Empty<object>());if(target is Beehive hive)return Honey(hive)>0;return false;}
  static int Honey(Beehive hive)=>(int)NativeFields.GetHoneyLevel.Invoke(hive,Array.Empty<object>());
  static string Resource(Component target){if(target is Pickable p){var item=(GameObject?)AccessTools.Field(typeof(Pickable),"m_itemPrefab").GetValue(p);return item?item.name:"";}if(target is Beehive hive){var item=(ItemDrop?)AccessTools.Field(typeof(Beehive),"m_honeyItem").GetValue(hive);return item?item.name:"";}return "";}
  bool ActorReady()=>settings.Active&&actor&&actor==Player.m_localPlayer&&!actor.IsDead()&&!actor.IsTeleporting()&&NativeWorld.IsCurrent(world);
  bool Active()=>ActorReady()&&settings.Get("Harvest.Enabled",true);
  /// <summary>A job may continue: bulk jobs need Harvest.Enabled; a Farming auto-replant job (#108) only needs Wildroot.</summary>
  bool Allowed(Job job)=>job.AutoReplant?ActorReady():Active();
  bool OnlyAutoReplants(){if(queue.Count>0)return false;foreach(var job in awaiting)if(!job.AutoReplant)return false;return true;}
  /// <summary>Handle of the last single-pick auto replant, for diagnostics and tests.</summary>
  public HarvestBatchHandle? LastAutoReplant{get;private set;}
  /// <summary>
  /// Farming auto replant after a single pick (#108): a harvest job already confirmed by the game's own pick, with no
  /// drops to collect, that replants through the same paid placement path as harvest replant.
  /// </summary>
  /// <remarks>The owner's pick has already destroyed the source's network view, so its id is taken before the pick.</remarks>
  internal bool SubmitAutoReplant(Component source,ZDOID uid){
   var player=Player.m_localPlayer;if(!player||(object)source==null||uid==ZDOID.None||!settings.Active||!ZNet.instance)return false;
   string id=uid.ToString();if(reserved.Contains(id)||jobs.ContainsKey(id))return false;
   long currentWorld=NativeWorld.CurrentUid();if(currentWorld==0)return false;if(actor!=player||world!=currentWorld)CancelAll();actor=player;world=currentWorld;
   string sourceName=source.name.Replace("(Clone)","");var original=catalog.Definitions.Values.FirstOrDefault(p=>p.Kind==PlantKind.Crop&&p.GrownPrefab==sourceName);if(original==null)return false;
   var handle=new HarvestBatchHandle{Pending=1};
   var job=new Job(0f,source.transform.position){Id=id,Target=source,Batch=handle,QueuedAt=Time.unscaledTime,Position=source.transform.position,Rotation=source.transform.rotation,Original=original.Id,Selected=Plugin.Instance?.Controls?.LastSelectedPlant??"",Resource=Resource(source),Confirmed=true,ConfirmedAt=Time.unscaledTime,AutoReplant=true};
   reserved.Add(id);jobs.Add(id,job);awaiting.Add(job);LastAutoReplant=handle;return true;
  }
  public HarvestBatchHandle? Submit(Component target){
   LastSubmission=null;
   var player=Player.m_localPlayer;if(!player||!target||!settings.Active||!settings.Get("Harvest.Enabled",true)||!ZNet.instance||!Ready(target)||!Permission(target.transform.position))return null;
   long currentWorld=NativeWorld.CurrentUid();if(currentWorld==0)return null;if(actor!=player||world!=currentWorld)CancelAll();actor=player;world=currentWorld;
   float radius=settings.Get("Harvest.Radius",3f);if(float.IsNaN(radius)||float.IsInfinity(radius)||radius<0)return null;
   var objects=new Dictionary<string,Component>(StringComparer.Ordinal);var snapshots=new List<HarvestableSnapshot>();
   foreach(var obj in UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None).Cast<Component>().Concat(UnityEngine.Object.FindObjectsByType<Beehive>(FindObjectsSortMode.None))){var view=obj.GetComponent<ZNetView>();if(!view||!view.IsValid())continue;string id=view.GetZDO().m_uid.ToString();if(reserved.Contains(id)||objects.ContainsKey(id))continue;objects.Add(id,obj);var pos=obj.transform.position;snapshots.Add(new HarvestableSnapshot(id,Resource(obj),new Point3(pos.x,pos.y,pos.z),Ready(obj),Permission(pos),obj is Beehive));}
   var origin=target.transform.position;var selection=settings.Get("Harvest.HarvestStyle","AllResources")=="LikeResources"?HarvestSelection.LikeResources:HarvestSelection.AllResources;
   var farming=FarmingState.Rules;float farmLevel=FarmingState.Level(player);bool autoReplant=FarmingPerks.AutoReplants(farming,farmLevel);
   var plan=HarvestPlanner.Plan(new HarvestRequest(new Point3(origin.x,origin.y,origin.z),radius,Resource(target),selection),snapshots);var handle=new HarvestBatchHandle{Pending=plan.ObjectIds.Count};foreach(var id in plan.ObjectIds){reserved.Add(id);var source=objects[id];string sourceName=source.name.Replace("(Clone)","");var original=catalog.Definitions.Values.FirstOrDefault(p=>p.Kind==PlantKind.Crop&&p.GrownPrefab==sourceName);var job=new Job(radius,origin){Id=id,Target=source,Batch=handle,QueuedAt=Time.unscaledTime,Position=source.transform.position,Rotation=source.transform.rotation,Original=original?.Id??"",Selected=Plugin.Instance?.Controls?.LastSelectedPlant??"",Resource=Resource(source),AutoReplant=original!=null&&autoReplant,Crop=source is Pickable?CropIndex.OfPickable(sourceName):null};jobs.Add(id,job);queue.Enqueue(job);}LastSubmission=handle;return handle;
  }
  internal void Update(){
   if(!Active()&&!(ActorReady()&&OnlyAutoReplants())){CancelAll();return;}
   int count=2;while(count-->0&&queue.Count>0){var job=queue.Dequeue();try{
    if(!job.Target){Complete(job,false,"TargetLost");continue;}if(!Ready(job.Target)){Complete(job,false,"NotRipe");continue;}if(!ZNetScene.instance||!ZNetScene.instance.IsAreaReady(job.Target.transform.position)){if(Time.unscaledTime-job.QueuedAt<15){queue.Enqueue(job);break;}Complete(job,false,"AreaNotReady");continue;}if(!PrivateArea.CheckAccess(job.Target.transform.position,0,false,true)){Complete(job,false,"WardDenied");continue;}
    // Interact preserves native skill/stat/bonus behavior; its captured dispatch travels through authenticated ownership.
    Executing=true;job.Request=native.Prepare(job.Target,actor!,job.RequesterRadius,job.PrimaryOrigin);job.Harvest=CruciblePlugin.RequestClient.InvokeAsync<NativeHarvestRequest,NativeHarvestReceipt>(NativeHarvestWire.Operation,job.Request,job.Cancellation.Token);
    job.Started=Time.unscaledTime;awaiting.Add(job);
   }catch(Exception error){log.LogWarning("Harvest request rejected: "+error.GetBaseException().GetType().Name);Complete(job,false,error.GetBaseException().GetType().Name);}finally{Executing=false;}}
   for(int i=awaiting.Count-1;i>=0;i--){var job=awaiting[i];
    if(!job.Confirmed&&job.Harvest?.IsCompleted==true){
     if(job.Harvest.IsFaulted||job.Harvest.IsCanceled){awaiting.RemoveAt(i);Complete(job,false,"HarvestOutcomeUnknown");continue;}
     var result=job.Harvest.Result;var receipt=result.Reply;
     if(result.State!=OperationState.Succeeded||receipt==null||job.Request==null||!receipt.Matches(job.Request)){awaiting.RemoveAt(i);Complete(job,false,result.State==OperationState.RecoveryRequired?"HarvestOutcomeUnknown":result.Code);continue;}
     if(!receipt.Harvested){awaiting.RemoveAt(i);Complete(job,false,receipt.Reason);continue;}
     job.Confirmed=true;job.ConfirmedAt=Time.unscaledTime;job.Drops.AddRange(receipt.Drops);
     // Seed return (#12) for a confirmed bulk pick: the requester makes the seed and collects it with the harvest's drops.
     if(job.Crop!=null){var seed=FarmingSeeds.Roll(FarmingState.Rules,FarmingState.Level(actor),job.Crop,job.Position);var seedView=seed?seed!.GetComponent<ZNetView>():null;if(seedView&&seedView!.IsValid())job.Drops.Add(new NativeHarvestDrop{Id=seedView.GetZDO().m_uid,Amount=seed!.m_itemData.m_stack});}
    }
    if(job.Confirmed){if(Time.unscaledTime-job.ConfirmedAt<0.5f)continue;for(int d=job.Drops.Count-1;d>=0;d--){var expected=job.Drops[d];var obj=ZNetScene.instance?ZNetScene.instance.FindInstance(expected.Id):null;var drop=obj?obj.GetComponent<ItemDrop>():null;
     // Missing replicas or externally consumed drops are unresolved, never evidence of this actor collecting them.
     if(!drop)continue;drop.Load();var view=drop.GetComponent<ZNetView>();if(!view||!view.IsValid()||drop.m_itemData.m_stack!=expected.Amount||!PrivateArea.CheckAccess(drop.transform.position,0,false,true))continue;
     if(!view.IsOwner()){drop.RequestOwn();continue;}
     int collected=NativeHarvestService.Collect(actor!,drop,out int remaining);job.Batch.Collected+=collected;expected.Amount=remaining;if(remaining==0)job.Drops.RemoveAt(d);}
     if(job.Drops.Count>0&&Time.unscaledTime-job.ConfirmedAt<15)continue;
     bool replant=settings.Get("Harvest.Replant",false)||job.AutoReplant;
     // Cancellation is a decision, not a transient setting: toggling on again must not revive an older hold.
     if(!replant)job.ReplantAsync?.Cancel();
     if((replant||job.ReplantAsync!=null)&&!TryReplant(job))continue;
     awaiting.RemoveAt(i);Complete(job,true,job.Drops.Count==0?"HarvestedAndCollected":"HarvestedWithDropsRemaining");}
    else if(Time.unscaledTime-job.Started>15){job.Cancellation.Cancel();awaiting.RemoveAt(i);Complete(job,false,"AcknowledgementTimeout");}
   }
  }
  bool ReplantInFlight(Job job){foreach(var other in awaiting)if(other!=job&&other.ReplantAsync!=null&&other.ReplantAsync.Result==null&&!other.ReplantAsync.NeedsRecovery)return true;return false;}
  internal bool HasSource(Component source){var view=source?source.GetComponent<ZNetView>():null;return view&&view.IsValid()&&jobs.ContainsKey(view.GetZDO().m_uid.ToString());}
  /// <summary>Locally loaded plants protect against an already replicated replacement.</summary>
  bool TryReplant(Job job){
   if(job.ReplantAsync!=null){
    var result=job.ReplantAsync.Result;
    if(result==null){if(!job.ReplantAsync.NeedsRecovery)return false;job.ReplantAsync.Cancel();job.Batch.Record(job.Id+":ReplantRecoveryRequired");log.LogWarning("Shared replant settling in the background: "+job.Id);return true;}
    job.Batch.Record(job.Id+":Replant"+result.Kind);if(result.Kind==PlacementResultKind.Committed)job.Batch.Replanted++;return true;
   }
   if(job.ReplantAttempted||string.IsNullOrEmpty(job.Original))return true;
   if(!ZNetScene.instance||!ZNetScene.instance.IsAreaReady(job.Position)){if(Time.unscaledTime-job.ConfirmedAt<15)return false;job.ReplantAttempted=true;job.Batch.Record(job.Id+":ReplantAreaNotReady");return true;}
   // One shared replant payment at a time: concurrent reservations on the same carried inventory contend (the provider holds it until
   // the first settles) and the later one could time out as insufficient while seeds exist. The rest wait their turn, unpaid.
   if(ReplantInFlight(job)&&ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities.TryResolve<IAsyncResourceProvider>("meadworks.resources.async",new System.Version(2,0),out _))return false;
   job.ReplantAttempted=true;if(!Allowed(job)||!Permission(job.Position)||Location.IsInsideNoBuildLocation(job.Position))return true;
   if(!NativePlanting.ReplantClear(job.Position,job.Id)){job.Batch.Record(job.Id+":ReplantOccupied");return true;}
   var context=PlacementController.Context(actor!,job.Batch.Id+":"+job.Id+":replant");var resources=planting.Resources(actor!);
   ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities.TryResolve<IAsyncResourceProvider>("meadworks.resources.async",new System.Version(2,0),out var shared);
   var choice=ReplantPlanner.Choose(new ReplantContext(job.Original,job.Selected,settings.Get("Harvest.ReplantTarget","Original")=="Selected",item=>shared!=null?int.MaxValue:resources.GetAvailable(context,item)),new CatalogSnapshot(catalog.Definitions.Values.ToArray()));
   if(choice.Plant==null){job.Batch.Record(job.Id+":ReplantInsufficientResources");return true;}
   var prefab=ZNetScene.instance.GetPrefab(choice.Plant.Prefab);var piece=prefab?prefab.GetComponent<Piece>():null;if(!piece){job.Batch.Record(job.Id+":ReplantPrefabUnavailable");return true;}
   var operation=new PlacementOperation(context,choice.Plant.Costs,()=>Allowed(job)&&(job.AutoReplant||settings.Get("Harvest.Replant",false)&&settings.Get("Harvest.Enabled",true))&&PrivateArea.CheckAccess(job.Position,0,false,true)&&!Location.IsInsideNoBuildLocation(job.Position));
   if(shared!=null){job.ReplantAsync=planting.BeginReplant(actor!,piece,job.Position,job.Rotation,operation,shared,job.Id);return false;}
   var synchronous=job.Batch.ReplantTransactions.Execute(operation,new PlacementBackend(actor!,piece,job.Position,job.Rotation,harvestedSource:job.Id),resources);job.Batch.Record(job.Id+":Replant"+synchronous.Kind);if(synchronous.Kind==PlacementResultKind.Committed)job.Batch.Replanted++;if(synchronous.Kind==PlacementResultKind.RecoveryRequired)log.LogError("Replant needs recovery: "+job.Id);return true;
  }
  void Complete(Job job,bool harvested,string reason="Canceled"){
   if(job.Request!=null&&NativeWorld.IsCurrent(job.Request.World)){
    foreach(var drop in job.Drops)NativeHarvestService.Release(drop.Id);
    // A completed receipt is known even if disable/cancel ran before Update consumed it.
    if(job.Harvest?.Status==TaskStatus.RanToCompletion){var result=job.Harvest.Result;var receipt=result.Reply;if(result.State==OperationState.Succeeded&&receipt!=null&&receipt.Matches(job.Request))foreach(var drop in receipt.Drops)NativeHarvestService.Release(drop.Id);}
   }
   job.Cancellation.Dispose();reserved.Remove(job.Id);jobs.Remove(job.Id);job.Batch.Record(job.Id+":"+reason);job.Batch.Pending--;if(harvested)job.Batch.Harvested++;else job.Batch.Cancelled++;
  }
  public void CancelPendingReplants(){foreach(var job in awaiting){if(job.AutoReplant)continue;job.ReplantAttempted=true;job.ReplantAsync?.Cancel();}}
  internal void CancelAll(){while(queue.Count>0)Complete(queue.Dequeue(),false);foreach(var job in awaiting){job.Cancellation.Cancel();job.ReplantAsync?.Cancel();Complete(job,false);}awaiting.Clear();reserved.Clear();jobs.Clear();actor=null;world=0;}
  public void Dispose(){CancelAll();native.Dispose();}
  sealed class Job {public readonly float RequesterRadius;public readonly Vector3 PrimaryOrigin;public Job(float requesterRadius,Vector3 primaryOrigin){RequesterRadius=requesterRadius;PrimaryOrigin=primaryOrigin;}public string Id="";public Component Target=null!;public HarvestBatchHandle Batch=null!;public float Started,QueuedAt,ConfirmedAt;public bool Confirmed,ReplantAttempted,AutoReplant;public CropInfo? Crop;public Vector3 Position;public Quaternion Rotation;public string Original="",Selected="",Resource="";public AsyncPlacementJob? ReplantAsync;public NativeHarvestRequest? Request;public Task<OperationResult<NativeHarvestReceipt>>? Harvest;public readonly CancellationTokenSource Cancellation=new CancellationTokenSource();public readonly List<NativeHarvestDrop> Drops=new List<NativeHarvestDrop>();}
 }
 [HarmonyPatch]
 static class HarvestInteractionPatch {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){yield return AccessTools.Method(typeof(Pickable),"Interact");yield return AccessTools.Method(typeof(Beehive),"Interact");}
  static bool Prefix(Component __instance,Humanoid __0,bool __1,bool __2,ref bool __result){
   var plugin=Plugin.Instance;var controller=plugin?.Harvesting;
   if(!plugin||!plugin.GameplayAvailable||controller==null||controller.Executing||__0!=Player.m_localPlayer||!InputRouter.InterceptsInteract(plugin.Controls?.WantsBulkHarvest()==true,__1,__2))return true;
   if(controller.HasSource(__instance)){__result=false;return false;}
   var batch=controller.Submit(__instance);if(batch==null||batch.Pending==0)return true;
   __result=false;return false;
  }
 }
}
