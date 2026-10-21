// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using ExtraPotions.Wildblood.Adopted.Raids;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Raids;
using ExtraPotions.Wildblood.Transfers;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime {
    // One local authority owns admission and its journals; remote-owned chests are deliberately unavailable.
    internal static class NativeRaidAdmission {
        internal const string RootKey="wildblood.raid.event-root",HashKey="wildblood.raid.plan-hash";
        static readonly Dictionary<Container,Owner> owners=new Dictionary<Container,Owner>();
        static readonly HashSet<Container> loaded=new HashSet<Container>();
        static readonly HashSet<Container> refused=new HashSet<Container>();
        static Terminal.ConsoleCommand? command;
        static float next;
        static long generation=-1;
        static bool Authority=>Plugin.Running&&CruciblePlugin.IsReady&&CruciblePlugin.Session.IsAuthority&&CruciblePlugin.Session.AuthorityReady
            &&ZNet.instance&&ZNet.instance.IsServer()&&CruciblePlugin.Session.WorldId==ZNet.instance.GetWorldUID();
        internal static bool SourcePresent=>Chainloader.PluginInfos.ContainsKey(ExtraPotions.Wildblood.Integration.ModuleStepAside.EliteCreaturesReborn.Guid);
        static bool Enabled=>WildbloodModules.ProductActive(out _)&&Plugin.Settings?.Get(ExtraPotions.Wildblood.Config.WildbloodSettings.General.Enabled)==true
            &&!SourcePresent;
        static bool Valid(Container chest)=>chest&&chest.GetInventory()!=null&&chest.GetComponent<ZNetView>()
            &&chest.GetComponent<ZNetView>().IsValid()&&chest.GetComponent<ZNetView>().IsOwner();

        internal static void Attach(Container chest){
            if(SourcePresent||!chest||!chest.GetComponent<ZNetView>()||!chest.GetComponent<ZNetView>().IsValid())return;
            var zdo=chest.GetComponent<ZNetView>().GetZDO();
            if(!Canonical(zdo.GetString(NativeRaidChest.TokenKey)))return;
            loaded.Add(chest);
            RaidHosts.AddHostPrefab(Utils.GetPrefabName(chest.gameObject));
            if(!chest.GetComponent<RaidRunner>())chest.gameObject.AddComponent<RaidRunner>();
        }
        static Owner Open(Container chest){
            if(!Authority||!Valid(chest))throw new InvalidOperationException("Only an available authority-owned chest can admit a raid.");
            var identity=chest.GetComponent<ZNetView>().GetZDO();string token=identity.GetString(NativeRaidChest.TokenKey);
            if(!Canonical(token)||ZDOMan.instance==null||!ZDOMan.instance.m_objectsByID.ContainsKey(identity.m_uid)
                ||ZDOMan.instance.m_objectsByID.Values.Any(other=>other.m_uid!=identity.m_uid&&other.GetString(NativeRaidChest.TokenKey)==token))throw new InvalidDataException("Raid chest identity is invalid or duplicated.");
            if(owners.TryGetValue(chest,out var existing))return existing;
            Attach(chest);
            var value=new Owner(chest);owners.Add(chest,value);return value;
        }
        internal static InventoryDecisionRecord? Intent(Container chest,string root)=>Open(chest).Intents.Find(root);
        internal static RaidStakeReceipt? Receipt(Container chest,string root)=>Open(chest).Proofs.Latest(root);
        internal static void Reconcile(Container chest)=>Open(chest).Recover();
        internal static void Detach(Container chest){if(owners.TryGetValue(chest,out var owner)){owner.Recover();owner.Dispose();owners.Remove(chest);}loaded.Remove(chest);refused.Remove(chest);}
        internal static void Release(Container chest){if(owners.TryGetValue(chest,out var owner)){owner.Dispose();owners.Remove(chest);}loaded.Remove(chest);refused.Remove(chest);}
        internal static bool CanRun(RaidRunner runner)=>Authority&&!refused.Contains(runner.GetComponent<Container>())&&owners.TryGetValue(runner.GetComponent<Container>(),out var owner)&&owner.VerifiedRoot==runner.Zdo?.GetString(RootKey);

        internal static string? Start(Container chest,Player asker,int stake,string requestId,int tier=-1){
            if(!Authority||!Enabled||!Plugin.NativeRulesReady)return "Raid admission requires the active Wildblood host.";
            if(!asker||asker!=Player.m_localPlayer||asker.IsDead()||!Valid(chest))return "An available host player and authority-owned chest are required.";
            if((asker.transform.position-chest.transform.position).sqrMagnitude>25f)return "Stand within 5 meters of the chest.";
            if(chest.IsInUse()||chest.m_checkGuardStone&&!PrivateArea.CheckAccess(chest.transform.position,0f,false)
                ||!chest.CheckAccess(asker.GetPlayerID()))return "The chest is in use or access is denied.";
            if(stake<1||stake>RaidStakePlan.MaxStake||!Canonical(requestId))return "Invalid stake or request ID.";
            var view=chest.GetComponent<ZNetView>();var zdo=view.GetZDO();
            string token=zdo.GetString(NativeRaidChest.TokenKey);
            if(string.IsNullOrEmpty(token)){token=Guid.NewGuid().ToString("N");string fresh=token;NativeRaidChest.WriteKeys(chest,z=>z.Set(NativeRaidChest.TokenKey,fresh));}
            try {
                var owner=Open(chest);owner.Recover();
                var previous=owner.Intents.Snapshot().FirstOrDefault(r=>RaidStakePlan.Decode(r.Metadata).RequestId==requestId);
                if(previous!=null){var replay=RaidStakePlan.Decode(previous.Metadata);return replay.Stake==stake&&replay.AskerCharacterId==asker.GetPlayerID()&&(tier<0||replay.Tier==tier)
                    &&owner.Proofs.Latest(replay.EventRoot)?.Stage>=RaidStakeStage.EventCommitted?null:"The request is unresolved or conflicts with its retained plan.";}
                if(owner.Intents.Snapshot().Any(r=>r.State==InventoryDecision.Prepared||r.State==InventoryDecision.Decided))return "An earlier admission requires recovery.";
                if(!owner.Intents.CanAdmit||!owner.Checkpoints.CanAdmit||owner.Intents.Snapshot().Count>=128)return "Raid journal capacity reached.";
                var runner=chest.GetComponent<RaidRunner>();
                if(runner.State.Running)return "A raid is already on here.";
                if(EffectArea.GetBaseValue(chest.transform.position,20f)<3)return "The chest must stand in a player base.";
                var nearZdos=new List<ZDO>();RaidZdos.Near(chest.transform.position,RaidTable.RaidSpacing,nearZdos);
                long now=NetTime.NowMs();
                if(runner.State.CooldownUntil>now||nearZdos.Any(other=>(other.GetPosition()-chest.transform.position).sqrMagnitude<=RaidTable.CooldownShareRange*RaidTable.CooldownShareRange&&new RaidState(other).CooldownUntil>now))return "A nearby raid chest is resting.";
                string? why=RaidSpacing.Refusal(chest.transform.position,zdo.m_uid);if(why!=null)return why;
                RaidChoice? choice=RaidPick.Draw(chest.transform.position);if(choice==null)return "No raid has been unlocked in this world yet.";
                GearTier.Publish(asker);var near=RaidPresence.Of(runner);var heat=RaidHeat.For(stake,tier>=0?tier:near.StrongestTier);
                string root=Guid.NewGuid().ToString("N");long peer=ZNet.GetUID();
                var plan=new RaidStakePlan(ZNet.instance.GetWorldUID(),token,root,requestId,asker.GetPlayerID(),choice.EventName,choice.DisplayName,
                    stake,heat.Heat,(byte)heat.Band.Index,(byte)heat.Tier,heat.Band.CoinsFor(stake),heat.Drops,now,now+900000,now+20000,now,
                    now+(long)(NetTime.DaySeconds()*RaidTable.CooldownDays*1000),checked((ushort)near.Players),(byte)near.StrongestTier,near.LocalNear,peer,peer);
                if(owner.Chest.Prepare(plan)!=InventoryDecision.Prepared)return "The chest cannot reserve that stake.";
                var record=owner.Chest.Record(root);
                if(record.Before.Length+record.After.Length+record.Metadata.Length>12288){owner.Chest.Abort(root);return "Raid stake image exceeds the bounded admission size.";}
                try {
                    var intent=owner.Intents.Prepare(root,record.Source,record.Fingerprint,record.Deadline,record.Before,record.After,record.Metadata);
                    owner.Intents.Transition(intent,InventoryDecision.Decided);
                    owner.Settle(intent);return null;
                } catch {if(owner.Intents.Find(root)?.State!=InventoryDecision.Decided&&owner.Intents.Find(root)?.State!=InventoryDecision.Committed)owner.Chest.Abort(root);throw;}
            } catch(Exception error){Plugin.NativeLogger?.LogError("Raid admission retained for recovery: "+error.GetType().Name+": "+error.Message);return "Raid admission could not complete; retained records require recovery.";}
        }
        static bool Canonical(string value)=>value!=null&&value.Length==32&&value.Any(c=>c!='0')&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
        internal static void Tick(){
            if(command==null)command=new Terminal.ConsoleCommand("wildblood_raid","start <coins> [tier 0-7] [request-id] or stop: host-owned chest within 5 meters",args=>{
                var player=Player.m_localPlayer;if(!Authority||!player){args.Context.AddString("Only the active local host can use this command.");return;}
                var chest=UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None).Where(c=>Valid(c)&&(c.transform.position-player.transform.position).sqrMagnitude<=25f).OrderBy(c=>(c.transform.position-player.transform.position).sqrMagnitude).FirstOrDefault();
                if(!chest){args.Context.AddString("No authority-owned chest within 5 meters.");return;}
                if(args.Length==2&&args[1]=="stop"){if(chest.GetComponent<RaidRunner>() is RaidRunner runner&&runner.State.Running){runner.End(RaidEnd.Stopped);Reconcile(chest);args.Context.AddString("Raid stopped.");}else args.Context.AddString("No raid is running at this chest.");return;}
                switch(RaidStartArgs.Parse(args.Args,out var start,out string badTier)){
                    case RaidStartParse.Usage:args.Context.AddString(WildbloodText.Text("raid_usage"));return;
                    case RaidStartParse.BadTier:args.Context.AddString(WildbloodText.Format("raid_tier_invalid",badTier));return;
                }
                string request=start.RequestId??Guid.NewGuid().ToString("N");args.Context.AddString(Start(chest,player,start.Coins,request,start.Tier)??("Raid admitted. Request "+request));
            });
            if(SourcePresent){CloseOwners();return;}
            RaidNet.EnsureRegistered();
            if(!Authority){CloseOwners();if(Enabled)RaidHere.PingDue(Time.time);return;}
            if(generation!=CruciblePlugin.Session.Generation){CloseOwners();refused.Clear();generation=CruciblePlugin.Session.Generation;}
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+RaidTable.TickSeconds;
            foreach(var stale in owners.Keys.Where(c=>!c).ToArray())Release(stale);
            foreach(var chest in loaded.ToArray()){
                if(!chest){loaded.Remove(chest);continue;}
                if(!Valid(chest)||string.IsNullOrEmpty(chest.GetComponent<ZNetView>().GetZDO().GetString(NativeRaidChest.TokenKey)))continue;
                if(refused.Contains(chest))continue;
                try{var owner=Open(chest);owner.Recover();var runner=chest.GetComponent<RaidRunner>();if(!Enabled&&runner&&runner.State.Running){runner.End(RaidEnd.Stopped);owner.Recover();}}catch(Exception e){refused.Add(chest);Plugin.NativeLogger?.LogError("Raid recovery held: "+e.GetType().Name+": "+e.Message);}
            }
            RaidHosts.TickAll();RaidHere.PingDue(Time.time);
        }
        static void CloseOwners(){foreach(var owner in owners.Values)owner.Dispose();owners.Clear();}
        internal static bool Shutdown(){
            bool release=true;
            foreach(var entry in owners.ToArray())try{
                if(Authority&&Valid(entry.Key)){var runner=entry.Key.GetComponent<RaidRunner>();if(runner&&runner.State.Running)runner.End(RaidEnd.Stopped);entry.Value.Recover();}
            }catch(Exception e){release=false;Plugin.NativeLogger?.LogError("Raid shutdown retained for recovery: "+e.GetType().Name+": "+e.Message);}
            release&=ZDOMan.instance==null||!ZDOMan.instance.m_objectsByID.Values.Any(zdo=>zdo.GetBool(NativeRaidChest.HoldKey)
                ||!string.IsNullOrEmpty(zdo.GetString(NativeRaidChest.TokenKey))&&RaidState.IsRunning(zdo));
            CloseOwners();loaded.Clear();refused.Clear();next=0;generation=-1;return release;
        }

        sealed class Owner:IDisposable {
            readonly Container container;
            internal readonly InventoryDecisionJournal Intents,Checkpoints;
            internal readonly RaidStakeCheckpoints Proofs;
            internal readonly NativeRaidChest Chest;
            internal string VerifiedRoot="";
            internal Owner(Container value){
                container=value;var zdo=value.GetComponent<ZNetView>().GetZDO();string token=zdo.GetString(NativeRaidChest.TokenKey);long world=ZNet.instance.GetWorldUID();
                if(!Canonical(token)||loaded.Any(other=>other&&other!=value&&other.GetComponent<ZNetView>()&&other.GetComponent<ZNetView>().IsValid()&&other.GetComponent<ZNetView>().GetZDO().m_uid!=zdo.m_uid&&other.GetComponent<ZNetView>().GetZDO().GetString(NativeRaidChest.TokenKey)==token))throw new InvalidDataException("Raid chest identity is invalid or duplicated.");
                string folder=Path.Combine(Paths.BepInExRootPath,"Wildblood.raid-authority",world.ToString(CultureInfo.InvariantCulture));Directory.CreateDirectory(folder);
                Intents=new InventoryDecisionJournal(Path.Combine(folder,token+".intent.wal"));
                try {foreach(var record in Intents.Snapshot()){var plan=RaidStakePlan.Decode(record.Metadata);RaidStakePlan.Verify(record,world,token,record.Id,plan.RequestId,record.Fingerprint);}Checkpoints=new InventoryDecisionJournal(Path.Combine(folder,token+".proof.wal"));try{Proofs=new RaidStakeCheckpoints(Checkpoints,Intents.Find);Chest=new NativeRaidChest(value,world,token,Intents);}catch{Checkpoints.Dispose();throw;}}catch{Intents.Dispose();throw;}
            }
            internal void Settle(InventoryDecisionRecord intent){
                var plan=RaidStakePlan.Verify(intent,ZNet.instance.GetWorldUID(),container.GetComponent<ZNetView>().GetZDO().GetString(NativeRaidChest.TokenKey),intent.Id,RaidStakePlan.Decode(intent.Metadata).RequestId,intent.Fingerprint);
                if(Chest.AcceptDecision(intent)!=InventoryDecision.Committed||!Chest.ReceiptConfirmed(plan.Source))throw new InvalidOperationException("Native debit is not confirmed.");

                var proof=Proofs.Latest(plan.EventRoot);if(proof==null)proof=Proofs.Record(plan.EventRoot,RaidStakeStage.Debited);
                var zdo=container.GetComponent<ZNetView>().GetZDO();var state=new RaidState(zdo);string installed=zdo.GetString(RootKey);
                if(proof.Stage==RaidStakeStage.Terminal)return;
                if(proof.Stage==RaidStakeStage.Debited){if(state.Running&&installed!=plan.EventRoot)throw new InvalidDataException("Another raid occupies the host.");Install(container.GetComponent<RaidRunner>(),plan,intent.Fingerprint);} else if(installed==plan.EventRoot){
                    if(zdo.GetString(HashKey)!=intent.Fingerprint||state.StartedAt!=plan.StartedAtMs||state.EventName!=plan.EventName||state.Stake!=plan.Stake||state.DisplayName!=plan.DisplayName||state.Heat!=plan.Heat||state.Band!=plan.Band||state.Tier!=plan.Tier||state.CoinsBack!=plan.CoinBudget||state.Deadline!=plan.DeadlineMs||state.CooldownUntil!=plan.CooldownUntilMs)throw new InvalidDataException("Raid event binding conflict.");
                } else {
                    if(state.Running||!string.IsNullOrEmpty(installed))throw new InvalidDataException("Raid event checkpoint is occupied.");
                    Install(container.GetComponent<RaidRunner>(),plan,intent.Fingerprint);
                    container.GetComponent<RaidRunner>().End(RaidEnd.Abandoned);
                }
                bool announced=proof.Stage==RaidStakeStage.Debited;
                if(announced)proof=Proofs.Record(plan.EventRoot,RaidStakeStage.EventCommitted);
                if(intent.State==InventoryDecision.Decided)Intents.Transition(intent,InventoryDecision.Committed);
                VerifiedRoot=plan.EventRoot;
                if(announced){try{RaidNet.Tell(container.GetComponent<RaidRunner>(),RaidText.Started(plan.DisplayName,RaidTable.Band(plan.Band)));}catch(Exception e){Plugin.NativeLogger?.LogWarning("Raid start notification: "+e.GetType().Name);}}
                if(state.Phase==RaidPhase.Ended){if(state.Ended==RaidEnd.None)throw new InvalidDataException("Missing terminal reason.");Proofs.Record(plan.EventRoot,RaidStakeStage.Terminal,(byte)state.Ended,state.EndedAt);}
            }
            internal void Recover(){
                VerifiedRoot="";
                foreach(var intent in Intents.Snapshot()){
                    if(intent.State==InventoryDecision.Decided||intent.State==InventoryDecision.Committed){
                        var proof=Proofs.Latest(intent.Id);
                        if(proof?.Stage==RaidStakeStage.Terminal&&Chest.ReceiptConfirmed(RaidStakePlan.Decode(intent.Metadata).Source))continue;
                        Settle(intent);
                    }
                }
            }
            public void Dispose(){try{Chest.Dispose();}finally{Checkpoints.Dispose();Intents.Dispose();}}
        }
        static void Install(RaidRunner runner,RaidStakePlan p,string hash){
            var zdo=runner.Zdo!;var state=runner.State;
            // Phase is the publication point: every immutable start field is installed before the runner can tick.
            state.Heat=p.Heat;state.Band=p.Band;state.Tier=p.Tier;state.Stake=p.Stake;state.CoinsBack=p.CoinBudget;state.CoinsUnspent=p.CoinBudget;state.Drops=p.Drops;
            state.EventName=p.EventName;state.DisplayName=p.DisplayName;state.Wave=0;state.Alive=0;state.Left=0;state.Ended=RaidEnd.None;state.EndedAt=0;
            state.StartedAt=p.StartedAtMs;state.Deadline=p.DeadlineMs;state.PhaseUntil=p.CountdownUntilMs;state.SeenAt=p.SeenAtMs;state.CooldownUntil=p.CooldownUntilMs;
            state.Runner=p.InitialRunnerPeer;state.Server=p.InitialServerSession;state.Alone=p.Alone;
            runner.Remember(new RaidPresence.Count(p.PresencePlayers,p.PresenceStrongestTier,p.LocalNear));
            zdo.Set(HashKey,hash);zdo.Set(RootKey,p.EventRoot);state.Phase=RaidPhase.Countdown;

        }
        [HarmonyPatch(typeof(Container),nameof(Container.Awake))]
        static class LoadedChest {static void Postfix(Container __instance)=>Attach(__instance);}
        [HarmonyPatch(typeof(Container),nameof(Container.Load))]
        static class ReplicatedChest {static void Postfix(Container __instance)=>Attach(__instance);}
        [HarmonyPatch(typeof(ZNetScene),nameof(ZNetScene.Destroy),new[]{typeof(GameObject)})]
        static class DestroyedChest {static bool Prefix(GameObject go){var chest=go?go.GetComponent<Container>():null;if(!chest||!owners.TryGetValue(chest,out var owner))return true;if(!Authority||!Valid(chest))return true;try{if(NativeRaidChest.Held(chest.GetInventory()))return false;var runner=chest.GetComponent<RaidRunner>();if(runner&&runner.State.Running)runner.End(RaidEnd.Robbed);owner.Recover();Release(chest);return true;}catch(Exception e){Plugin.NativeLogger?.LogError("Raid chest destruction held: "+e.GetType().Name);return false;}}}
        [HarmonyPatch(typeof(ZNetView),nameof(ZNetView.ClaimOwnership))]
        static class RunningOwnership {static bool Prefix(ZNetView __instance)=>!__instance||!__instance.IsValid()||!__instance.GetComponent<Container>()||string.IsNullOrEmpty(__instance.GetZDO().GetString(NativeRaidChest.TokenKey))||!RaidState.IsRunning(__instance.GetZDO());}
    }
}
