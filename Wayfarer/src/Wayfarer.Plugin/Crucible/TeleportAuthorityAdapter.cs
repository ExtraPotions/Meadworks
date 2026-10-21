using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using Wayfarer.Core;
using CorePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace Wayfarer.CrucibleIntegration {
    internal sealed class TeleportAuthorityAdapter:IDisposable {
        readonly Func<TeleportPolicy> policy;readonly TeleportGrantLedger ledger=new TeleportGrantLedger();readonly Dictionary<string,double> nextAuthorization=new Dictionary<string,double>();readonly IDisposable authorization,completion;long generation=-1;
        internal TeleportAuthorityAdapter(ProductRegistration owner,Func<TeleportPolicy> policy){
            this.policy=policy;
            authorization=CorePlugin.Services.Operations.Register(owner,new OperationDescriptor(SessionIntegration.Authorize,new System.Version(1,0)),new TeleportCodec(),Authorize);
            try{completion=CorePlugin.Services.Operations.Register(owner,new OperationDescriptor(SessionIntegration.Complete,new System.Version(1,0)),new CompletionCodec(),Complete);}catch{authorization.Dispose();throw;}
        }
        void Reset(){if(generation==CorePlugin.Session.Generation)return;generation=CorePlugin.Session.Generation;ledger.Reset();nextAuthorization.Clear();}
        TeleportActorFacts? Actor(AuthorityContext context){
            if(!long.TryParse(context.ActorId,NumberStyles.Integer,CultureInfo.InvariantCulture,out var actorId))return null;
            var net=ZNet.instance;if(net==null)return null;var player=Player.GetPlayer(actorId);ZDO? zdo=null;
            bool travel;
            if(context.IsLocal){travel=MeadworksMischief.Decide(MeadworksIntegration.Metadata().Id,MischiefCategory.Travel).Allowed;if(player==null||player!=Player.m_localPlayer)return null;var view=player.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner())return null;zdo=view.GetZDO();}
            else {var peer=net.GetPeers().FirstOrDefault(p=>p.m_uid.ToString(CultureInfo.InvariantCulture)==context.SenderId&&p.IsReady()&&p.m_rpc.IsConnected());if(peer==null)return null;zdo=ZDOMan.instance?.GetZDO(peer.m_characterID);if(zdo==null||zdo.GetOwner()!=peer.m_uid||zdo.GetLong(ZDOVars.s_playerID,0)!=actorId)return null;travel=MeadworksMischief.DecideForPeer(peer.m_rpc,MischiefCategory.Travel).Allowed;} // Destinations the admin lets players use are in the policy's PlayerCategories.
            if(zdo==null)return null;
            bool alive=context.IsLocal&&player!=null?!player.IsDead()&&player.GetHealth()>0:
                !zdo.GetBool(ZDOVars.s_dead,false)&&zdo.GetFloat(ZDOVars.s_health,zdo.GetFloat(ZDOVars.s_maxHealth,0))>0;
            // The remote owner maintains native teleport cooldown/animation. The
            // authority also imposes its own two-second authorization cooldown;
            // the owning client must recheck native cooldown before movement.
            bool cooldown=!nextAuthorization.TryGetValue(context.ActorId,out var until)||SessionIntegration.Now>=until;
            if(context.IsLocal)cooldown&=(float)(typeof(Player).GetField("m_teleportCooldown",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(player)??0f)>=2f;
            return new TeleportActorFacts(context.ActorId,context.Session.WorldId,context.Session.SessionId,travel,true,alive,context.IsLocal&&player!=null&&player.IsTeleporting(),cooldown);
        }
        OperationExecution<TeleportGrant> Authorize(AuthorityContext context,TeleportRequest request){
            Reset();var actor=Actor(context);if(actor==null)return OperationExecution<TeleportGrant>.Rejected("unavailable");
            var activePolicy=policy();var denied=TeleportAuthorization.Evaluate(activePolicy,actor,request,new TeleportTargetFacts(false,0,0,0));
            if(TeleportRefusals.Is(denied.Code)||denied.Code=="dead"||denied.Code=="busy")return OperationExecution<TeleportGrant>.Rejected(denied.Code);
            var target=Target(context,request);var decision=TeleportAuthorization.Evaluate(activePolicy,actor,request,target);
            if(!decision.Allowed)return OperationExecution<TeleportGrant>.Rejected(decision.Code);
            var grant=ledger.Issue(actor,request,decision,SessionIntegration.Now);if(grant==null)return OperationExecution<TeleportGrant>.Rejected("busy");
            nextAuthorization[actor.ActorId]=SessionIntegration.Now+2;return OperationExecution<TeleportGrant>.Success(grant);
        }
        OperationExecution<bool> Complete(AuthorityContext context,TeleportCompletion request){Reset();var actor=new TeleportActorFacts(context.ActorId,context.Session.WorldId,context.Session.SessionId,false,true,true,false,true);return OperationExecution<bool>.Success(ledger.Complete(actor,request.RequestId,request.GrantId));}
        static TeleportTargetFacts Facts(Vector3 position)=>new TeleportTargetFacts(LandingPolicy.IsValid(position.x,position.y,position.z),position.x,position.y,position.z);
        static TeleportTargetFacts Invalid()=>new TeleportTargetFacts(false,0,0,0);
        static TeleportTargetFacts Target(AuthorityContext context,TeleportRequest request){
            var id=request.DestinationId;var claimed=new Vector3(request.X,request.Y,request.Z);
            if(request.Category==Category.Map)return id=="map"?Facts(claimed):Invalid();
            // Beds and last-death positions are client-owned profile claims. This
            // validates policy/bounds, not ownership proof of a server-side bed.
            if(request.Category==Category.Bed)return id=="bed"?Facts(claimed):Invalid();
            if(request.Category==Category.Grave&&id=="death")return Facts(claimed);
            if(request.Category==Category.Stones){if(id!="stones"||!ZoneSystem.instance)return Invalid();var location=(string?)typeof(Game).GetField("m_StartLocation",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(Game.instance)??"StartTemple";return ZoneSystem.instance.GetLocationIcon(location,out var position)?Facts(new Vector3(position.x,ZoneSystem.c_WaterLevel,position.z)):Invalid();}
            if(request.Category==Category.Player){var net=ZNet.instance;if(net==null)return Invalid();var peer=net.GetPeers().FirstOrDefault(p=>"player:"+p.m_characterID==id&&p.IsReady()&&p.m_rpc.IsConnected()&&p.m_uid.ToString(CultureInfo.InvariantCulture)!=context.SenderId);if(peer!=null)return peer.m_publicRefPos?Facts(peer.m_refPos):Invalid();var host=net.GetPlayerList().FirstOrDefault(p=>"player:"+p.m_characterID==id&&p.m_publicPosition);if(host.m_publicPosition&&Player.m_localPlayer&&host.m_characterID==Player.m_localPlayer.GetComponent<ZNetView>()?.GetZDO()?.m_uid&&NativeActorId()!=context.ActorId)return Facts(Player.m_localPlayer.transform.position);return Invalid();}
            if(request.Category==Category.Grave&&id.StartsWith("grave:",StringComparison.Ordinal)){
                var record=DestinationRecord(id,"grave:");var prefab=record==null?null:ZNetScene.instance?.GetPrefab(record.GetPrefab());
                if(prefab&&prefab.GetComponent<TombStone>()&&record!.GetLong(ZDOVars.s_owner,0).ToString(CultureInfo.InvariantCulture)==context.ActorId)return Facts(record.GetPosition());
            }
            if(request.Category==Category.Boat&&id.StartsWith("boat:",StringComparison.Ordinal)){
                var record=DestinationRecord(id,"boat:");var prefab=record==null?null:ZNetScene.instance?.GetPrefab(record.GetPrefab());
                if(prefab&&prefab.GetComponent<Ship>())return Facts(record!.GetPosition());
                if(record==null&&request.IsLastKnown)return Facts(claimed);
            }
            return Invalid();
        }
        // Dedicated authorities retain native world records without constructing
        // the clients' loaded Ship/TombStone objects. Resolve the requested record
        // directly; the owning client still validates a loaded landing before use.
        static ZDO? DestinationRecord(string id,string prefix){
            var parts=id.Substring(prefix.Length).Split(':');
            if(parts.Length!=2||!long.TryParse(parts[0],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var user)||user==0||!uint.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out var objectId)||objectId==0||ZDOMan.instance==null)return null;
            // ZDOID's constructor adds unknown users to a native static cache.
            // Untrusted requests may only refer to an already replicated creator.
            var users=typeof(ZDOID).GetField("m_userIDs",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null) as List<long>;
            if(users==null||!users.Contains(user))return null;
            return ZDOMan.instance.GetZDO(new ZDOID(user,objectId));
        }
        static string NativeActorId()=>Player.m_localPlayer.GetPlayerID().ToString(CultureInfo.InvariantCulture);
        public void Dispose(){completion.Dispose();authorization.Dispose();ledger.Reset();nextAuthorization.Clear();}
    }
}
