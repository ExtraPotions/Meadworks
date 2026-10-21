// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Adopted.Transfers;
using ExtraPotions.Wildblood.Raids;
using ExtraPotions.Wildblood.Transfers;
using HarmonyLib;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime {
    // Authority-owned chest participant. Event admission and remote-owned chests remain separate callers.
    internal sealed class NativeRaidChest : IInventoryImageStore,IInventoryCheckpointStore,IInventoryDecisionObserver,IDisposable {
        internal const string TokenKey="wildblood.raid.chest",HoldKey="wildblood.raid.hold";
        static readonly ConditionalWeakTable<Inventory,Container> containers=new ConditionalWeakTable<Inventory,Container>();
        static readonly HashSet<Container> opened=new HashSet<Container>();
        readonly Container chest;
        readonly long world;
        readonly string token;
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        readonly InventoryDecisionJournal journal;
        readonly InventoryDecisionJournal authorityDecisions;
        readonly DurableInventoryParticipant participant;
        readonly Dictionary<string,(byte[] Before,byte[] After,string Fingerprint)> bindings=new Dictionary<string,(byte[] Before,byte[] After,string Fingerprint)>(StringComparer.Ordinal);
        bool disposed;

        internal NativeRaidChest(Container chest,long world,string token):this(chest,world,token,null) {}
        internal NativeRaidChest(Container chest,long world,string token,InventoryDecisionJournal authorityDecisions) {
            this.chest=chest;this.world=world;this.token=token;this.authorityDecisions=authorityDecisions;
            if(!Current||!InstanceToken.IsValid(token)||opened.Contains(chest))throw new InvalidOperationException("Raid chest authority/identity unavailable.");
            string folder=Path.Combine(Paths.BepInExRootPath,"Wildblood.raid-owner",world.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            journal=new InventoryDecisionJournal(Path.Combine(folder,token+".wal"));
            try {
                foreach(var record in journal.Snapshot())Bind(record);
                if(authorityDecisions!=null)foreach(var record in journal.Snapshot().Where(r=>r.State==InventoryDecision.Prepared)){
                    var authority=authorityDecisions.Find(record.Id);
                    if(authority!=null&&(authority.State==InventoryDecision.Prepared||authority.State==InventoryDecision.Aborted))
                        RaidStakeOwnerDecision.AbortUndecided(journal,authorityDecisions,world,token,record.Id,NativeTheftInventory.Bytes(chest.GetInventory()),ReceiptConfirmed(record.Source));
                    else RaidStakeOwnerDecision.Accept(journal,authority,world,token);
                }
                participant=new DurableInventoryParticipant(journal,this,()=>UnityEngine.Time.realtimeSinceStartupAsDouble);
                Track(chest);opened.Add(chest);RefreshHold();
            } catch { WriteKeys(chest,zdo=>zdo.Set(HoldKey,true));journal.Dispose();throw; }
        }
        bool Current=>!disposed&&Thread.CurrentThread.ManagedThreadId==thread&&chest&&chest.GetInventory()!=null
            &&CruciblePlugin.IsReady&&CruciblePlugin.Session.IsAuthority&&CruciblePlugin.Session.AuthorityReady&&CruciblePlugin.Session.WorldId==world
            &&ZNet.instance&&ZNet.instance.IsServer()&&ZNet.instance.GetWorldUID()==world
            &&chest.GetComponent<ZNetView>()&&chest.GetComponent<ZNetView>().IsValid()&&chest.GetComponent<ZNetView>().IsOwner()
            &&chest.GetComponent<ZNetView>().GetZDO().GetString(TokenKey)==token;
        /// <summary>
        /// Wildblood's own keys on a raid chest's ZDO (hold, receipt, token) leave its items unchanged. When the chest had read
        /// the ZDO's latest data, its read marker moves along (as Burrow's NativeBundleStore.WriteKeys does): otherwise the
        /// game's Container.Load re-reads the same items during the hold and re-creates every item, so the debit's exact
        /// before image no longer matches and the commit is refused.
        /// </summary>
        internal static void WriteKeys(Container chest,Action<ZDO> write){
            var view=chest?chest.GetComponent<ZNetView>():null;if(!view||!view.IsValid())return;
            var zdo=view.GetZDO();uint before=zdo.DataRevision;
            write(zdo);
            if(view.IsOwner()&&chest.m_lastRevision==before)chest.m_lastRevision=zdo.DataRevision;
        }
        internal static void Track(Container value) { if(value&&value.GetInventory()!=null){containers.Remove(value.GetInventory());containers.Add(value.GetInventory(),value);} }
        internal static bool Held(Inventory inventory)=>inventory!=null&&containers.TryGetValue(inventory,out var c)&&Held(c);
        static bool Held(Container c)=>c&&c.GetComponent<ZNetView>()&&c.GetComponent<ZNetView>().IsValid()&&c.GetComponent<ZNetView>().GetZDO().GetBool(HoldKey);
        void Available(){if(!Current||TheftTransfers.RawHeld(chest.GetInventory())||TheftInventoryGuards.ForeignHeld(chest.GetInventory()))throw new InvalidOperationException("Raid chest unavailable or held by another participant.");}
        void Bind(InventoryDecisionRecord record){
            var plan=RaidStakePlan.Decode(record.Metadata);
            RaidStakePlan.Verify(record,world,token,plan.EventRoot,plan.RequestId,plan.Fingerprint(record.Before,record.After));
            NativeTheftInventory.Read(record.Before);NativeTheftInventory.Read(record.After);
            bindings[record.Source]=(record.Before,record.After,record.Fingerprint);
        }
        void RefreshHold(){if(!Current)return;bool held=journal.Snapshot().Any(r=>participant.IsHeld(r.Source));WriteKeys(chest,zdo=>zdo.Set(HoldKey,held));}
        internal InventoryDecision Prepare(RaidStakePlan plan){
            Available();participant.Expire();RefreshHold();plan=RaidStakePlan.Decode(plan.Encode());
            if(plan.WorldUid!=world||plan.ChestToken!=token)return InventoryDecision.Rejected;
            var old=journal.Find(plan.EventRoot);
            if(old!=null)return old.Metadata.SequenceEqual(plan.Encode())?participant.Status(plan.EventRoot):InventoryDecision.Rejected;
            if(journal.Snapshot().Any(r=>participant.IsHeld(r.Source)))return InventoryDecision.Rejected;
            var before=NativeTheftInventory.Bytes(chest.GetInventory());var shadow=NativeTheftInventory.Read(before);
            int remaining=plan.Stake;
            foreach(var item in shadow.GetAllItems().Where(i=>i.m_dropPrefab&&i.m_dropPrefab.name=="Coins").OrderBy(i=>i.m_gridPos.y).ThenBy(i=>i.m_gridPos.x).ToList()){
                if(item.m_stack<1)throw new InvalidDataException("Invalid coin stack.");
                int take=Math.Min(remaining,item.m_stack);item.m_stack-=take;remaining-=take;if(item.m_stack==0)shadow.GetAllItems().Remove(item);if(remaining==0)break;
            }
            if(remaining!=0)return InventoryDecision.Rejected;
            var after=NativeTheftInventory.Bytes(shadow);string hash=plan.Fingerprint(before,after);
            // Binding exists before Prepare asks the store to capture the live before image.
            bindings[plan.Source]=(before,after,hash);
            WriteKeys(chest,zdo=>zdo.Set(HoldKey,true));
            try{return participant.Prepare(plan.EventRoot,plan.Source,hash,before,after,plan.Encode());}finally{RefreshHold();}
        }
        internal IReadOnlyList<RaidStakePlan> Records {
            get {Available();return journal.Snapshot().Select(r=>RaidStakePlan.Decode(Record(r.Id).Metadata)).ToArray();}
        }
        internal InventoryDecisionRecord Record(string root){Available();var r=journal.Find(root);if(r==null)throw new InvalidDataException("Missing raid debit record.");Bind(r);return r;}
        internal string Fingerprint(string root)=>Record(root).Fingerprint;
        internal InventoryDecision AcceptDecision(InventoryDecisionRecord authority){
            Available();if(authorityDecisions!=null&&(authority==null||!ReferenceEquals(authorityDecisions.Find(authority.Id),authority)))throw new InvalidDataException("Raid decision is not retained by this authority.");
            var r=RaidStakeOwnerDecision.Accept(journal,authority,world,token);Bind(r);
            WriteKeys(chest,zdo=>zdo.Set(HoldKey,true));
            try{return participant.Commit(r.Id);}finally{RefreshHold();}
        }
        internal InventoryDecision Reconcile(string root){
            Available();var r=Record(root);
            if(r.State!=InventoryDecision.Decided&&r.State!=InventoryDecision.Committed)return participant.Status(root);
            if(authorityDecisions!=null)RaidStakeOwnerDecision.Accept(journal,authorityDecisions.Find(root),world,token);
            try{return participant.Commit(root);}finally{RefreshHold();}
        }
        internal InventoryDecision Commit(string root){Available();try{return participant.Commit(root);}finally{RefreshHold();}}
        internal InventoryDecision Status(string root){Available();try{return participant.Status(root);}finally{RefreshHold();}}
        internal InventoryDecision Abort(string root){Available();try{return participant.Rollback(root);}finally{RefreshHold();}}
        public bool IsBound(string source)=>Current&&bindings.TryGetValue(source,out var r)
            &&NativeTheftInventory.Read(r.Before).GetWidth()==chest.GetInventory().GetWidth()&&NativeTheftInventory.Read(r.Before).GetHeight()==chest.GetInventory().GetHeight();
        string ReceiptKey(string source)=>"wildblood.raid.receipt."+source;
        public bool ReceiptConfirmed(string source)=>Current&&bindings.TryGetValue(source,out var r)&&chest.GetComponent<ZNetView>().GetZDO().GetString(ReceiptKey(source))==r.Fingerprint;
        public byte[] Capture(string source){Available();if(!IsBound(source))throw new InvalidOperationException("Raid binding unavailable.");return ReceiptConfirmed(source)?bindings[source].After:NativeTheftInventory.Bytes(chest.GetInventory());}
        void Decided(string source){
            var record=journal.Snapshot().SingleOrDefault(r=>r.Source==source);
            if(record==null||(record.State!=InventoryDecision.Decided&&record.State!=InventoryDecision.Committed))throw new InvalidOperationException("Raid debit requires a flushed decision.");
            DecisionFlushed(record);
        }
        public void Apply(string source,byte[] image){
            Available();Decided(source);if(!IsBound(source)||!image.SequenceEqual(bindings[source].After)||!NativeTheftInventory.Bytes(chest.GetInventory()).SequenceEqual(bindings[source].Before))throw new InvalidOperationException("Raid image unavailable.");
            var expected=NativeTheftInventory.Read(image);var live=chest.GetInventory();var original=live.GetAllItems().ToArray();var items=new List<ItemDrop.ItemData>();
            foreach(var item in expected.GetAllItems()){
                var same=original.FirstOrDefault(i=>i.m_gridPos==item.m_gridPos&&NativeTheftInventory.SameExceptCount(i,item));
                if(same!=null){same.m_stack=item.m_stack;items.Add(same);}else items.Add(item);
            }
            TheftInventoryGuards.Applying=true;try{live.GetAllItems().Clear();live.GetAllItems().AddRange(items);live.Changed();}finally{TheftInventoryGuards.Applying=false;}
        }
        public void Save(string source){
            Available();Decided(source);if(!IsBound(source)||!NativeTheftInventory.Bytes(chest.GetInventory()).SequenceEqual(bindings[source].After))throw new InvalidOperationException("Raid binding/image unavailable.");
            chest.Save();var body=new ZPackage();chest.GetInventory().Save(body);
            if(!chest.GetComponent<ZNetView>().GetZDO().GetByteArray(ZDOVars.s_items).SequenceEqual(body.GetArray()))throw new IOException("Native raid chest save did not retain the exact inventory.");
            WriteKeys(chest,zdo=>zdo.Set(ReceiptKey(source),bindings[source].Fingerprint));
        }
        public void DecisionFlushed(InventoryDecisionRecord record){var p=RaidStakePlan.Decode(record.Metadata);RaidStakePlan.Verify(record,world,token,p.EventRoot,p.RequestId,p.Fingerprint(record.Before,record.After));}
        public void Dispose(){if(disposed)return;try{foreach(var r in journal.Snapshot().Where(r=>r.State==InventoryDecision.Prepared))participant.Rollback(r.Id);RefreshHold();}finally{disposed=true;opened.Remove(chest);journal.Dispose();}}

        [HarmonyPatch(typeof(Container),nameof(Container.Awake))]
        static class TrackChest {static void Postfix(Container __instance)=>Track(__instance);}
        [HarmonyPatch(typeof(Container),nameof(Container.Interact))]
        static class Interaction {static bool Prefix(Container __instance,ref bool __result){if(!Held(__instance))return true;__result=false;return false;}}
        [HarmonyPatch(typeof(ZNetView),nameof(ZNetView.ClaimOwnership))]
        static class Ownership {static bool Prefix(ZNetView __instance)=>!Held(__instance.GetComponent<Container>());}
        [HarmonyPatch(typeof(ZNetScene),nameof(ZNetScene.Destroy))]
        static class Destruction {static bool Prefix(UnityEngine.GameObject go)=>!go||!Held(go.GetComponent<Container>());}
        [HarmonyPatch]
        static class PieceDestruction {
            static IEnumerable<MethodBase> TargetMethods()=>typeof(WearNTear).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(m=>m.Name=="Destroy"||m.Name=="Damage");
            static bool Prefix(WearNTear __instance)=>!Held(__instance.GetComponent<Container>());
        }
    }
}
