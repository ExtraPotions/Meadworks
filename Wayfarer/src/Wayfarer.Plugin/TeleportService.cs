using UnityEngine;
using HarmonyLib;
using Wayfarer.Core;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
namespace Wayfarer {
 internal sealed class TeleportService {
  readonly Plugin p;internal TeleportService(Plugin plugin){p=plugin;}
  Vector3? pending;Vector3? recovery;long pendingWorld;float expires;long generation=-1;
  Task<OperationResult<TeleportGrant>>? authorization;CancellationTokenSource? cancellation;PendingTeleport? lifecycle;TeleportRequest? request;
  readonly TeleportGrantLedger grants=new TeleportGrantLedger();
  internal bool AwaitingAuthorization=>authorization!=null;
  internal void ToMap(Vector3 position){position.y=ZoneSystem.c_WaterLevel;Start(position,Category.Map,"map",false);}
  internal void TryTeleport(string id) {
   if(!p.CanTeleport){p.Report(p.TeleportRefusal());return;}
   if(!p.Catalog.TryResolve(id,out var d,out var error)||d==null){p.Report(error);return;}
   var target=new Vector3(d.X,d.Y,d.Z);
   if(d.Category==Category.Map||d.Category==Category.Stones)target.y=ZoneSystem.c_WaterLevel;
   Start(target,d.Category,d.Id,d.IsLastKnown);
  }
  TeleportActorFacts? Actor(){var player=Player.m_localPlayer;var session=p.Integration.Session;if(!player||p.World==0||!session.AuthorityReady||session.WorldId!=p.World)return null;var view=player.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner())return null;var cooldown=(float)(AccessTools.Field(typeof(Player),"m_teleportCooldown")?.GetValue(player)??0f);return new TeleportActorFacts(player.GetPlayerID().ToString(CultureInfo.InvariantCulture),p.World,session.SessionId,session.IsAdmin,true,!player.IsDead(),player.IsTeleporting(),cooldown>=2);}
  void Start(Vector3 target,Category category,string destination,bool lastKnown) {
   if(!p.CanUse(category)){p.Refuse(category);return;}
   if(authorization!=null||pending.HasValue||recovery.HasValue){p.Report("busy");return;}
   if(!LandingPolicy.IsValid(target.x,target.y,target.z)){p.Report("unsafe");return;}
   var player=Player.m_localPlayer;if(!player){p.Report("unavailable");return;}
   if(player.IsDead()){p.Report("dead");return;}if(player.IsTeleporting()){p.Report("busy");return;}
   var actor=Actor();if(actor==null){p.Report("unavailable");return;}if(!actor.CooldownReady){p.Report("rejected");return;}
   var session=p.Integration.Session;generation=session.Generation;var departure=player.transform.position;
   request=new TeleportRequest(System.Guid.NewGuid().ToString("N"),category,destination,target.x,target.y,target.z,lastKnown);
   lifecycle=new PendingTeleport(actor,request,generation,p.Integration.MonotonicTime,departure.x,departure.y,departure.z);
   cancellation=new CancellationTokenSource();authorization=p.Integration.AuthorizeAsync(request,cancellation.Token);p.Report("authorizing");
  }
  void CancelAuthorization(){cancellation?.Cancel();lifecycle?.Cancel();authorization=null;cancellation?.Dispose();cancellation=null;request=null;}
  internal void Dispose(){CancelAuthorization();pending=null;recovery=null;grants.Reset();}
  void Acknowledge(TeleportGrant grant,bool accepted)=>p.Integration.CompleteAsync(new TeleportCompletion(grant.RequestId,grant.GrantId,accepted),CancellationToken.None);
  void PumpAuthorization(){
   var session=p.Integration.Session;
   if(generation!=session.Generation){CancelAuthorization();pending=null;recovery=null;lifecycle=null;grants.Reset();generation=session.Generation;return;}
   if(authorization==null)return;
   if(!p.CanTeleport||request!=null&&!p.CanUse(request.Category)){var refusal=request!=null?p.Refusal(request.Category):p.TeleportRefusal();CancelAuthorization();p.Report(refusal);return;}
   if(!authorization.IsCompleted)return;
   var task=authorization;authorization=null;cancellation?.Dispose();cancellation=null;
   if(task.IsFaulted||task.IsCanceled){lifecycle?.Cancel();p.Report("authorityunavailable");return;}
   var result=task.Result;if(result.State!=OperationState.Succeeded||result.Reply==null){lifecycle?.Cancel();p.Report(TeleportRefusals.Is(result.Code)||result.Code=="dead"||result.Code=="busy"?result.Code:"authorityunavailable");return;}
   var grant=result.Reply;var actor=Actor();var player=Player.m_localPlayer;
   if(actor==null||request==null||lifecycle==null||!player){Acknowledge(grant,false);return;}
   // Revalidate local sharing/ownership/profile availability without substituting
   // new coordinates for the authority-approved destination.
   if(grant.Category!=Category.Map&&!p.Catalog.TryResolve(grant.DestinationId,out _,out _)){lifecycle.Cancel();Acknowledge(grant,false);p.Report("unavailable");return;}
   var target=new Vector3(grant.X,grant.Y,grant.Z);
   if((grant.Category==Category.Boat||grant.Category==Category.Player)&&!LandingResolver.TryResolve(target,out target)){lifecycle.Cancel();Acknowledge(grant,false);p.Report("unsafe");return;}
   if(!grants.TrackVerifiedGrant(grant)||!grants.TryConsume(grant,actor,p.Integration.MonotonicTime)||!lifecycle.TryAccept(grant,actor,session.Generation,p.Integration.MonotonicTime,p.CanUse(grant.Category))) {Acknowledge(grant,false);p.Report("rejected");return;}
   var accepted=player.TeleportTo(target,player.transform.rotation,true);Acknowledge(grant,accepted);
   if(!accepted){lifecycle.Cancel();p.Report("rejected");return;}
   pending=target;pendingWorld=p.World;expires=Time.unscaledTime+30;
   p.State.Close();p.Report("success");if(p.Options.CloseMap.Value&&Minimap.instance)Minimap.instance.SetMapMode(Minimap.MapMode.Small);
  }
  internal void UpdateLanding() {
   PumpAuthorization();
   if(recovery.HasValue) {
    var recovering=Player.m_localPlayer;
    if(!recovering||p.World!=pendingWorld||Time.unscaledTime>expires){recovery=null;return;}
    var actor=Actor();if(actor!=null&&actor.Alive&&!actor.Teleporting&&actor.CooldownReady&&lifecycle!=null&&lifecycle.TryRecovery(actor,p.Integration.Session.Generation,p.Integration.MonotonicTime,out var departure)){
     recovering.TeleportTo(new Vector3(departure.X,departure.Y,departure.Z),recovering.transform.rotation,true);recovery=null;p.Report("unsafe");
    }
    return;
   }
   if(!pending.HasValue)return;
   var player=Player.m_localPlayer;
   if(!player||p.World!=pendingWorld||Time.unscaledTime>expires){pending=null;return;}
   if(player.IsTeleporting())return;
   var target=pending.Value;pending=null;
   if(player.IsDead()||Vector2.Distance(new Vector2(target.x,target.z),new Vector2(player.transform.position.x,player.transform.position.z))>2)return;
   if(!ZNetScene.instance||!ZNetScene.instance.IsAreaReady(target))return;
   var mask=LayerMask.GetMask("Default","static_solid","Default_small","piece","terrain");
   // Sample just above the requested spawn first, retaining indoor floor selection.
   if(!Physics.Raycast(target+Vector3.up*2,Vector3.down,out var hit,2000,mask,QueryTriggerInteraction.Ignore)) {
    if(!Physics.Raycast(target+Vector3.up*1000,Vector3.down,out hit,2000,mask,QueryTriggerInteraction.Ignore)){Recover();return;}
   }
   target.y=LandingPolicy.CorrectHeight(target.y,hit.point.y,ZoneSystem.c_WaterLevel);
   if(!LandingResolver.Clear(target,hit.normal.y)) {
    if(!LandingResolver.TryResolve(target,out target)){Recover();return;}
   }
   player.transform.position=target;
   var body=player.GetComponent<Rigidbody>();if(body){body.position=target;body.linearVelocity=Vector3.zero;}
   AccessTools.Field(typeof(Character),"m_maxAirAltitude")?.SetValue(player,target.y);
  }
  void Recover(){recovery=Vector3.zero;expires=Time.unscaledTime+30;p.Report("unsafe");}
 }
 internal static class LandingResolver {
  internal static bool Clear(Vector3 pos,float normalY) {
   var player=Player.m_localPlayer;var collider=player?player.GetComponent<CapsuleCollider>():null;
   var scale=player?player.transform.lossyScale:Vector3.one;
   var radius=collider?collider.radius*Mathf.Max(scale.x,scale.z):.4f;
   var height=collider?Mathf.Max(collider.height*scale.y,radius*2):1.8f;
   var center=pos+(collider?Vector3.Scale(collider.center,scale):Vector3.up*.9f);
   var offset=Vector3.up*(height*.5f-radius);
   var mask=LayerMask.GetMask("Default","static_solid","Default_small","piece","terrain","character","character_net","character_ghost");
   var blocked=false;
   foreach(var hit in Physics.OverlapCapsule(center-offset,center+offset,radius,mask,QueryTriggerInteraction.Ignore))
    if(!player||!hit.transform.IsChildOf(player.transform)){blocked=true;break;}
   return LandingPolicy.Clear(ZNetScene.instance&&ZNetScene.instance.IsAreaReady(pos),normalY,!blocked,pos.y<=ZoneSystem.c_WaterLevel+.61f);
  }
  internal static bool TryResolve(Vector3 target,out Vector3 landing) {
   landing=target;if(!ZNetScene.instance||!ZNetScene.instance.IsAreaReady(target))return false;
   var solid=LayerMask.GetMask("Default","static_solid","Default_small","piece","terrain");
   foreach(var radius in new[]{5f,8f})for(var i=0;i<8;i++) {
    var angle=i*Mathf.PI/4;var probe=target+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
    if(!Physics.Raycast(probe+Vector3.up*80,Vector3.down,out var hit,180,solid,QueryTriggerInteraction.Ignore))continue;
    if(hit.collider.GetComponentInParent<Ship>())continue;
    probe.y=Mathf.Max(hit.point.y,ZoneSystem.c_WaterLevel)+0.6f;
    if(!Clear(probe,hit.normal.y))continue;
    landing=probe;return true;
   }
   return false;
  }
 }
}
