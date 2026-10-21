using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Contracts;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Wildroot.Runtime {
 public sealed class PlacementBatchHandle {
  readonly List<PlacementResult> results=new List<PlacementResult>();internal readonly PlacementTransaction Transactions=new PlacementTransaction();internal bool Cancelled;internal int BackgroundPending;
  public string Id{get;}public int Pending{get;internal set;}public IReadOnlyList<PlacementResult> Results=>results.AsReadOnly();
  internal PlacementBatchHandle(string id,int count){Id=id;Pending=count;}internal void Complete(PlacementResult result){results.Add(result);Pending--;}public void Cancel()=>Cancelled=true;
 }
 public sealed class PlacementController {
  readonly WildrootConfig settings;readonly PrefabRegistrar catalog;readonly ManualLogSource log;readonly Queue<Job> queue=new Queue<Job>();Player? inventoryActor;InventoryResourceProvider? inventoryProvider;
  readonly AsyncPlacementTransaction asynchronous;readonly FilePlacementJournal journal;readonly List<Job> inFlight=new List<Job>();readonly Queue<List<Job>> grids=new Queue<List<Job>>();readonly List<AsyncPlacementJob> recovering=new List<AsyncPlacementJob>();readonly HashSet<string> providerIds=new HashSet<string>(StringComparer.Ordinal);string recoverySession="",recoveryActor="";long recoveryWorld;float nextBind;long recoveryPid;string? recoveryPidText;static readonly System.Version AsyncProviderVersion=new System.Version(2,0);
  readonly int unityThread=Thread.CurrentThread.ManagedThreadId;
  internal PlacementController(WildrootConfig settings,PrefabRegistrar catalog,ManualLogSource log){this.settings=settings;this.catalog=catalog;this.log=log;journal=new FilePlacementJournal(Path.Combine(BepInEx.Paths.ConfigPath,"Wildroot","placements"));asynchronous=new AsyncPlacementTransaction(journal);}
  internal IResourceProvider Resources(Player actor){if(CruciblePlugin.Services.Capabilities.TryResolve<IResourceProvider>("meadworks.resources",new System.Version(1,0),out var shared))return shared!;if(inventoryActor!=actor||inventoryProvider==null){inventoryActor=actor;inventoryProvider=new InventoryResourceProvider(actor);}return inventoryProvider;}
  internal static ResourceContext Context(Player actor,string id)=>new ResourceContext(actor.GetPlayerID().ToString(CultureInfo.InvariantCulture),NativeWorld.CurrentUid().ToString(CultureInfo.InvariantCulture),id);
  /// <summary>Harvest replacements share the grid transaction engine and journal, without grid scatter or tool effort.</summary>
  internal AsyncPlacementJob BeginReplant(Player actor,Piece piece,Vector3 position,Quaternion rotation,PlacementOperation operation,IAsyncResourceProvider provider,string source){
   string session=CruciblePlugin.Session.SessionId,providerId=AsyncPlacementTransaction.ProviderIdentity(provider);providerIds.Add(providerId);
   return asynchronous.Begin(new AsyncResourceContext(operation.Context.ActorId,NativeWorld.CurrentUid(),session,operation.Context.RequestId),operation,new PlacementBackend(actor,piece,position,rotation,true,session,harvestedSource:source),provider,providerId);
  }
  static bool Allowed(Vector3 pos)=>PrivateArea.CheckAccess(pos,0,false,true)&&!Location.IsInsideNoBuildLocation(pos);
  static bool Permission(Vector3 pos)=>ZNetScene.instance&&ZNetScene.instance.IsAreaReady(pos)&&Allowed(pos);
  static double ServerLatency(){if(!ZNet.instance||ZNet.instance.IsServer())return 0;ZNet.instance.GetNetStats(out _,out _,out var ping,out _,out _);return Math.Max(0,ping)/1000d;}
  static void Background(Job job){if(job.Background)return;job.Background=true;job.Batch.BackgroundPending++;}
  static void Complete(Job job,PlacementResult result){if(job.Background)job.Batch.BackgroundPending--;job.Batch.Complete(new PlacementResult(result.Kind,job.Cell));}
  internal static ItemDrop.ItemData? RightItem(Player actor)=>actor?(ItemDrop.ItemData?)NativeFields.GetRightItem.Invoke(actor,Array.Empty<object>()):null;
  // The player still planting this grid. The cultivator is matched by kind, not by item instance: another mod may
  // rebuild inventory items (a shared-material payment does), and that must not end the grid.
  bool PlayerPlanting(Job job)=>!job.Batch.Cancelled&&settings.Active&&job.Actor&&job.Actor==Player.m_localPlayer&&!job.Actor.IsDead()&&!job.Actor.IsTeleporting()&&NativeWorld.IsCurrent(job.World)&&HoldsTool(job);
  bool HoldsTool(Job job){var tool=RightItem(job.Actor);return tool!=null&&(tool==job.Tool&&job.Actor.GetInventory().ContainsItem(tool)||SharedPlacementFlow.IsCultivator(ToolName(tool)));}
  /// <summary>Whether this one cell can still be placed. A cell that cannot is skipped and never charged; the rest of the grid goes on.</summary>
  static bool CellPlaceable(Job job)=>Permission(job.Position);
  /// <summary>The spot is permitted but the game reports its area as not ready yet. Picking up an item nearby does this for a
  /// few frames while the picked object and its drop settle, so a retried cell waits for it (bounded) instead of being skipped.</summary>
  static bool AreaSettling(Job job)=>ZNetScene.instance&&!ZNetScene.instance.IsAreaReady(job.Position)&&PrivateArea.CheckAccess(job.Position,0,false,true)&&!Location.IsInsideNoBuildLocation(job.Position);
  bool Active(Job job)=>PlayerPlanting(job)&&CellPlaceable(job);
  static string? ToolName(ItemDrop.ItemData? tool)=>tool!=null&&tool.m_dropPrefab?tool.m_dropPrefab.name:null;
  static void ChargeTool(Player actor,float durability){var tool=RightItem(actor);if(tool!=null&&SharedPlacementFlow.IsCultivator(ToolName(tool)))tool.m_durability=Math.Max(0,tool.m_durability-durability);}
  public PlacementBatchHandle? LastSubmission{get;private set;}
  internal GridPlacementPlan? LastGamePlan{get;private set;}PlacementBatchHandle? gameBatch;
  /// <summary>A grid planted from the game is still being placed; PlantEasily refuses new placements meanwhile. A canceled
  /// grid (pause menu, another window, world change) and cells settling in the background never hold the cultivator.</summary>
  internal bool IsPlanting=>gameBatch!=null&&SharedPlacementFlow.BlocksPlanting(gameBatch.Pending-gameBatch.BackgroundPending,gameBatch.Cancelled);
  readonly HashSet<string> backgroundNoticeBatches=new HashSet<string>(StringComparer.Ordinal);
  readonly List<Job> detached=new List<Job>();
  int inputFrame=-1;long inputWorld;Player? inputActor;string inputPiece="";PlacementBatchHandle? inputBatch;
  internal PlacementBatchHandle? SubmitFromGame(Piece piece,PlantingLayout layout,GridPlacementPlan plan){
   var actor=Player.m_localPlayer;long world=NativeWorld.CurrentUid();
   if(inputFrame==Time.frameCount&&inputActor==actor&&inputWorld==world&&inputPiece==piece.name){LastSubmission=inputBatch;return inputBatch;}
   inputFrame=Time.frameCount;inputActor=actor;inputWorld=world;inputPiece=piece.name;inputBatch=SubmitLayout(piece,layout,plan);if(inputBatch!=null){gameBatch=inputBatch;LastGamePlan=plan;}return inputBatch;
  }
  internal bool CanHandle(Piece piece){var actor=Player.m_localPlayer;var tool=actor?RightItem(actor):null;return actor&&piece&&catalog.Registered&&settings.Active&&tool?.m_dropPrefab&&tool.m_dropPrefab.name=="Cultivator"&&catalog.Definitions.ContainsKey(piece.name.Replace("(Clone)",""));}
  internal bool RecoveryPrefabAllowed(string source)=>catalog.Registered&&settings.Active&&catalog.Definitions.TryGetValue(source,out var definition)&&definition.Enabled;
  internal GridPlacementPlan Plan(Player actor,PlantingLayout layout)=>GridPlacementRules.Decide(layout.Statuses,settings.Get("Planting.PreventInvalidPlanting",true),settings.Get("Planting.PreventPartialPlanting",false),GridPreview.NoPlacementCost(actor));
  /// <summary>Plants a grid at an explicit root without snapping (developer and integration entry point).</summary>
  public PlacementBatchHandle? Submit(Piece piece,Vector3 anchor,Quaternion rotation){
   LastSubmission=null;var actor=Player.m_localPlayer;var preview=Plugin.Instance?.Preview;if(!actor||!piece||preview==null||!CanHandleSubmit(actor,piece))return null;
   PlantingLayout layout;try{layout=preview.Compute(actor,piece,NativePlanting.Ground(anchor),rotation,false);}catch(ArgumentException e){log.LogWarning("Placement rejected: "+e.Message);return null;}
   preview.EvaluateAll(actor,layout,null);return SubmitLayout(piece,layout,Plan(actor,layout));
  }
  bool CanHandleSubmit(Player actor,Piece piece)=>RightItem(actor)!=null&&catalog.Registered&&settings.Active&&catalog.Definitions.TryGetValue(piece.name.Replace("(Clone)",""),out var definition)&&definition.Enabled;
  internal PlacementBatchHandle? SubmitLayout(Piece piece,PlantingLayout layout,GridPlacementPlan plan){
   LastSubmission=null;
   var actor=Player.m_localPlayer;var tool=actor?RightItem(actor):null;if(!actor||!piece||tool==null||!catalog.Registered||!settings.Active||!catalog.Definitions.TryGetValue(piece.name.Replace("(Clone)",""),out var definition)||!definition.Enabled)return null;
   int count=layout.Cells.Length,limit=Math.Max(1,settings.Get("Performance.MaxConcurrentPlacements",500));if(count+queue.Count+inFlight.Count+detached.Count+grids.Sum(g=>g.Count)>limit)return null;
   string id=System.Guid.NewGuid().ToString("N");var handle=new PlacementBatchHandle(id,count);LastSubmission=handle;var resource=Resources(actor);CruciblePlugin.Services.Capabilities.TryResolve<IAsyncResourceProvider>("meadworks.resources.async",new System.Version(2,0),out var shared);
   var scatter=new ScatterOptions(settings.Get("Planting.RandomizeRotation",true),settings.Get("Planting.EnableScatter",true),settings.Get("Planting.PositionScatterRadius",0f),settings.Get("Planting.RotationScatterAngle",0f));var random=new SeededScatterRandom(id.GetHashCode());
   var placed=new HashSet<int>(plan.Placed);var pending=new List<Job>();double latency=ServerLatency();
   for(int i=0;i<count;i++){
    string cell=id+":"+(i/layout.Columns).ToString(CultureInfo.InvariantCulture)+":"+(i%layout.Columns).ToString(CultureInfo.InvariantCulture);
    if(!placed.Contains(i)){handle.Complete(new PlacementResult(!plan.Refused&&layout.Statuses[i]==GhostStatus.LackResources?PlacementResultKind.InsufficientResources:PlacementResultKind.Cancelled,cell));continue;}
    var final=PlacementScatter.Apply(NativePlanting.V(layout.Cells[i]),layout.Rotation.eulerAngles.y,scatter,random);var position=NativePlanting.U(final.Position);
    pending.Add(new Job{Actor=actor,Tool=tool,Piece=piece,Definition=definition,Position=position,Rotation=Quaternion.Euler(0,(float)final.Yaw,0)*Quaternion.Euler((float)final.Pitch,0,(float)final.Roll),World=NativeWorld.CurrentUid(),Batch=handle,Cell=cell,PreflightValid=Allowed(position),Resources=resource,SharedResources=shared,Retry=new SharedPlacementRetry(latency)});
   }
   if(pending.Count==0)return handle;
   if(settings.Get("Planting.PreventPartialPlanting",false)&&shared!=null&&pending.Count==count&&pending.All(j=>j.PreflightValid)){grids.Enqueue(pending);return handle;}
   foreach(var job in pending)queue.Enqueue(job);return handle;
  }
  internal void Update(){
   int batch=Math.Max(1,Math.Min(10000,settings.Get("Performance.BulkPlantingBatchSize",2)));
   UpdateRecovery();
   asynchronous.Poll(batch);
   if(recovering.Count>0)foreach(var job in recovering.Where(j=>j.Result!=null).ToArray())recovering.Remove(job);
   double now=Time.realtimeSinceStartupAsDouble;
   foreach(var job in inFlight.Count+detached.Count>0?inFlight.Concat(detached).ToArray():Array.Empty<Job>()){ // no per-frame copy while nothing is in flight
    if(job.Async!.Result==null){
     if(job.Async.NeedsRecovery)Background(job);
     if(job.Detached||now-job.AsyncSince<job.Retry.WindowSeconds)continue;
     // Keep the exact transaction alive for a late answer; never retry an uncertain payment under a new id.
     job.Async.Cancel();Background(job);job.Detached=true;inFlight.Remove(job);detached.Add(job);
     if(backgroundNoticeBatches.Add(job.Batch.Id))log.LogWarning("Wildroot is finishing some shared-material plantings in the background (grid "+job.Batch.Id+"); each one is placed or its materials are returned.");
     continue;
    }
    var result=job.Async.Result;if(result.Kind==PlacementResultKind.Committed&&job.Actor&&job.Actor==Player.m_localPlayer&&NativeWorld.IsCurrent(job.World)){if(job.UseStamina)job.Actor.UseStamina(job.Stamina);if(job.UseDurability)ChargeTool(job.Actor,job.Durability);}
    bool retryUnpaid=!job.Detached&&job.Retry.TryRetry(result.Kind,PlayerPlanting(job),job.PreflightValid&&Allowed(job.Position),now);
    // A settled whole-grid failure is retried cell by cell, after its original payment has been returned.
    if(!job.Detached&&(job.Aggregate&&SharedPlacementFlow.RetryCellsIndividually(result.Kind,job.Batch.Cancelled)||retryUnpaid)){Background(job);job.LastUnpaid=result.Kind;job.Aggregate=false;job.Async=null;inFlight.Remove(job);queue.Enqueue(job);continue;}
    Complete(job,result);inFlight.Remove(job);detached.Remove(job);
   }
   if(grids.Count>0){var group=grids.Dequeue();StartGrid(group);}
   while(batch-->0&&queue.Count>0){
    // Shared-material cells wait on the provider a few at a time; the rest stay queued instead of overrunning its admission.
    var next=queue.Peek();if(next.SharedResources!=null&&next.PreflightValid&&!SharedPlacementFlow.CanStart(inFlight.Count))break;
    bool planting=PlayerPlanting(next),permitted=next.PreflightValid&&Allowed(next.Position);
    if(next.SharedResources!=null&&planting&&permitted&&(next.Retry.Waiting(now)||next.Retry.WaitForArea(planting,permitted,!AreaSettling(next),now))){Background(next);queue.Enqueue(queue.Dequeue());continue;}
    var job=queue.Dequeue();PlacementResult result;
    if(job.SharedResources!=null&&job.Retry.Expired(now))result=new PlacementResult(job.LastUnpaid,job.Cell);
    else if(!job.PreflightValid||!Active(job))result=new PlacementResult(PlacementResultKind.Cancelled,job.Cell);
    else{
     float stamina=(float)AccessTools.Method(typeof(Player),"GetBuildStamina").Invoke(job.Actor,Array.Empty<object>());bool useStamina=settings.Get("PlantEasily.Difficulty.UseStamina",true),useDurability=settings.Get("PlantEasily.Difficulty.UseDurability",true);float durability=(float)AccessTools.Method(typeof(Player),"GetPlaceDurability").Invoke(job.Actor,new object[]{job.Tool});
     // PlantEasily charges effort per placed piece without a sufficiency check, so a large grid can exhaust stamina.
     useDurability&=job.Tool.m_shared.m_useDurability;
     {var operation=new PlacementOperation(Context(job.Actor,job.Request),job.Definition.Costs,()=>Active(job));if(job.SharedResources!=null){job.UseStamina=useStamina;job.UseDurability=useDurability;job.Stamina=stamina;job.Durability=durability;string providerId=AsyncPlacementTransaction.ProviderIdentity(job.SharedResources);providerIds.Add(providerId);job.AsyncSince=now;job.Async=asynchronous.Begin(new AsyncResourceContext(job.Actor.GetPlayerID().ToString(CultureInfo.InvariantCulture),job.World,CruciblePlugin.Session.SessionId,job.Request),operation,new PlacementBackend(job.Actor,job.Piece,job.Position,job.Rotation,true,CruciblePlugin.Session.SessionId),job.SharedResources,providerId);inFlight.Add(job);break;}result=job.Batch.Transactions.Execute(operation,new PlacementBackend(job.Actor,job.Piece,job.Position,job.Rotation),job.Resources);if(result.Kind==PlacementResultKind.Committed){if(useStamina)job.Actor.UseStamina(stamina);if(useDurability)ChargeTool(job.Actor,durability);}}
    }
    Complete(job,result);if(result.Kind==PlacementResultKind.RecoveryRequired)log.LogWarning("Placement needs recovery: "+job.Cell);
   }
  }
  void StartGrid(List<Job> group){
   if(!group.All(PlayerPlanting)){foreach(var cell in group)Complete(cell,new PlacementResult(PlacementResultKind.Cancelled,cell.Cell));return;}
   // A spot that changed since the grid was aimed is skipped; the other cells are planted one by one.
   if(!group.All(CellPlaceable)){foreach(var cell in group)queue.Enqueue(cell);return;}
   var first=group[0];float stamina=(float)AccessTools.Method(typeof(Player),"GetBuildStamina").Invoke(first.Actor,Array.Empty<object>()),durability=(float)AccessTools.Method(typeof(Player),"GetPlaceDurability").Invoke(first.Actor,new object[]{first.Tool});bool useStamina=settings.Get("PlantEasily.Difficulty.UseStamina",true),useDurability=settings.Get("PlantEasily.Difficulty.UseDurability",true);
   useDurability&=first.Tool.m_shared.m_useDurability;bool Valid()=>group.All(PlayerPlanting);
   string session=CruciblePlugin.Session.SessionId,providerId=AsyncPlacementTransaction.ProviderIdentity(first.SharedResources!);providerIds.Add(providerId);
   var operations=group.Select(cell=>new PlacementOperation(Context(cell.Actor,cell.Cell),cell.Definition.Costs,Valid)).ToArray();var backends=group.Select(cell=>(IPlacementBackend)new PlacementBackend(cell.Actor,cell.Piece,cell.Position,cell.Rotation,true,session)).ToArray();
   var transaction=asynchronous.BeginBatch(new AsyncResourceContext(first.Actor.GetPlayerID().ToString(CultureInfo.InvariantCulture),first.World,session,first.Batch.Id),operations,backends,first.SharedResources!,providerId);
   foreach(var cell in group){cell.Async=transaction;cell.AsyncSince=Time.realtimeSinceStartupAsDouble;cell.Aggregate=true;cell.UseStamina=useStamina;cell.UseDurability=useDurability;cell.Stamina=stamina;cell.Durability=durability;inFlight.Add(cell);}
  }
  void UpdateRecovery(){
   var actor=Player.m_localPlayer;if(!actor||!ZNet.instance||!catalog.Registered){recoverySession="";return;}long world=NativeWorld.CurrentUid();long pid=actor.GetPlayerID();if(recoveryPidText==null||pid!=recoveryPid){recoveryPid=pid;recoveryPidText=pid.ToString(CultureInfo.InvariantCulture);}string actorId=recoveryPidText,session=CruciblePlugin.Session.SessionId;if(string.IsNullOrEmpty(session)||world==0)return;
   // The provider is only used for a new session/actor/world or a due bind; the lookup allocates, so skip it otherwise.
   if(recoverySession==session&&recoveryActor==actorId&&recoveryWorld==world&&Time.unscaledTime<nextBind)return;
   CruciblePlugin.Services.Capabilities.TryResolve<IAsyncResourceProvider>("meadworks.resources.async",AsyncProviderVersion,out var shared);string? currentId=shared==null?null:AsyncPlacementTransaction.ProviderIdentity(shared);
   if(recoverySession!=session||recoveryActor!=actorId||recoveryWorld!=world){
    recoverySession=session;recoveryActor=actorId;recoveryWorld=world;nextBind=0;
    try{
     var records=journal.LoadRecoverable(out var unreadable);
     if(unreadable.Count>0)log.LogWarning("Wildroot moved "+unreadable.Count+" unreadable planting record(s) to "+journal.RetiredDirectory+"; they are kept there and no longer replayed.");
     foreach(var record in records.Where(r=>r.ActorId==actorId&&r.WorldId==world&&(!r.Completed||r.Payment==AsyncReservationState.Committed))){
     try{var backends=record.Cells.Select(c=>(IPlacementBackend)PlacementBackend.Restore(actor,c.RecoveryData,record.SessionId)).ToArray();var operations=record.Cells.Select(c=>new PlacementOperation(new ResourceContext(record.ActorId,record.WorldId.ToString(CultureInfo.InvariantCulture),c.RequestId),c.Costs,()=>false)).ToArray();providerIds.Add(record.ProviderId);var job=asynchronous.Restore(record,operations,backends,currentId==record.ProviderId?shared:null,record.ProviderId);if(!recovering.Contains(job))recovering.Add(job);}
     // A record that can no longer be replayed (its plant is gone from the catalog, or its plan no longer matches) is moved aside once, not retried every login.
     catch(IOException error){log.LogWarning("Wildroot kept planting record "+record.RequestId+" for the next load: "+error.Message);}
     catch(Exception error){log.LogWarning("Wildroot retired planting record "+record.RequestId+" that can no longer be replayed ("+error.Message+")"+(journal.Retire(record)?"; it is kept in "+journal.RetiredDirectory+".":"; it stays in place."));}
    }}catch(Exception error){log.LogWarning("Shared placement journal unavailable: "+error.Message);}
   }
   if(Time.unscaledTime>=nextBind){nextBind=Time.unscaledTime+1;foreach(string id in providerIds)asynchronous.Bind(id,id==currentId?shared:null);}
  }
  internal void RecordRemoval(string actor,long world,string session,string request,string native){try{asynchronous.RecordRemoval(actor,world,session,request,native);}catch(Exception error){log.LogError("Placement lifecycle journal unavailable: "+error.Message);}}
  internal void CancelAll(){while(queue.Count>0){var job=queue.Dequeue();job.Batch.Cancel();Complete(job,new PlacementResult(PlacementResultKind.Cancelled,job.Cell));}while(grids.Count>0)foreach(var job in grids.Dequeue()){job.Batch.Cancel();Complete(job,new PlacementResult(PlacementResultKind.Cancelled,job.Cell));}foreach(var job in inFlight.Concat(detached))job.Batch.Cancel();asynchronous.CancelAll();inventoryProvider=null;inventoryActor=null;}
  internal bool IsRetiring{get;private set;}
  internal async Task ShutdownAsync(){
   IsRetiring=true;
   CancelAll();var context=SynchronizationContext.Current;
   bool OnOwnerContext()=>context!=null&&Thread.CurrentThread.ManagedThreadId==unityThread&&SynchronizationContext.Current==context;
   try{
    if(!OnOwnerContext())log.LogWarning("Wildroot shutdown has no Unity owner context; unfinished plantings are saved and resume when this world loads again.");
    await asynchronous.RetireAsync(OnOwnerContext);
    if(asynchronous.HasRecoveryRequired)log.LogWarning("Wildroot saved unfinished plantings; they resume when this world loads again.");
   }catch(Exception error){log.LogError("Wildroot shutdown cleanup retained for recovery: "+error.GetType().Name);}
  }
  internal bool HasRecoveryRequired=>asynchronous.HasRecoveryRequired;
  sealed class Job {public Player Actor=null!;public ItemDrop.ItemData Tool=null!;public Piece Piece=null!;public PlantDefinition Definition=null!;public Vector3 Position;public Quaternion Rotation;public long World;public PlacementBatchHandle Batch=null!;public string Cell="";public bool PreflightValid,UseStamina,UseDurability,Aggregate,Background,Detached;public PlacementResultKind LastUnpaid=PlacementResultKind.Cancelled;public SharedPlacementRetry Retry=null!;public double AsyncSince;public string Request=>Retry.RequestId(Cell);public float Stamina,Durability;public IResourceProvider Resources=null!;public IAsyncResourceProvider? SharedResources;public AsyncPlacementJob? Async;}
 }
}
