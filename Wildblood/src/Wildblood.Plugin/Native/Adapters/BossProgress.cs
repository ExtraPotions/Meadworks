// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Source ECR boss-count behavior with authority receipts for the seven verified vanilla boss families.
// Changed by expDARE, 2026: owned Wildblood integration, bounded authority receipts and native emission guards.
// Changed by expDARE, 2026: Kall Fimbulbringer's final phase (FrozenKing_p3, defeated_frozenking_p3); earlier phases never count.
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Boss progression and material behavior adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using BossProgressMessage = ExtraPotions.Wildblood.Bosses.BossProgressMessage;
using BossProgressFlightGate = ExtraPotions.Wildblood.Bosses.BossProgressFlightGate;
using BossProgressFlightStart = ExtraPotions.Wildblood.Bosses.BossProgressFlightStart;
using InstanceToken = ExtraPotions.Meadworks.Crucible.InstanceToken;
using BossProgression = ExtraPotions.Wildblood.Bosses.BossProgression;
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
using HarmonyLib;
using UnityEngine;
using RootPlugin = ExtraPotions.Wildblood.Runtime.Plugin;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    internal static class BossProgress
    {
        const string Operation = "meadworks.wildblood.boss_progress", TokenKey = "wildblood.boss_encounter", Prefix = "wildblood_boss_progress_";
        static readonly Dictionary<string,string> keys = new Dictionary<string,string>(StringComparer.Ordinal) {
            ["Eikthyr"]="defeated_eikthyr", ["gd_king"]="defeated_gdking", ["Bonemass"]="defeated_bonemass",
            ["Dragon"]="defeated_dragon", ["GoblinKing"]="defeated_goblinking", ["SeekerQueen"]="defeated_queen", ["Fader"]="defeated_fader",
            ["FrozenKing_p3"]="defeated_frozenking_p3"
        };
        internal sealed class Death
        {
            internal ZDOID Source; internal long Owner, World; internal string Boss="", Token="";
            internal float Until, Next; internal bool Terminal;
        }
        static readonly Dictionary<ZDOID,Death> proofs = new Dictionary<ZDOID,Death>();
        static readonly Dictionary<ZDOID,(Death Fact,Character Body)> retiring=new Dictionary<ZDOID,(Death,Character)>();
        static readonly HashSet<ZDOID> admittedDeaths=new HashSet<ZDOID>();
        static readonly Dictionary<string,Death> pending = new Dictionary<string,Death>(StringComparer.Ordinal);
        sealed class Flight
        {
            internal BossProgressMessage Message=null!;internal long Lease,Generation;
            internal CancellationTokenSource Cancellation=null!;internal Task<OperationResult<bool>> Task=null!;
        }
        sealed class Codec : IOperationCodec<BossProgressMessage,bool>
        {
            public byte[] EncodeRequest(BossProgressMessage request)=>request.Encode();
            public BossProgressMessage DecodeRequest(byte[] bytes)=>BossProgressMessage.Decode(bytes);
            public byte[] EncodeReply(bool value)=>new[]{value?(byte)1:(byte)0};
            public bool DecodeReply(byte[] bytes)=>bytes!=null&&bytes.Length==1&&bytes[0]<=1?bytes[0]==1:throw new FormatException("Invalid boss progress acknowledgement.");
        }
        static IDisposable? operation;
        static readonly BossProgressFlightGate flightGate=new BossProgressFlightGate();
        static readonly List<Flight> flights=new List<Flight>();
        static bool capacityReported;
        static float next;
        static int writing;
        static readonly HashSet<string> invalid=new HashSet<string>(StringComparer.Ordinal);
        static bool Current => RootPlugin.NativeRulesReady && CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.WorldId!=0
            && ZNet.instance!=null && ZNet.m_world!=null && ZNet.instance.GetWorldUID()==CruciblePlugin.Session.WorldId;
        internal static bool Eligible(Character creature, CreatureTraits traits, bool terminal=false) => keys.TryGetValue(Utils.GetPrefabName(creature.gameObject),out var key)
            && creature.IsBoss() && creature.m_defeatSetGlobalKey==key && !traits.PhantomCopy && !traits.Echo && !traits.Decoy
            && (!terminal || !(traits.HasAspect(Aspect.Tethered) && TetherPair.Standing(AspectStore.GetTether(creature.m_nview.GetZDO()))));
        internal static string Token(ZDO zdo)
        {
            string text=zdo.GetString(TokenKey);
            if(InstanceToken.TryParse(text,out _))return text;
            if(text.Length!=0||!zdo.IsOwner())return "";
            text=InstanceToken.New().Value;zdo.Set(TokenKey,text);return text;
        }
        internal static void Inherit(ZDO parent,ZDO copy) { string token=Token(parent);if(token.Length>0&&copy.IsOwner())copy.Set(TokenKey,token); }
        static BossProgression? Read(string boss)
        {
            ZoneSystem.instance.GetGlobalKey(Prefix+boss.ToLowerInvariant(),out string value);
            try { return BossProgression.Decode(value??""); }
            catch(FormatException) { if(invalid.Add(boss))RootPlugin.NativeLogger?.LogError("Invalid retained boss progression; preserved without resetting: "+boss);return null; }
        }
        internal static int Count(string boss)=>Current&&keys.ContainsKey(boss)&&ZoneSystem.instance!=null?Read(boss)?.Count??0:0;
        internal static float StarBoost(string boss) => Current&&keys.ContainsKey(boss)&&ZoneSystem.instance!=null
            ? (Read(boss)?.StarBoost(RuleState.Active.Boss.HarderEachKill)??1f) : 1f;
        internal static Death? Prepare(Character creature,EliteController controller,ZDO zdo,bool terminal=false)
        {
            if(!Current||!WildbloodModules.Active(WildbloodModule.Bosses)||!zdo.IsOwner()||!Eligible(creature,controller.Traits,terminal))return null;
            string token=Token(zdo);if(token.Length==0)return null;
            var fact=new Death{Source=zdo.m_uid,Owner=zdo.GetOwner(),World=CruciblePlugin.Session.WorldId,Boss=Utils.GetPrefabName(creature.gameObject),Token=token,Until=Time.unscaledTime+30,Terminal=terminal&&creature.GetHealth()<=0};
            Send(fact,false);return fact;
        }
        internal static void Complete(Death? fact)
        {
            if(fact==null||!Current||fact.World!=CruciblePlugin.Session.WorldId)return;
            if(!pending.ContainsKey(fact.Token)&&pending.Count>=128){RootPlugin.NativeLogger?.LogError("Boss progression settlement queue full; defeat not guessed.");return;}
            pending[fact.Token]=fact;Send(fact,true);fact.Next=Time.unscaledTime+1;
            var settled=Read(fact.Boss);if(settled!=null&&(settled.Count==BossProgression.Maximum||settled.Contains(fact.Token)))pending.Remove(fact.Token);
        }
        internal static void Initialize(ProductRegistration registration)
        {
            if(operation!=null)throw new InvalidOperationException("Boss progression operation already registered.");
            operation=CruciblePlugin.Services.Operations.Register(registration,new OperationDescriptor(Operation,new System.Version(1,0)),new Codec(),OnRequest);
        }
        static OperationExecution<bool> OnRequest(AuthorityContext context,BossProgressMessage message)
        {
            if(!Current||operation==null||!ZNet.instance.IsServer()||context.IsLocal
                ||context.Session.Generation!=CruciblePlugin.Session.Generation||context.Session.WorldId!=CruciblePlugin.Session.WorldId
                ||context.Session.SessionId!=CruciblePlugin.Session.SessionId
                ||!BossProgressMessage.TryAuthenticatedOwner(context.SenderId,out long owner))return OperationExecution<bool>.Rejected("BossAuthorityUnavailable");
            var fact=new Death{Source=new ZDOID(message.SourceUser,message.SourceId),Owner=owner,World=message.World,Boss=message.Boss,Token=message.Token,Terminal=message.Terminal,Until=Time.unscaledTime+30};
            // Receive retains the existing Bosses-off exception for an already-bound terminal settlement.
            return Admit(owner,fact,message.Complete,context.MarkDecisionMade)?OperationExecution<bool>.Success(true):OperationExecution<bool>.Rejected("BossFactRefused");
        }
        static void Send(Death fact,bool complete)
        {
            if(!Current||operation==null)return;
            if(ZNet.instance.IsServer()){Receive(fact.Owner,fact,complete);return;}
            var message=new BossProgressMessage(complete,fact.Terminal,fact.World,fact.Source.UserID,fact.Source.ID,fact.Boss,fact.Token);
            var start=flightGate.Start(message,out long lease);
            if(start!=BossProgressFlightStart.Started){if(start==BossProgressFlightStart.Full&&!capacityReported){capacityReported=true;RootPlugin.NativeLogger?.LogError("Boss progression request capacity reached; defeat not guessed.");}return;}
            var cancellation=new CancellationTokenSource();
            try{flights.Add(new Flight{Message=message,Lease=lease,Generation=CruciblePlugin.Session.Generation,Cancellation=cancellation,Task=CruciblePlugin.RequestClient.InvokeAsync<BossProgressMessage,bool>(Operation,message,cancellation.Token)});}
            catch{flightGate.Finish(message,lease);cancellation.Dispose();throw;}
        }
        static void PollRequests()
        {
            foreach(var flight in flights.ToArray())if(flight.Task.IsCompleted){
                flights.Remove(flight);flightGate.Finish(flight.Message,flight.Lease);flight.Cancellation.Dispose();
                if(flight.Task.IsFaulted){_ = flight.Task.Exception;RootPlugin.NativeLogger?.LogError("Boss progression transport task failed; defeat not guessed.");continue;}
                if(flight.Generation!=CruciblePlugin.Session.Generation)continue;
                if(flight.Task.IsCanceled){RootPlugin.NativeLogger?.LogError("Boss progression transport canceled; defeat not guessed.");continue;}
                var result=flight.Task.Result;
                if(result.State==OperationState.Succeeded&&result.Reply&&flight.Message.Terminal)FinishDeath(new ZDOID(flight.Message.SourceUser,flight.Message.SourceId));
                if((result.State!=OperationState.Succeeded||!result.Reply)&&flight.Message.Complete&&result.Code!="BossFactRefused")RootPlugin.NativeLogger?.LogError("Boss progression request not admitted: "+result.Code+"; defeat not guessed.");
            }
            if(flightGate.Count<BossProgressFlightGate.Capacity)capacityReported=false;
        }
        // Admit terminal proof before native death removes its source or issues any drops.
        internal static bool ReadyForDeath(Character creature){
            if(!Current||ZNet.instance.IsServer()||!creature||creature.IsPlayer())return true;
            var view=creature.m_nview;var controller=creature.GetComponent<EliteController>();
            if(!view||!view.IsValid()||!view.IsOwner()||controller==null||!controller.Ready||!Eligible(creature,controller.Traits,true)||!WildbloodModules.Active(WildbloodModule.Bosses))return true;
            var id=view.GetZDO().m_uid;if(admittedDeaths.Contains(id))return true;if(retiring.ContainsKey(id))return false;
            if(retiring.Count>=128){RootPlugin.NativeLogger?.LogError("Boss death proof capacity reached; native death continued without guessed progression.");return true;}
            var fact=Prepare(creature,controller,view.GetZDO(),true);if(fact==null||!fact.Terminal)return true;
            retiring[id]=(fact,creature);return false;
        }
        static void FinishDeath(ZDOID id){
            if(!retiring.TryGetValue(id,out var held))return;retiring.Remove(id);
            if(!held.Body||!held.Body.m_nview||!held.Body.m_nview.IsValid()||!held.Body.m_nview.IsOwner())return;
            admittedDeaths.Add(id);try{AccessTools.Method(typeof(Character),"CheckDeath").Invoke(held.Body,null);}finally{admittedDeaths.Remove(id);}
        }
        static void Receive(long sender,Death fact,bool complete)=>Admit(sender,fact,complete,null);
        static bool Admit(long sender,Death fact,bool complete,Action? decision)
        {
            if(!keys.ContainsKey(fact.Boss)||!InstanceToken.IsValid(fact.Token)||fact.World!=CruciblePlugin.Session.WorldId)return false;
            if(!WildbloodModules.Active(WildbloodModule.Bosses)
                &&(!complete||!proofs.TryGetValue(fact.Source,out var held)||!held.Terminal))return false;
            var source=ZDOMan.instance?.GetZDO(fact.Source);
            if(source!=null) {
                if(!NativeBossMessages.Source(fact.Source,sender,out var verified)||verified.GetString(TokenKey)!=fact.Token
                    ||ZNetScene.instance.GetPrefab(verified.GetPrefab()).name!=fact.Boss
                    ||ZNetScene.instance.GetPrefab(verified.GetPrefab()).GetComponent<Character>().m_defeatSetGlobalKey!=keys[fact.Boss])return false;
                var traits=TraitStore.Load(verified);
                if(traits.PhantomCopy||traits.Echo||traits.Decoy||(complete||fact.Terminal)&&traits.HasAspect(Aspect.Tethered)&&TetherPair.Standing(AspectStore.GetTether(verified)))return false;
                if(!complete) {
                    if(proofs.Count>=512&&!proofs.ContainsKey(fact.Source))return false;
                    // Native health is authored by this authenticated ZDO owner; its terminal claim must bind while the source exists.
                    if(proofs.TryGetValue(fact.Source,out var prior)&&prior.Owner==sender&&prior.Token==fact.Token&&prior.Boss==fact.Boss&&prior.World==fact.World&&prior.Terminal)fact.Terminal=true;
                    proofs[fact.Source]=fact;return true;
                }
                if(fact.Terminal)
                {
                    if(proofs.Count>=512&&!proofs.ContainsKey(fact.Source))return false;
                    proofs[fact.Source]=fact;
                }
            }
            if(!complete)return false;
            if(!proofs.TryGetValue(fact.Source,out var proof)||!proof.Terminal||proof.Owner!=sender||proof.Token!=fact.Token||proof.Boss!=fact.Boss||proof.Until<Time.unscaledTime)return false;
            var state=Read(fact.Boss);if(state==null)return false;
            if(state.Count==BossProgression.Maximum||state.Contains(fact.Token))return true;
            if(!state.Complete(fact.Token))return false;
            decision?.Invoke();
            writing++;try{ZoneSystem.instance.RPC_SetGlobalKey(0L,Prefix+fact.Boss.ToLowerInvariant()+" "+state.Encode());}finally{writing--;}return true;
        }
        internal static void Tick()
        {
            PollRequests();if(!Current||Time.unscaledTime<next)return;next=Time.unscaledTime+1;
            foreach(var entry in retiring.ToArray()) {
                if(entry.Value.Fact.Until<Time.unscaledTime){RootPlugin.NativeLogger?.LogError("Boss terminal proof acknowledgement expired; source cleanup continued without guessed progression.");FinishDeath(entry.Key);}
                else Send(entry.Value.Fact,false);
            }
            foreach(var stale in proofs.Where(p=>p.Value.Until<Time.unscaledTime).Select(p=>p.Key).ToArray())proofs.Remove(stale);
            if(WildbloodModules.Active(WildbloodModule.Bosses))foreach(var creature in Character.GetAllCharacters()) {
                if(creature==null||creature.IsPlayer()||creature.IsDead())continue;
                var controller=creature.GetComponent<EliteController>();var view=creature.m_nview;
                if(controller!=null&&controller.Ready&&view!=null&&view.IsValid()&&view.IsOwner()&&Eligible(creature,controller.Traits))Prepare(creature,controller,view.GetZDO());
            }
            foreach(var entry in pending.ToArray()) {
                var fact=entry.Value;var state=Read(fact.Boss);
                if(state!=null&&(state.Count==BossProgression.Maximum||state.Contains(fact.Token))){pending.Remove(entry.Key);continue;}
                if(fact.Until<Time.unscaledTime){pending.Remove(entry.Key);RootPlugin.NativeLogger?.LogError("Boss progression unresolved after retained owner proof expired; defeat not guessed: "+fact.Boss);continue;}
                if(fact.Next<=Time.unscaledTime){Send(fact,true);fact.Next=Time.unscaledTime+1;}
            }
        }
        internal static void Reset(){
            foreach(var flight in flights){flight.Cancellation.Cancel();flight.Cancellation.Dispose();if(flight.Task.IsFaulted)_=flight.Task.Exception;}
            retiring.Clear();admittedDeaths.Clear();
            flights.Clear();flightGate.Reset();capacityReported=false;proofs.Clear();pending.Clear();invalid.Clear();next=0;
        }
        internal static void Shutdown(){try{operation?.Dispose();}finally{operation=null;Reset();}}
        internal static bool MayWrite(string text)=>!Reserved(text)||writing>0;
        static bool Reserved(string text)=>(text??"").TrimStart().StartsWith(Prefix,StringComparison.OrdinalIgnoreCase);
    }
    [HarmonyPatch(typeof(ZoneSystem),"RPC_SetGlobalKey")]
    internal static class BossProgressSetGuard { static bool Prefix(string __1)=>BossProgress.MayWrite(__1); }
    [HarmonyPatch(typeof(ZoneSystem),"RPC_RemoveGlobalKey")]
    internal static class BossProgressRemoveGuard { static bool Prefix(string __1)=>BossProgress.MayWrite(__1); }
}

namespace ExtraPotions.Wildblood.Runtime {
    [HarmonyPatch(typeof(Character),"CheckDeath")]
    internal static class NativeBossDeathAdmission {
        static bool Prefix(Character __instance)=>__instance.IsDead()||__instance.GetHealth()>0||Adopted.Runtime.BossProgress.ReadyForDeath(__instance);
    }
}
