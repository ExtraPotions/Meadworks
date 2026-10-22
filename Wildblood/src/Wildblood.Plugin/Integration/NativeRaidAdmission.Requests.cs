// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BepInEx;
using ExtraPotions.Wildblood.Adopted.Raids;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Raids;
using ExtraPotions.Wildblood.Transfers;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime {
    internal static partial class NativeRaidAdmission {
        const string RequestRpc="mw_wildblood_raid_request_v1",ReplyRpc="mw_wildblood_raid_reply_v1";
        sealed class PendingRequest {
            internal ZRoutedRpc Bus=null!;internal ZNet Network=null!;internal long Generation,World,Server;
            internal float Deadline;internal bool Stop;internal Action<string> Output=null!;
        }
        static readonly Dictionary<string,PendingRequest> requests=new Dictionary<string,PendingRequest>();
        static readonly Dictionary<long,float> requestTimes=new Dictionary<long,float>();
        sealed class DeferredRequest {
            internal ZRoutedRpc Bus=null!;internal long Sender,World,Generation;internal ZDOID Chest;internal bool Stop;
            internal string Request="",Root="",Items="";internal int Stake,Tier;internal uint Revision;internal float Deadline;
        }
        static readonly Dictionary<string,DeferredRequest> deferred=new Dictionary<string,DeferredRequest>();
        static Container? FindChest(ZDOID id)=>UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None).FirstOrDefault(c=>Available(c)&&c.GetComponent<ZNetView>().GetZDO().m_uid==id);
        static bool Available(Container chest)=>chest&&chest.GetInventory()!=null&&chest.GetComponent<ZNetView>()&&chest.GetComponent<ZNetView>().IsValid();
        static Player? PlayerFor(long sender){
            if(!ZNet.instance||sender==0)return null;
            if(sender==ZNet.GetUID())return Player.m_localPlayer;
            var peer=ZNet.instance.GetPeer(sender);
            if(peer==null||!peer.IsReady()||peer.m_rpc==null||!peer.m_rpc.IsConnected()||peer.m_characterID.IsNone())return null;
            return Player.GetAllPlayers().FirstOrDefault(p=>p&&p.m_nview&&p.m_nview.IsValid()
                &&p.m_nview.GetZDO().m_uid==peer.m_characterID&&p.m_nview.GetZDO().GetOwner()==sender
                &&p.GetPlayerID()!=0&&(peer.m_playerID==0||peer.m_playerID==p.GetPlayerID()));
        }
        static ZDO? CharacterFor(long sender){
            var local=PlayerFor(sender);if(local)return local.m_nview.GetZDO();
            var peer=ZNet.instance.GetPeer(sender);
            if(peer==null||!peer.IsReady()||peer.m_rpc==null||!peer.m_rpc.IsConnected()||peer.m_characterID.IsNone()||ZDOMan.instance==null)return null;
            var character=ZDOMan.instance.GetZDO(peer.m_characterID);
            if(character==null||character.GetOwner()!=sender||character.GetLong(ZDOVars.s_playerID,0)==0
                ||peer.m_playerID!=0&&peer.m_playerID!=character.GetLong(ZDOVars.s_playerID,0))return null;
            var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(character.GetPrefab()):null;
            return prefab&&prefab.GetComponent<Player>()?character:null;
        }
        static bool RequestNear(long sender,ZDO character,ZDO chest){
            float square=(character.GetPosition()-chest.GetPosition()).sqrMagnitude;
            if(float.IsNaN(square)||float.IsInfinity(square)||square>25f||character.GetBool(ZDOVars.s_dead))return false;
            var actor=PlayerFor(sender);if(actor&&(actor.IsDead()||actor.IsTeleporting()))return false;
            var loaded=FindChest(chest.m_uid);
            if(loaded)return loaded.CheckAccess(character.GetLong(ZDOVars.s_playerID,0));
            var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(chest.GetPrefab()):null;var container=prefab?prefab.GetComponent<Container>():null;
            return container&&(container.m_privacy==Container.PrivacySetting.Public
                ||container.m_privacy==Container.PrivacySetting.Private&&chest.GetLong(ZDOVars.s_creator,0)==character.GetLong(ZDOVars.s_playerID,0));
        }
        static bool Eligible(Container chest,Player asker,bool stopping=false){
            if(!Authority||!Available(chest)||!asker||asker.IsDead()||asker.IsTeleporting()||!asker.m_nview||!asker.m_nview.IsValid()
                ||PlayerFor(asker.m_nview.GetZDO().GetOwner())!=asker)return false;
            var delta=asker.transform.position-chest.transform.position;
            if(float.IsNaN(delta.sqrMagnitude)||float.IsInfinity(delta.sqrMagnitude)||delta.sqrMagnitude>25f
                ||!stopping&&(chest.IsInUse()||chest.GetComponent<ZNetView>().GetZDO().GetInt(ZDOVars.s_inUse)==1)
                ||!chest.CheckAccess(asker.GetPlayerID()))return false;
            if(chest.m_checkGuardStone){
                bool inside=false,allowed=false;long actor=asker.GetPlayerID();
                foreach(var area in PrivateArea.m_allAreas){
                    if(!area||!area.IsEnabled()||!area.IsInside(chest.transform.position,0f))continue;
                    inside=true;if(area.m_piece&&area.m_piece.GetCreator()==actor||area.IsPermitted(actor))allowed=true;
                }
                if(inside&&!allowed)return false;
            }
            return true;
        }
        static bool PendingAdmission(Container chest){
            string token=chest.GetComponent<ZNetView>().GetZDO().GetString(NativeRaidChest.TokenKey);
            if(string.IsNullOrEmpty(token))return false;
            if(!Canonical(token)||ZDOMan.instance==null||ZDOMan.instance.m_objectsByID.Values.Any(z=>z.m_uid!=chest.GetComponent<ZNetView>().GetZDO().m_uid&&z.GetString(NativeRaidChest.TokenKey)==token))return true;
            bool Unsettled(InventoryDecisionJournal intents,InventoryDecisionJournal proofs){
                if(proofs.Snapshot().Any(r=>r.State!=InventoryDecision.Committed))return true;
                var checkpoints=new RaidStakeCheckpoints(proofs,intents.Find);
                return intents.Snapshot().Any(r=>r.State!=InventoryDecision.Aborted
                    &&(r.State!=InventoryDecision.Committed||checkpoints.Latest(r.Id)?.Stage!=RaidStakeStage.Terminal));
            }
            if(owners.TryGetValue(chest,out var owner))return Unsettled(owner.Intents,owner.Checkpoints);
            string world=ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture);
            string folder=Path.Combine(Paths.BepInExRootPath,"Wildblood.raid-authority",world);
            string intent=Path.Combine(folder,token+".intent.wal"),proof=Path.Combine(folder,token+".proof.wal");
            if(!File.Exists(intent)&&!File.Exists(proof))return File.Exists(Path.Combine(Paths.BepInExRootPath,"Wildblood.raid-owner",world,token+".wal"));
            if(!File.Exists(intent)||!File.Exists(proof))return true;
            using(var intents=new InventoryDecisionJournal(intent,true))using(var proofs=new InventoryDecisionJournal(proof,true))return Unsettled(intents,proofs);
        }
        static string ItemsHash(ZDO zdo){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(zdo.GetByteArray(ZDOVars.s_items)??Array.Empty<byte>())).Replace("-","").ToLowerInvariant();}
        static bool LoadCurrent(Container chest){
            var zdo=chest.GetComponent<ZNetView>().GetZDO();uint revision=zdo.DataRevision;
            chest.Load();
            if(chest.m_lastRevision!=revision||zdo.DataRevision!=revision)return false;
            byte[] saved=zdo.GetByteArray(ZDOVars.s_items);
            if(saved==null)return true;
            var image=new ZPackage();chest.GetInventory().Save(image);
            return image.GetArray().SequenceEqual(saved);
        }
        static bool Acquire(Container chest,Player asker,uint expectedRevision,string? expectedItems){
            if(!Eligible(chest,asker))return false;
            var view=chest.GetComponent<ZNetView>();var zdo=view.GetZDO();
            try {
                if(Valid(chest)){
                    // Recovery and replay retain their exact held images; a new idle admission loads current native items first.
                    if(zdo.GetBool(NativeRaidChest.HoldKey)||RaidState.IsRunning(zdo)||PendingAdmission(chest))return true;
                    return LoadCurrent(chest)&&Eligible(chest,asker);
                }
                if(zdo.GetOwner()!=asker.m_nview.GetZDO().GetOwner()||expectedItems==null||expectedItems.Length!=64
                    ||zdo.DataRevision!=expectedRevision||ItemsHash(zdo)!=expectedItems
                    ||zdo.GetBool(NativeRaidChest.HoldKey)||RaidState.IsRunning(zdo)||PendingAdmission(chest))return false;
                uint revision=zdo.DataRevision;string root=zdo.GetString(RootKey),token=zdo.GetString(NativeRaidChest.TokenKey);
                view.ClaimOwnership();
                if(!Valid(chest)||zdo.DataRevision!=revision)return false;
                if(!LoadCurrent(chest))return false;
                // Load the replicated native items only after ownership changes, and check eligibility again before a journal can debit.
                return Valid(chest)&&Eligible(chest,asker)&&zdo.DataRevision==revision&&zdo.GetString(RootKey)==root
                    &&zdo.GetString(NativeRaidChest.TokenKey)==token&&ItemsHash(zdo)==expectedItems&&!zdo.GetBool(NativeRaidChest.HoldKey)&&!RaidState.IsRunning(zdo);
            }catch(Exception error){Plugin.NativeLogger?.LogWarning("Raid chest ownership refused: "+error.GetType().Name);return false;}
        }
        internal static string? Stop(Container chest,Player asker,string expectedRoot){
            if(!Eligible(chest,asker,true)||!Valid(chest)||!Canonical(expectedRoot))return "The chest is unavailable or access is denied.";
            var zdo=chest.GetComponent<ZNetView>().GetZDO();
            if(zdo.GetString(RootKey)!=expectedRoot)return "The raid at this chest has changed.";
            try {
                var owner=Open(chest);owner.Recover();var runner=chest.GetComponent<RaidRunner>();
                if(!runner||owner.VerifiedRoot!=expectedRoot&&owner.Proofs.Latest(expectedRoot)?.Stage!=RaidStakeStage.Terminal)return "Raid recovery must finish before stopping.";
                if(runner.State.Running)runner.End(RaidEnd.Stopped);
                owner.Recover();return null;
            }catch(Exception error){Plugin.NativeLogger?.LogError("Raid stop retained for recovery: "+error.GetType().Name+": "+error.Message);return "Raid stop requires recovery.";}
        }
        static bool RegisterRequests(){
            var bus=ZRoutedRpc.instance;
            return !SourcePresent&&bus!=null&&NativeRpcOwnership.Routed(bus,NativeRpcOwner.RaidAdmission,()=>{
                bus.Register<ZPackage>(RequestRpc,(sender,packet)=>OnRequest(bus,sender,packet));
                bus.Register<ZPackage>(ReplyRpc,(sender,packet)=>OnReply(bus,sender,packet));
            },()=>{requests.Clear();requestTimes.Clear();CloseScenes();},RequestRpc,ReplyRpc);
        }
        static void Submit(Container chest,Player asker,bool stop,int stake,string request,int tier,Action<string> output){
            if(Authority){string? error=stop?Stop(chest,asker,chest.GetComponent<ZNetView>().GetZDO().GetString(RootKey)):Start(chest,asker,stake,request,tier);ShowResult(stop,request,error==null,output);if(error!=null)Plugin.NativeLogger?.LogWarning("Raid request refused: "+error);return;}
            if(!Plugin.Running||!CruciblePlugin.IsReady||!CruciblePlugin.Session.AuthorityReady||!ZNet.instance||!RegisterRequests()
                ||requests.Count>=16||requests.ContainsKey(request)||!Canonical(request)){output(WildbloodText.Text("raid_request_unavailable"));return;}
            var server=ZNet.instance.GetServerPeer();if(server==null){output(WildbloodText.Text("raid_request_unavailable"));return;}
            var packet=new ZPackage();packet.Write(1);packet.Write(ZNet.instance.GetWorldUID());packet.Write(chest.GetComponent<ZNetView>().GetZDO().m_uid);
            packet.Write(stop);packet.Write(request);packet.Write(stake);packet.Write(tier);packet.Write(stop?chest.GetComponent<ZNetView>().GetZDO().GetString(RootKey):"");
            var chestState=chest.GetComponent<ZNetView>().GetZDO();packet.Write((long)chestState.DataRevision);packet.Write(ItemsHash(chestState));
            requests.Add(request,new PendingRequest{Bus=ZRoutedRpc.instance,Network=ZNet.instance,World=ZNet.instance.GetWorldUID(),Generation=CruciblePlugin.Session.Generation,Server=server.m_uid,Stop=stop,Deadline=Time.unscaledTime+20,Output=output});
            ZRoutedRpc.instance.InvokeRoutedRPC(server.m_uid,RequestRpc,packet);output(WildbloodText.Format("raid_request_sent",request));
        }
        static void OnRequest(ZRoutedRpc bus,long sender,ZPackage packet){
            if(!Authority||SourcePresent||!ReferenceEquals(bus,ZRoutedRpc.instance)||!NativeRpcOwnership.OwnsRouted(bus,NativeRpcOwner.RaidAdmission))return;
            try {
                if(packet==null||packet.Size()>512||packet.ReadInt()!=1||packet.ReadLong()!=ZNet.instance.GetWorldUID())return;
                var chestId=packet.ReadZDOID();bool stop=packet.ReadBool();string request=packet.ReadString();int stake=packet.ReadInt(),tier=packet.ReadInt();string root=packet.ReadString();
                long revision=packet.ReadLong();string items=packet.ReadString();
                if(packet.GetPos()!=packet.Size()||!Canonical(request)||tier < -1||tier>RaidTable.MaxTier||(!stop&&(stake<1||stake>RaidStakePlan.MaxStake||root!=""))
                    ||stop&&(stake!=0||tier!=-1||!Canonical(root))||revision<0||revision>uint.MaxValue||items.Length!=64||items.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return;
                var character=CharacterFor(sender);var chestState=ZDOMan.instance?.GetZDO(chestId);
                if(character==null||chestState==null)return;
                if(requestTimes.TryGetValue(sender,out float last)&&Time.unscaledTime-last<.25f)return;
                if(requestTimes.Count>=128&&!requestTimes.ContainsKey(sender))return;
                requestTimes[sender]=Time.unscaledTime;
                var call=new DeferredRequest{Bus=bus,Sender=sender,World=ZNet.instance.GetWorldUID(),Generation=CruciblePlugin.Session.Generation,
                    Chest=chestId,Stop=stop,Request=request,Root=root,Stake=stake,Tier=tier,Revision=(uint)revision,Items=items,Deadline=Time.unscaledTime+20};
                if(!RequestNear(sender,character,chestState)){Reply(call,"The requesting player is out of range or chest access is denied.");return;}
                if(!stop&&(!Enabled||!Plugin.NativeRulesReady)){Reply(call,"Raid rules are not ready.");return;}
                bool ready=LeaseScene(chestState);
                var actor=PlayerFor(sender);var chest=FindChest(chestId);
                if(ready&&actor&&chest){Execute(call,chest,actor);return;}
                string key=sender.ToString(CultureInfo.InvariantCulture)+":"+request;
                if(deferred.TryGetValue(key,out var old)){
                    if(old.Chest!=call.Chest||old.Stop!=call.Stop||old.Stake!=call.Stake||old.Tier!=call.Tier||old.Root!=call.Root)Reply(call,"The request conflicts with a queued admission.");
                    return;
                }
                if(deferred.Count>=16){Reply(call,"Raid area loading capacity reached.");return;}
                deferred.Add(key,call);
            }catch(Exception error){Plugin.NativeLogger?.LogWarning("Raid request packet refused: "+error.GetType().Name);}
        }
        static void Reply(DeferredRequest call,string? error){
            var reply=new ZPackage();reply.Write(1);reply.Write(call.World);reply.Write(call.Request);reply.Write(error==null);
            call.Bus.InvokeRoutedRPC(call.Sender,ReplyRpc,reply);if(error!=null)Plugin.NativeLogger?.LogWarning("Raid request "+call.Request+" refused: "+error);
        }
        static void Execute(DeferredRequest call,Container chest,Player actor)=>Reply(call,call.Stop?Stop(chest,actor,call.Root):Start(chest,actor,call.Stake,call.Request,call.Tier,call.Revision,call.Items));
        static void DeferredTick(){
            foreach(var pair in deferred.ToArray()){
                var call=pair.Value;
                if(!Authority||!ReferenceEquals(call.Bus,ZRoutedRpc.instance)||call.World!=CruciblePlugin.Session.WorldId||call.Generation!=CruciblePlugin.Session.Generation){deferred.Remove(pair.Key);continue;}
                var character=CharacterFor(call.Sender);var state=ZDOMan.instance?.GetZDO(call.Chest);
                if(character==null||state==null||!RequestNear(call.Sender,character,state)||Time.unscaledTime>call.Deadline){deferred.Remove(pair.Key);Reply(call,"The requesting player or raid area is no longer ready.");continue;}
                var actor=PlayerFor(call.Sender);var chest=FindChest(call.Chest);
                if(!actor||!chest||ZNet.instance.IsDedicated()&&(!sceneLeases.TryGetValue(call.Chest,out var lease)||!lease.Ready))continue;
                deferred.Remove(pair.Key);Execute(call,chest,actor);
            }
        }
        static void OnReply(ZRoutedRpc bus,long sender,ZPackage packet){
            if(!ReferenceEquals(bus,ZRoutedRpc.instance)||!NativeRpcOwnership.OwnsRouted(bus,NativeRpcOwner.RaidAdmission))return;
            try {
                if(packet==null||packet.Size()>128||packet.ReadInt()!=1)return;
                long world=packet.ReadLong();string request=packet.ReadString();bool accepted=packet.ReadBool();
                if(packet.GetPos()!=packet.Size()||!requests.TryGetValue(request,out var pending)||sender!=pending.Server
                    ||!ReferenceEquals(pending.Bus,bus)||!ReferenceEquals(pending.Network,ZNet.instance)||world!=pending.World
                    ||world!=CruciblePlugin.Session.WorldId||pending.Generation!=CruciblePlugin.Session.Generation)return;
                requests.Remove(request);ShowResult(pending.Stop,request,accepted,pending.Output);
            }catch(Exception error){Plugin.NativeLogger?.LogWarning("Raid reply packet refused: "+error.GetType().Name);}
        }
        static void ShowResult(bool stop,string request,bool accepted,Action<string> output)=>output(WildbloodText.Format(accepted?(stop?"raid_request_stopped":"raid_request_started"):"raid_request_refused",request));
        static void RequestTick(){
            RegisterRequests();
            SceneTick();DeferredTick();
            foreach(var peer in requestTimes.Where(p=>Time.unscaledTime-p.Value>60).Select(p=>p.Key).ToArray())requestTimes.Remove(peer);
            foreach(var request in requests.ToArray()){
                var pending=request.Value;
                if(!ReferenceEquals(pending.Bus,ZRoutedRpc.instance)||!ReferenceEquals(pending.Network,ZNet.instance)||pending.Generation!=CruciblePlugin.Session.Generation){requests.Remove(request.Key);continue;}
                if(Time.unscaledTime<pending.Deadline)continue;
                requests.Remove(request.Key);pending.Output(WildbloodText.Format("raid_request_timeout",request.Key));
            }
        }
        static void CloseRequests(){requests.Clear();requestTimes.Clear();CloseScenes();NativeRpcOwnership.Release(NativeRpcOwner.RaidAdmission);}
    }
}
