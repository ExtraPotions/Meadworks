// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Saga.Runtime {
    public static class BackstabCredit {
        const string ObserveId="meadworks.saga.backstab_observed",CreditId="meadworks.saga.backstab_credit";
        const int PendingLimit=32;
        static readonly BackstabReceipts receipts=new BackstabReceipts();
        static readonly Dictionary<string,Pending> pending=new Dictionary<string,Pending>(StringComparer.Ordinal);
        static readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        static IDisposable? operation,command;static long world,generation;static string epoch="";static bool diagnosed;static Player? actor;
        sealed class Pending {
            internal BackstabReceipt Receipt=null!;internal bool Delivery;internal string Peer="";internal long Generation;internal double Started,Next;internal int Attempts;
            internal CancellationTokenSource Cancel=new CancellationTokenSource();internal Task<OperationResult<bool>>? Task;
        }
        sealed class Codec:IOperationCodec<BackstabReceipt,bool>{public byte[] EncodeRequest(BackstabReceipt value)=>value.Encode();public BackstabReceipt DecodeRequest(byte[] bytes)=>BackstabReceipt.Decode(bytes);public byte[] EncodeReply(bool value)=>new[]{value?(byte)1:(byte)0};public bool DecodeReply(byte[] bytes)=>bytes.Length==1&&(bytes[0]==0||bytes[0]==1)?bytes[0]==1:throw new FormatException("Backstab reply.");}
        static SessionSnapshot Session=>CruciblePlugin.Services.Sessions.Current;
        static double Now=>clock.Elapsed.TotalSeconds;
        static bool Active=>Plugin.Ready&&NativeSaga.SneakEffectsActive;
        static void Diagnose(string reason){if(diagnosed)return;diagnosed=true;NativeSaga.Log.LogWarning("Backstab credit refused: "+reason+". Native damage is unchanged; credit qualification remains incomplete.");}
        public static void ProducerRejected(string reason)=>Diagnose("producer disabled: "+reason);
        public static void Initialize(ProductRegistration registration){
            operation=CruciblePlugin.Services.Operations.Register(registration,new OperationDescriptor(ObserveId,new System.Version(1,0)),new Codec(),Observe);
            try{command=CruciblePlugin.Services.PeerCommands.Register(registration,new OperationDescriptor(CreditId,new System.Version(1,0)),new Codec(),Receive);}catch{operation.Dispose();operation=null;throw;}
        }
        static ZDOID Victim(BackstabReceipt r)=>new ZDOID(r.VictimUser,r.VictimId);
        static ZDOID Attacker(BackstabReceipt r)=>new ZDOID(r.AttackerUser,r.AttackerId);
        static string Peer(long id)=>id.ToString(CultureInfo.InvariantCulture);
        static bool MatchingSession(BackstabReceipt r)=>Session.AuthorityReady&&r.World==Session.WorldId&&r.Epoch==Session.SessionId;
        static bool AttackerPeer(BackstabReceipt r,out string peerId){
            peerId="";var net=ZNet.instance;var zdo=ZDOMan.instance?.GetZDO(Attacker(r));if(net==null||zdo==null||!zdo.IsValid()||zdo.GetLong(ZDOVars.s_playerID)!=r.Player)return false;
            var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(zdo.GetPrefab()):null;if(!prefab||!prefab.GetComponent<Player>())return false;
            var local=Player.m_localPlayer;if(local!=null&&local.m_nview!=null&&local.m_nview.IsValid()&&local.m_nview.IsOwner()&&local.GetZDOID()==Attacker(r)&&local.GetPlayerID()==r.Player&&zdo.GetOwner()==ZNet.GetUID()){peerId=Peer(ZNet.GetUID());return true;}
            foreach(var peer in net.GetPeers())if(peer.IsReady()&&peer.m_rpc.IsConnected()&&peer.m_characterID==Attacker(r)&&peer.m_uid==zdo.GetOwner()&&(peer.m_playerID==0||peer.m_playerID==r.Player)){peerId=Peer(peer.m_uid);return true;}return false;
        }
        static bool LiveSource(BackstabReceipt r,long sender){var zdo=ZDOMan.instance?.GetZDO(Victim(r));if(zdo==null||!zdo.IsValid()||zdo.GetOwner()!=sender||sender!=r.Owner||zdo.GetPrefab()!=r.Prefab)return false;var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(r.Prefab):null;return prefab&&prefab.GetComponent<Character>()&&prefab.GetComponent<BaseAI>();}
        static bool QueueDelivery(BackstabReceipt r,string peer){
            if(pending.TryGetValue(r.Event,out var prior))return prior.Delivery&&prior.Receipt.Same(r)&&prior.Peer==peer;
            if(pending.Count>=PendingLimit)return false;
            pending.Add(r.Event,new Pending{Receipt=r,Delivery=true,Peer=peer,Generation=Session.Generation,Started=Now});return true;
        }
        static bool Admit(BackstabReceipt r,long sender){
            if(!Active||!Session.IsAuthority||!MatchingSession(r)||sender!=r.Owner||!AttackerPeer(r,out var peer))return false;
            bool retained=receipts.Contains(r);bool source=retained||LiveSource(r,sender);
            bool room=pending.ContainsKey(r.Event)||pending.Count<PendingLimit;
            var result=receipts.Admit(r,Session.WorldId,Session.SessionId,source&&room,Now);
            if(result!=BackstabAdmission.Accepted&&result!=BackstabAdmission.Duplicate){Diagnose("authority "+result);return false;}
            return QueueDelivery(r,peer);
        }
        static OperationExecution<bool> Observe(AuthorityContext context,BackstabReceipt receipt){
            Refresh();if(context.IsLocal||context.Session.WorldId!=receipt.World||context.Session.SessionId!=receipt.Epoch||!long.TryParse(context.SenderId,NumberStyles.Integer,CultureInfo.InvariantCulture,out var sender)||!Admit(receipt,sender))return OperationExecution<bool>.Rejected("BackstabObservationRejected");
            context.MarkDecisionMade();return OperationExecution<bool>.Success(true);
        }
        static OperationExecution<bool> Receive(PeerCommandContext context,BackstabReceipt receipt){
            Refresh();var player=Player.m_localPlayer;
            if(!Active||!MatchingSession(receipt)||!context.ServerConnectionVerified||context.LocalPeerId!=Peer(ZNet.GetUID())||context.Session.WorldId!=receipt.World||context.Session.SessionId!=receipt.Epoch||!NativeDodging.Owned(player)||player.GetPlayerID()!=receipt.Player||player.GetZDOID()!=Attacker(receipt))return OperationExecution<bool>.Rejected("BackstabCreditRejected");
            var result=receipts.Admit(receipt,Session.WorldId,Session.SessionId,true,Now,true);
            if(result==BackstabAdmission.Duplicate)return OperationExecution<bool>.Success(true);
            if(result!=BackstabAdmission.Accepted){Diagnose("receiver "+result);return OperationExecution<bool>.Rejected("BackstabCredit"+result);}
            context.MarkDecisionMade();try{float amount=NativeSaga.BackstabExperience;if(amount>0)player.RaiseSkill(global::Skills.SkillType.Sneak,amount);return OperationExecution<bool>.Success(true);}catch(Exception error){Diagnose("consumer "+error.GetType().Name);return OperationExecution<bool>.RecoveryRequired();}
        }
        // Called only from the validated native successful branch. Never throw into or defer native damage/death.
        public static void ObserveSuccessfulBackstab(Character victim,Character attacker){try{
            if(!Active||!Session.AuthorityReady||!(attacker is Player player)||victim==null||player.GetPlayerID()==0)return;
            var source=victim.m_nview;var target=player.m_nview;if(source==null||!source.IsValid()||!source.IsOwner()||target==null||!target.IsValid())return;
            Refresh();var data=source.GetZDO();var v=data.m_uid;var a=target.GetZDO().m_uid;
            var receipt=new BackstabReceipt(Session.WorldId,Session.SessionId,v.UserID,v.ID,data.GetPrefab(),data.GetOwner(),a.UserID,a.ID,player.GetPlayerID(),Guid.NewGuid().ToString("N"));
            if(Session.IsAuthority){if(data.GetOwner()!=ZNet.GetUID()||!Admit(receipt,ZNet.GetUID()))Diagnose("local success admission");return;}
            if(pending.Count>=PendingLimit){Diagnose("pending capacity");return;}
            var item=new Pending{Receipt=receipt,Generation=Session.Generation,Started=Now};pending.Add(receipt.Event,item);Submit(item);
        }catch(Exception error){Diagnose("native success capture "+error.GetType().Name);}}
        static void Submit(Pending item){try{item.Attempts++;item.Next=Now+1;item.Task=item.Delivery?CruciblePlugin.PeerCommandClient.SendAsync<BackstabReceipt,bool>(item.Peer,CreditId,item.Receipt.Event,item.Receipt,item.Cancel.Token):CruciblePlugin.RequestClient.InvokeAsync<BackstabReceipt,bool>(ObserveId,item.Receipt,item.Cancel.Token);}catch(Exception error){Diagnose("transport submission "+error.GetType().Name);Remove(item.Receipt.Event);}}
        static void Remove(string id){if(!pending.TryGetValue(id,out var item))return;pending.Remove(id);item.Cancel.Cancel();item.Cancel.Dispose();if(item.Task!=null){var abandoned=item.Task;abandoned.ContinueWith(task=>{var ignored=task.Exception;},TaskContinuationOptions.OnlyOnFaulted);}}
        static void CancelAll(){foreach(var id in pending.Keys.ToArray())Remove(id);}
        static void Refresh(){var session=Session;if(world!=session.WorldId||epoch!=session.SessionId){CancelAll();world=session.WorldId;epoch=session.SessionId;receipts.Session(world,epoch);diagnosed=false;}if(generation!=session.Generation||!ReferenceEquals(actor,Player.m_localPlayer)){CancelAll();generation=session.Generation;actor=Player.m_localPlayer;}}
        public static void Tick(){Refresh();if(!Active||!Session.AuthorityReady){CancelAll();return;}
            foreach(var item in pending.Values.ToArray()){
                if(item.Generation!=Session.Generation||!MatchingSession(item.Receipt)){Remove(item.Receipt.Event);continue;}
                if(item.Task!=null&&item.Task.IsCompleted){var task=item.Task;item.Task=null;if(task.Status==TaskStatus.RanToCompletion&&task.Result.State==OperationState.Succeeded&&task.Result.Reply){Remove(item.Receipt.Event);continue;}if(task.IsFaulted){var ignored=task.Exception;}if(item.Attempts>=3||Now-item.Started>=30){Diagnose(item.Delivery?"delivery not acknowledged":"success admission not acknowledged");Remove(item.Receipt.Event);continue;}}
                if(Now-item.Started>=30){Diagnose("pending success timed out");Remove(item.Receipt.Event);continue;}
                // The authority validates immutable receipts; victim death or ownership changes do not cancel a submitted observation.
                if(item.Task==null&&Now>=item.Next)Submit(item);
            }
        }
        public static void Shutdown(){CancelAll();command?.Dispose();command=null;operation?.Dispose();operation=null;actor=null;}
    }
}
