using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>The source owner alone executes native harvest. Broadcast picked state is never a requester receipt.</summary>
 internal sealed class NativeHarvestService:IDisposable {
  readonly WildrootConfig settings;readonly IDisposable operation,command;
  readonly Dictionary<RequestIdentity,Forwarded> forwarded=new Dictionary<RequestIdentity,Forwarded>();
  bool disposed;
  sealed class Forwarded {internal RequestIdentity Identity=null!;internal NativeHarvestRequest Request=null!;internal Task<OperationResult<NativeHarvestReceipt>> Task=null!;}
  [ThreadStatic]static DispatchScope? dispatch;
  [ThreadStatic]static ZDO? effectActor;
  [ThreadStatic]static ZDOID collecting;
  const string CollectionLease="wildroot.harvest.collection-until";
  sealed class DispatchScope {internal ZNetView View=null!;internal int Bonus;internal bool Requested;}
  internal NativeHarvestService(WildrootConfig settings,ProductRegistration registration){
   this.settings=settings;
   command=CruciblePlugin.Services.PeerCommands.Register(registration,new OperationDescriptor(NativeHarvestWire.Command,NativeHarvestWire.OwnerVersion),new NativeHarvestCommandCodec(),Owner);
   operation=CruciblePlugin.Services.Operations.Register(registration,new OperationDescriptor(NativeHarvestWire.Operation,NativeHarvestWire.OperationVersion),new NativeHarvestCodec(),Forward,Recover);
  }
  internal NativeHarvestRequest Prepare(Component source,Player actor,float requesterRadius,Vector3 primaryOrigin){
   var view=source.GetComponent<ZNetView>();var session=CruciblePlugin.Session;
   if(!view||!view.IsValid()||!session.AuthorityReady||session.WorldId!=NativeWorld.CurrentUid())throw new InvalidOperationException("HarvestSessionUnavailable");
   var scope=new DispatchScope{View=view};dispatch=scope;
   try{if(source is Pickable pickable)pickable.Interact(actor,false,false);else if(source is Beehive hive)hive.Interact(actor,false,false);else throw new InvalidOperationException("HarvestTargetInvalid");}
   finally{dispatch=null;}
   if(!scope.Requested)throw new InvalidOperationException("HarvestNotRequested");
   return new NativeHarvestRequest(requesterRadius,primaryOrigin){Root=Guid.NewGuid().ToString("N"),Actor=actor.GetPlayerID().ToString(CultureInfo.InvariantCulture),World=session.WorldId,Session=session.SessionId,Target=view.GetZDO().m_uid,Bonus=scope.Bonus};
  }
  /// <summary>True while bulk harvest runs a native Interact on this thread; that pick's extras wait for the owner's receipt.</summary>
  internal static bool Dispatching=>dispatch!=null;
  internal static bool CaptureDispatch(ZNetView view,string method,object[] arguments){
   var scope=dispatch;if(scope==null||scope.View!=view||method!="RPC_Pick"&&method!="RPC_Extract")return false;
   if(scope.Requested)throw new InvalidOperationException("Duplicate native harvest dispatch.");
   scope.Requested=true;scope.Bonus=method=="RPC_Pick"?(int)arguments[0]:0;return true;
  }
   internal static ZDOID EffectActor()=>effectActor!=null?effectActor.m_uid:Player.m_localPlayer.GetZDOID();
   internal static bool PickupAllowed(ItemDrop drop){var view=drop.GetComponent<ZNetView>();if(!view||!view.IsValid()||!ZNet.instance)return true;var zdo=view.GetZDO();return collecting==zdo.m_uid||zdo.GetLong(CollectionLease,0)<=ZNet.instance.GetTime().Ticks;}
   internal static bool CollectionLeased(ItemDrop drop){var view=drop.GetComponent<ZNetView>();return view&&view.IsValid()&&ZNet.instance&&view.GetZDO().GetLong(CollectionLease,0)>ZNet.instance.GetTime().Ticks;}
   // Auto-stacking is not collection: even the exact pickup scope may not merge or destroy a leased identity.
   internal static bool AutoStackCandidateOwned(ItemDrop drop){var view=drop.GetComponent<ZNetView>();return view&&view.IsValid()&&view.IsOwner()&&!CollectionLeased(drop);}
  internal static int Collect(Player player,ItemDrop drop,out int remaining){
   int before=drop.m_itemData.m_stack;remaining=before;var view=drop.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner())return 0;
   collecting=view.GetZDO().m_uid;
   try{
    bool picked=player.Pickup(drop.gameObject,true,false);
    // Native AddItem can move part of a stack and return false when the remainder has no slot.
    remaining=picked?0:drop?Math.Max(0,Math.Min(before,drop.m_itemData.m_stack)):before;
    return before-remaining;
   }finally{
    collecting=ZDOID.None;
    // Match ItemDrop.Pickup's save after native pickup, including surviving partial stacks.
    if(drop&&view&&view.IsValid()&&view.IsOwner())ItemDrop.SaveToZDO(drop.m_itemData,view.GetZDO());
   }
  }
  internal static void Release(ZDOID id){var obj=ZNetScene.instance?ZNetScene.instance.FindInstance(id):null;var view=obj?obj.GetComponent<ZNetView>():null;if(view&&view.IsValid()&&view.IsOwner())view.GetZDO().Set(CollectionLease,0L);}
  OperationExecution<NativeHarvestReceipt> Forward(AuthorityContext context,NativeHarvestRequest request)=>Forward(context,request,Time.unscaledTime+5f);
  OperationExecution<NativeHarvestReceipt> Forward(AuthorityContext context,NativeHarvestRequest request,float publicationDeadline){
   var current=CruciblePlugin.Session;
   if(disposed||!settings.Active||context.ActorId!=request.Actor||context.Session.WorldId!=request.World||context.Session.SessionId!=request.Session||!current.AuthorityReady||current.WorldId!=request.World||current.SessionId!=request.Session||!NativeWorld.IsCurrent(request.World))return OperationExecution<NativeHarvestReceipt>.Rejected("HarvestIdentity");
   var zdo=ZDOMan.instance?.GetZDO(request.Target);
   // A newly created remote source may reach the authority after this request. Stay undecided
   // while native publication catches up, and revalidate the current session and owner each poll.
   if(zdo==null||zdo.GetOwner()==0)return Time.unscaledTime<publicationDeadline?OperationExecution<NativeHarvestReceipt>.Pending(()=>Forward(context,request,publicationDeadline)):OperationExecution<NativeHarvestReceipt>.Rejected("TargetLost");
   // Once sent, cancellation cannot assert that the native owner did not harvest.
   context.MarkDecisionMade();
   var sender=context.IsLocal?ZNet.GetUID().ToString(CultureInfo.InvariantCulture):context.SenderId;
   var owner=zdo.GetOwner().ToString(CultureInfo.InvariantCulture);
   var item=new Forwarded{Identity=context.Identity,Request=request,Task=CruciblePlugin.PeerCommandClient.SendAsync<NativeHarvestCommand,NativeHarvestReceipt>(owner,NativeHarvestWire.Command,context.Identity.RequestId,new NativeHarvestCommand{Request=request,SenderPeer=sender},CancellationToken.None)};
   forwarded.Add(context.Identity,item);return Poll(item);
  }
  OperationExecution<NativeHarvestReceipt> Recover(RequestIdentity identity)=>forwarded.TryGetValue(identity,out var item)?Poll(item):OperationExecution<NativeHarvestReceipt>.RecoveryRequired();
  OperationExecution<NativeHarvestReceipt> Poll(Forwarded item){
   if(disposed||!item.Task.IsCompleted)return disposed?OperationExecution<NativeHarvestReceipt>.RecoveryRequired():OperationExecution<NativeHarvestReceipt>.Pending(()=>Poll(item));
   if(item.Task.IsFaulted||item.Task.IsCanceled)return OperationExecution<NativeHarvestReceipt>.RecoveryRequired();
   var result=item.Task.Result;
   if(result.State!=OperationState.Succeeded||result.Reply==null||!result.Reply.Matches(item.Request))return OperationExecution<NativeHarvestReceipt>.RecoveryRequired();
   forwarded.Remove(item.Identity);return OperationExecution<NativeHarvestReceipt>.Success(result.Reply);
  }
  static NativeHarvestReceipt Denied(NativeHarvestRequest request,string reason)=>new NativeHarvestReceipt{Request=request,Reason=reason};
  OperationExecution<NativeHarvestReceipt> Owner(PeerCommandContext context,NativeHarvestCommand command)=>Owner(context,command,Time.unscaledTime+5f);
  OperationExecution<NativeHarvestReceipt> Owner(PeerCommandContext context,NativeHarvestCommand command,float areaDeadline){
   var request=command.Request;
   if(disposed||!context.IsLocal&&!context.ServerConnectionVerified||!context.Session.AuthorityReady||context.Identity==null||context.Session.WorldId!=request.World||context.Session.SessionId!=request.Session)return OperationExecution<NativeHarvestReceipt>.Rejected("HarvestIdentity");
   // Direct handler calls must satisfy the same bounds as wire calls.
   try{request=NativeHarvestWire.RequestValue(NativeHarvestWire.Encode(request));}catch{return OperationExecution<NativeHarvestReceipt>.Rejected("HarvestMalformed");}
   if(!settings.Active)return OperationExecution<NativeHarvestReceipt>.Success(Denied(request,"HarvestDisabled"));
   var target=ZNetScene.instance?ZNetScene.instance.FindInstance(request.Target):null;var view=target?target.GetComponent<ZNetView>():null;
   if(!view||!view.IsValid()||!view.IsOwner()||view.GetZDO().GetOwner()!=ZNet.GetUID())return OperationExecution<NativeHarvestReceipt>.Success(Denied(request,"TargetOwnerChanged"));
   if(!TryActor(request.Actor,command.SenderPeer,out var player,out var character,out var peer))return OperationExecution<NativeHarvestReceipt>.Success(Denied(request,"HarvestActorUnavailable"));
   // The requester selected this sphere around the primary crop. That primary may already be destroyed;
   // validate its frozen position against the real character instead of requiring a live primary ZDO.
   var position=target!.transform.position;float radius=request.RequesterRadius;var origin=request.PrimaryOrigin;var actorPosition=character!.GetPosition();var eyePosition=actorPosition+Vector3.up*EyeHeight();
   if(float.IsNaN(radius)||float.IsInfinity(radius)||radius<0||!Finite(origin)||!Finite(position)||!Finite(actorPosition)||!HarvestPlanner.ActorInReach(Vector3.Distance(actorPosition,origin),Vector3.Distance(eyePosition,origin))||Vector3.Distance(position,origin)>radius+0.05f||Vector3.Distance(actorPosition,position)>radius+5f||character.GetBool(ZDOVars.s_dead,false)||player&&(player.IsDead()||player.IsTeleporting())){
    Debug.LogWarning("Wildroot harvest actor guard: distance="+Vector3.Distance(actorPosition,position)+", limit="+(radius+5f)+", originDistance="+Vector3.Distance(actorPosition,origin)+", eyeOriginDistance="+Vector3.Distance(eyePosition,origin)+", sourceDistance="+Vector3.Distance(position,origin)+", areaReady="+ZNetScene.instance.IsAreaReady(position)+", dead="+character.GetBool(ZDOVars.s_dead,false)+", localDead="+(player&&player.IsDead())+", localTeleporting="+(player&&player.IsTeleporting()));
    return OperationExecution<NativeHarvestReceipt>.Success(Denied(request,"HarvestOutOfRange"));
   }
   // Native destruction removes an instance before its ZDO is drained, temporarily making adjacent areas unready.
   // Stay undecided and revalidate ownership, session, actor and permissions on every poll.
   if(!ZNetScene.instance.IsAreaReady(position))return Time.unscaledTime<areaDeadline?OperationExecution<NativeHarvestReceipt>.Pending(()=>Owner(context,command,areaDeadline)):OperationExecution<NativeHarvestReceipt>.Success(Denied(request,"HarvestAreaNotReady"));
   long actor=long.Parse(request.Actor,CultureInfo.InvariantCulture);
   if(!WardAccess(position,actor))return OperationExecution<NativeHarvestReceipt>.Success(Denied(request,"WardDenied"));
   var pickable=target.GetComponent<Pickable>();var hive=target.GetComponent<Beehive>();
   if(!pickable&&!hive||pickable&&((int)AccessTools.Field(typeof(Pickable),"m_enabled").GetValue(pickable)==0||(bool)NativeFields.GetPicked.Invoke(pickable,Array.Empty<object>())||!(bool)NativeFields.CanBePicked.Invoke(pickable,Array.Empty<object>()))||hive&&Honey(hive)<=0)return OperationExecution<NativeHarvestReceipt>.Success(Denied(request,"NotRipe"));
   if(pickable){int maximum=(int)AccessTools.Field(typeof(Pickable),"m_bonusYieldAmount").GetValue(pickable);if(request.Bonus!=0&&request.Bonus!=maximum)return OperationExecution<NativeHarvestReceipt>.Success(Denied(request,"HarvestBonusInvalid"));if((bool)AccessTools.Field(typeof(Pickable),"m_tarPreventsPicking").GetValue(pickable)){var floating=pickable.GetComponent<Floating>();if(floating&&floating.IsInTar())return OperationExecution<NativeHarvestReceipt>.Success(Denied(request,"HarvestInTar"));}}
   else if(request.Bonus!=0)return OperationExecution<NativeHarvestReceipt>.Success(Denied(request,"HarvestBonusInvalid"));
   var before=new HashSet<int>(UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None).Select(d=>d.GetInstanceID()));
   // A competing command cannot run between this ripe check and the native picked/level write.
   context.MarkDecisionMade();effectActor=character;
   try{
    if(pickable)AccessTools.Method(typeof(Pickable),"RPC_Pick").Invoke(pickable,new object[]{peer,request.Bonus});
    else AccessTools.Method(typeof(Beehive),"RPC_Extract").Invoke(hive,new object[]{peer});
    bool harvested=!target||pickable&&(bool)NativeFields.GetPicked.Invoke(pickable,Array.Empty<object>())||hive&&Honey(hive)==0;
    if(!harvested)return OperationExecution<NativeHarvestReceipt>.RecoveryRequired();
    var drops=new List<NativeHarvestDrop>();
    foreach(var drop in UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None)){
     if(before.Contains(drop.GetInstanceID()))continue;var dropView=drop.GetComponent<ZNetView>();
     if(!dropView||!dropView.IsValid()||!dropView.IsOwner()||drop.m_itemData.m_stack<1)return OperationExecution<NativeHarvestReceipt>.RecoveryRequired();
     var zdo=dropView.GetZDO();ItemDrop.SaveToZDO(drop.m_itemData,zdo);zdo.Set(CollectionLease,ZNet.instance.GetTime().AddSeconds(30).Ticks);drops.Add(new NativeHarvestDrop{Id=zdo.m_uid,Amount=drop.m_itemData.m_stack});
     // Native item ownership moves to the authenticated winner; replicas retain exact drop identities.
     zdo.SetOwner(peer);if(peer!=ZNet.GetUID())ZDOMan.instance.ForceSendZDO(peer,zdo.m_uid);
    }
    var receipt=new NativeHarvestReceipt{Request=request,Harvested=true,Reason="Harvested",Drops=drops.ToArray()};NativeHarvestWire.Encode(receipt);
    return OperationExecution<NativeHarvestReceipt>.Success(receipt);
   }catch{return OperationExecution<NativeHarvestReceipt>.RecoveryRequired();}
   finally{effectActor=null;}
  }
  static int Honey(Beehive hive)=>(int)NativeFields.GetHoneyLevel.Invoke(hive,Array.Empty<object>());
  // The actor is a ZDO (its peer may be remote), so its eye is its position raised by the player prefab's eye height.
  static float eyeHeight=-1f;
  static float EyeHeight(){if(eyeHeight<0f){var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab("Player"):null;var player=prefab?prefab.GetComponent<Player>():null;if(!player||!player.m_eye)return 0f;eyeHeight=Mathf.Max(0f,player.m_eye.position.y-prefab!.transform.position.y);}return eyeHeight;}
  static bool Finite(Vector3 position)=>!float.IsNaN(position.x)&&!float.IsNaN(position.y)&&!float.IsNaN(position.z)&&!float.IsInfinity(position.x)&&!float.IsInfinity(position.y)&&!float.IsInfinity(position.z);
  internal static bool WardAccess(Vector3 position,long actor){
   bool inside=false,permitted=false;
   foreach(var area in (IEnumerable<PrivateArea>)AccessTools.Field(typeof(PrivateArea),"m_allAreas").GetValue(null)){
    if(!area||!(bool)AccessTools.Method(typeof(PrivateArea),"IsEnabled").Invoke(area,Array.Empty<object>())||!(bool)AccessTools.Method(typeof(PrivateArea),"IsInside").Invoke(area,new object[]{position,0f}))continue;
    inside=true;var piece=(Piece?)AccessTools.Field(typeof(PrivateArea),"m_piece").GetValue(area);
    if(piece&&piece.GetCreator()==actor||(bool)AccessTools.Method(typeof(PrivateArea),"IsPermitted").Invoke(area,new object[]{actor}))permitted=true;
   }
   return !inside||permitted;
  }
  static bool TryActor(string actorId,string sender,out Player? player,out ZDO? character,out long peer){
   player=null;character=null;peer=0;if(!ZNet.instance||!long.TryParse(actorId,NumberStyles.Integer,CultureInfo.InvariantCulture,out var actor)||actor==0||!long.TryParse(sender,NumberStyles.Integer,CultureInfo.InvariantCulture,out peer)||peer==0)return false;
   player=Player.GetPlayer(actor);if(player){var view=player!.GetComponent<ZNetView>();if(view&&view.IsValid())character=view.GetZDO();}
   if(character==null){var senderPeer=peer;var connection=ZNet.instance.GetPeers().FirstOrDefault(p=>p.m_uid==senderPeer&&p.IsReady()&&p.m_rpc!=null&&p.m_rpc.IsConnected());if(connection!=null)character=ZDOMan.instance?.GetZDO(connection.m_characterID);}
   if(character==null){foreach(var info in ZNet.instance.GetPlayerList()){var candidate=ZDOMan.instance?.GetZDO(info.m_characterID);if(candidate!=null&&candidate.GetOwner()==peer&&candidate.GetLong(ZDOVars.s_playerID,0)==actor){character=candidate;break;}}}
   if(character==null||character.GetOwner()!=peer||character.GetLong(ZDOVars.s_playerID,0)!=actor)return false;
   var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(character.GetPrefab()):null;return prefab&&prefab.GetComponent<Player>();
  }
  public void Dispose(){if(disposed)return;disposed=true;operation.Dispose();command.Dispose();forwarded.Clear();}
 }
 [HarmonyPatch(typeof(ZNetView),nameof(ZNetView.InvokeRPC),new[]{typeof(string),typeof(object[])})]
 static class NativeHarvestDispatchPatch {static bool Prefix(ZNetView __instance,string __0,object[] __1)=>!NativeHarvestService.CaptureDispatch(__instance,__0,__1);}
  [HarmonyPatch(typeof(ItemDrop),nameof(ItemDrop.CanPickup))]
  static class NativeHarvestPickupPatch {static bool Prefix(ItemDrop __instance,ref bool __result){if(NativeHarvestService.PickupAllowed(__instance))return true;__result=false;return false;}}
  [HarmonyPatch(typeof(ItemDrop),"AutoStackItems")]
  static class NativeHarvestAutoStackPatch {
   static bool Prefix(ItemDrop __instance)=>!NativeHarvestService.CollectionLeased(__instance);
   static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions){
    var code=instructions.ToList();var field=AccessTools.Field(typeof(ItemDrop),"m_nview");var owner=AccessTools.Method(typeof(ZNetView),nameof(ZNetView.IsOwner));int matches=0;
    for(int i=0;i<code.Count;i++){
     // The native candidate is still on the stack here; retain its ownership check and exclude only live leases.
     if(code[i].opcode==OpCodes.Ldfld&&Equals(code[i].operand,field)&&i+1<code.Count&&code[i+1].opcode==OpCodes.Callvirt&&Equals(code[i+1].operand,owner)){
      matches++;yield return new CodeInstruction(code[i]){opcode=OpCodes.Call,operand=AccessTools.Method(typeof(NativeHarvestService),nameof(NativeHarvestService.AutoStackCandidateOwned))};
      yield return new CodeInstruction(code[i+1]){opcode=OpCodes.Nop,operand=null};i++;
     }else yield return code[i];
    }
    if(matches!=1)throw new InvalidOperationException("Native auto-stack candidate ownership signature changed.");
   }
  }
 [HarmonyPatch(typeof(Pickable),"RPC_Pick")]
 static class NativeHarvestEffectPatch {
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions){
   var code=instructions.ToList();var field=AccessTools.Field(typeof(Player),"m_localPlayer");int matches=0;
   for(int i=0;i<code.Count;i++){
    if(code[i].opcode==OpCodes.Ldsfld&&Equals(code[i].operand,field)&&i+1<code.Count&&code[i+1].operand is MethodInfo method&&method.Name=="GetZDOID"){
     matches++;var replacement=new CodeInstruction(code[i]){opcode=OpCodes.Call,operand=AccessTools.Method(typeof(NativeHarvestService),nameof(NativeHarvestService.EffectActor))};yield return replacement;
     // Keep the second instruction as a no-op so each instruction retains its native labels/exception blocks.
     yield return new CodeInstruction(code[i+1]){opcode=OpCodes.Nop,operand=null};i++;
    }else yield return code[i];
   }
   if(matches!=1)throw new InvalidOperationException("Native harvest effect actor signature changed.");
  }
 }
}
