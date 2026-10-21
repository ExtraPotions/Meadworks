using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using BepInEx;
using Burrow.Core;
using Burrow.Resources;
using Burrow.Shared;
using ExtraPotions.Burrow;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace Burrow.SharedTransfers {
    /// <summary>Only the authenticated command route enters the shadow planner.
    /// A flushed native decision and migration image precede every live write.</summary>
    internal sealed class SharedTransferOwner:IDisposable {
        const string MigrationKey="burrow.shared-decisions.v1";
        readonly int unityThread=Thread.CurrentThread.ManagedThreadId;
        readonly HashSet<Inventory> held=new HashSet<Inventory>();
        readonly HashSet<Container> blocked=new HashSet<Container>();
        readonly Dictionary<Container,uint> importedRevisions=new Dictionary<Container,uint>();
        InventoryDecisionJournal journal;NativeBundleStore native;OwnerStore store;DurableInventoryParticipant participant;
        long world;bool disposed,fault;double nextRecoveryTick;
        [ThreadStatic]static PlanScope plan;
        sealed class PlanScope {internal Container Chest;internal Inventory Inventory;internal TransferRequest Request;internal TransferReceipt Reply;}
        internal static bool IsPlanning(Container chest)=>plan!=null&&ReferenceEquals(plan.Chest,chest);
        internal static Inventory PlanningInventory(Container chest)=>IsPlanning(chest)?plan.Inventory:null;
        internal static void CaptureReply(bool ok,string reason,ZPackage payload){
            if(plan==null)return;if(plan.Reply!=null)throw new InvalidDataException("Duplicate planner reply.");
            var request=plan.Request;plan.Reply=new TransferReceipt{Root=request.Root,Actor=request.Actor,World=request.World,Chest=request.Chest,Kind=request.Kind,Disposition=ok?TransferDisposition.Approved:TransferDisposition.Denied,Reason=reason??"",Payload=payload?.GetArray()??Array.Empty<byte>()};
        }
        void UnityThread(){if(Thread.CurrentThread.ManagedThreadId!=unityThread)throw new InvalidOperationException("Shared chest operations require Unity Update.");}
        bool Open(long requestedWorld){
            if(disposed||requestedWorld==0||NativeChestIdentity.World!=requestedWorld)return false;
            if(journal!=null&&world==requestedWorld)return !fault;
            Close();world=requestedWorld;fault=false;
            try{journal=new InventoryDecisionJournal(Path.Combine(Paths.BepInExRootPath,"Burrow.shared-owner",TransferWire.Hash(world.ToString(CultureInfo.InvariantCulture))+".bin"));native=new NativeBundleStore(journal,world,"shared-owner");store=new OwnerStore(this,native);participant=new DurableInventoryParticipant(journal,store,()=>Time.realtimeSinceStartupAsDouble);RefreshLocks();return true;}
            catch(Exception error){fault=true;Plugin.Log.LogError("Burrow shared chest journal unavailable: "+error.GetType().Name);return false;}
        }
        static Container Chest(string id)=>NativeChestIdentity.Container(id);
        static bool Owns(Container chest)=>chest&&chest.m_nview&&chest.m_nview.IsValid()&&chest.m_nview.IsOwner()&&chest.m_nview.GetZDO().GetOwner()==ZNet.GetUID();
        static TransferReceipt Recovery(TransferRequest request)=>new TransferReceipt{Root=request.Root,Actor=request.Actor,World=request.World,Chest=request.Chest,Kind=request.Kind,Disposition=TransferDisposition.Recovery,Reason="recovery",Payload=Array.Empty<byte>()};
        internal OperationExecution<TransferReceipt> Handle(PeerCommandContext context,OwnerCommand command){
            UnityThread();var request=command?.Request;
            if(request==null)return OperationExecution<TransferReceipt>.Rejected("bad");
            if(context==null||(!context.IsLocal&&!context.ServerConnectionVerified)||!context.Session.AuthorityReady||context.Session.WorldId!=request.World||context.Identity==null||context.Identity.WorldId!=request.World||!Open(request.World))return OperationExecution<TransferReceipt>.Success(Recovery(request));
            // Decode again so directly invoked handlers receive the same strict bound.
            try{request=TransferWire.Decode(TransferWire.Encode(request));}catch{return OperationExecution<TransferReceipt>.Success(TransferReceipt.Denied(request,"bad"));}
            var chest=NativeChestIdentity.Container(request.Chest,journal.Find(OwnerDecisionCodec.Source(request))!=null,true);if(!Owns(chest))return OperationExecution<TransferReceipt>.Success(Recovery(request));
            if(!TryActor(request.Actor,command.SenderPeer,out _,out _))return OperationExecution<TransferReceipt>.Success(Recovery(request));
            try{
                Import(chest,true);
                RefreshLocks();
                var old=journal.Find(OwnerDecisionCodec.Source(request));
                if(old!=null){
                    var saved=OwnerDecisionCodec.ReadMetadata(old.Metadata);
                    if(!OwnerDecisionCodec.SameRequest(saved.Request,request))return OperationExecution<TransferReceipt>.Success(TransferReceipt.Denied(request,"conflict"));
                    // Durable receipts are reconciled before current gameplay eligibility.
                    if(old.State==InventoryDecision.Decided||old.State==InventoryDecision.Committed){context.MarkDecisionMade();var state=participant.Commit(old.Id);Release(old,state);RefreshLocks();return OperationExecution<TransferReceipt>.Success(state==InventoryDecision.Committed?saved.Receipt:Recovery(request));}
                    return OperationExecution<TransferReceipt>.Success(TransferReceipt.Denied(request,"aborted"));
                }
                if(blocked.Contains(chest))return OperationExecution<TransferReceipt>.Success(Recovery(request));
                if(request.Session!=context.Session.SessionId||!Eligible(chest,request,command.SenderPeer)||IsLocked(chest.GetInventory())||NativeInventoryGuards.ForeignHold(chest.m_nview)||Plugin.Instance?.Resources?.IsLocked(chest.GetInventory())==true)return OperationExecution<TransferReceipt>.Success(TransferReceipt.Denied(request,"denied"));
                byte[] image=NativeBundleStore.Bytes(chest.GetInventory());using(var sha=SHA256.Create())if(!sha.ComputeHash(image).SequenceEqual(request.Expected))return OperationExecution<TransferReceipt>.Success(TransferReceipt.Denied(request,"stale"));
                var beforeImage=new NativeImage{Id="chest:"+NativeChestIdentity.Ensure(chest.m_nview.GetZDO()),Width=chest.GetInventory().GetWidth(),Height=chest.GetInventory().GetHeight(),Data=image};
                plan=new PlanScope{Chest=chest,Inventory=beforeImage.Load(),Request=request};
                TransferReceipt receipt;byte[] after;
                try{
                    var packet=new ZPackage(request.Body);
                    switch(request.Kind){case ChestRequests.TakeRpc:ChestOwnerHandler.Take(chest,0,packet);break;case ChestRequests.PutRpc:ChestOwnerHandler.Put(chest,0,packet);break;case ChestRequests.MoveRpc:ChestOwnerHandler.Move(chest,0,packet);break;case ChestRequests.TakeAllRpc:ChestOwnerHandler.TakeAll(chest,0,packet);break;case ChestRequests.StackAllRpc:ChestOwnerHandler.StackAll(chest,0,packet);break;default:throw new InvalidDataException("Unknown chest operation.");}
                    if(packet.GetPos()!=packet.Size()||plan.Reply==null)throw new InvalidDataException("Incomplete chest plan.");receipt=plan.Reply;after=NativeBundleStore.Bytes(plan.Inventory);
                }finally{plan=null;}
                if(receipt.Disposition==TransferDisposition.Denied)return OperationExecution<TransferReceipt>.Success(receipt);
                var changed=beforeImage.Copy();changed.Data=after;var before=NativeBundleStore.Encode(new[]{beforeImage});var intended=NativeBundleStore.Encode(new[]{changed});
                byte[] metadata=OwnerDecisionCodec.Metadata(request,receipt);string source=OwnerDecisionCodec.Source(request);
                // Both metadata and the complete retained migration ledger are bounded
                // before journal admission, hence an oversized reply cannot consume items.
                Migration(chest,new OwnerDecisionCodec.Entry{Metadata=metadata,Before=before,After=intended});
                native.Bind(source,before,intended);
                var admission=participant.Prepare(source,source,OwnerDecisionCodec.Fingerprint(request),before,intended,metadata);
                if(admission!=InventoryDecision.Prepared)return OperationExecution<TransferReceipt>.Success(TransferReceipt.Denied(request,"busy"));
                native.Hold(source,false);RefreshLocks();
                var result=participant.Commit(source);var record=journal.Find(source);
                if(record.State==InventoryDecision.Decided||record.State==InventoryDecision.Committed)context.MarkDecisionMade();
                Release(record,result);RefreshLocks();return OperationExecution<TransferReceipt>.Success(result==InventoryDecision.Committed?receipt:result==InventoryDecision.Aborted?TransferReceipt.Denied(request,"aborted"):Recovery(request));
            }catch(Exception error){plan=null;Plugin.Log.LogWarning("Burrow shared transfer retained or refused: "+error.GetType().Name);RefreshLocks();return OperationExecution<TransferReceipt>.Success(Recovery(request));}
        }
        static bool TryActor(string actorId,string sender,out Player player,out ZDO character){
            player=null;character=null;if(!ZNet.instance||!long.TryParse(actorId,NumberStyles.Integer,CultureInfo.InvariantCulture,out var actor)||actor==0||!long.TryParse(sender,NumberStyles.Integer,CultureInfo.InvariantCulture,out var peerId)||peerId==0)return false;
            player=Player.GetPlayer(actor);
            if(player){var view=player.GetComponent<ZNetView>();if(view&&view.IsValid())character=view.GetZDO();}
            if(character==null){var peer=ZNet.instance.GetPeers().FirstOrDefault(p=>p.m_uid==peerId&&p.IsReady()&&p.m_rpc!=null&&p.m_rpc.IsConnected());if(peer!=null)character=ZDOMan.instance?.GetZDO(peer.m_characterID);}
            if(character==null)foreach(var info in ZNet.instance.GetPlayerList()){var candidate=ZDOMan.instance?.GetZDO(info.m_characterID);if(candidate!=null&&candidate.GetOwner()==peerId&&candidate.GetLong(ZDOVars.s_playerID,0)==actor){character=candidate;break;}}
            if(character==null||character.GetOwner()!=peerId||character.GetLong(ZDOVars.s_playerID,0)!=actor)return false;
            var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(character.GetPrefab()):null;return prefab&&prefab.GetComponent<Player>();
        }
        static bool Eligible(Container chest,TransferRequest request,string sender){
            if(Burrow.Integration.MultiUserChestYield.ModeFor(chest)!=SharedMode.Full||chest.m_inventory==null||chest.m_lastRevision==uint.MaxValue||!ContainerRules.IsEnabled(ContainerScan.PrefabName(chest)))return false;
            if(ContainerScan.IsShip(chest)&&!CoreSettings.Ships.Value||ContainerScan.IsCart(chest)&&!CoreSettings.Carts.Value||ContainerScan.IsPrivateChest(chest)&&!CoreSettings.PlayerChests.Value)return false;
            if(!TryActor(request.Actor,sender,out var player,out var character))return false;
            long actor=long.Parse(request.Actor,CultureInfo.InvariantCulture);
            if(player&&(player.IsDead()||player.IsTeleporting())||character.GetBool(ZDOVars.s_dead,false))return false;
            float radius=SharedSettings.TransferRadius.Value;if(float.IsNaN(radius)||float.IsInfinity(radius)||radius<0||radius>50)return false;
            Vector3 position=player?player.transform.position:character.GetPosition();if(float.IsNaN(position.x)||float.IsNaN(position.y)||float.IsNaN(position.z)||float.IsInfinity(position.x)||float.IsInfinity(position.y)||float.IsInfinity(position.z)||Vector3.Distance(position,chest.transform.position)>radius)return false;
            if(chest.m_privacy==Container.PrivacySetting.Private&&chest.m_piece==null||!chest.CheckAccess(actor))return false;
            if(CoreSettings.HonourWards.Value&&chest.m_checkGuardStone){bool inside=false,permitted=false;foreach(var area in PrivateArea.m_allAreas){if(!area||!area.IsEnabled()||!area.IsInside(chest.transform.position,0f))continue;inside=true;if(area.m_piece&&area.m_piece.GetCreator()==actor||area.IsPermitted(actor))permitted=true;}if(inside&&!permitted)return false;}
            return true;
        }
        static void ValidateEntry(OwnerDecisionCodec.Entry entry,Container chest,long world){
            var value=OwnerDecisionCodec.ReadMetadata(entry.Metadata);if(value.Request.World!=world||!NativeChestIdentity.Matches(chest.m_nview.GetZDO(),value.Request.Chest))throw new InvalidDataException("Migration chest identity.");
            foreach(var bytes in new[]{entry.Before,entry.After}){var packet=new ZPackage(bytes);if(packet.ReadInt()!=1||packet.ReadInt()!=1)throw new InvalidDataException("Migration inventory scope.");string id=packet.ReadString();if(!id.StartsWith("chest:",StringComparison.Ordinal)||!NativeChestIdentity.Matches(chest.m_nview.GetZDO(),id.Substring(6)))throw new InvalidDataException("Migration inventory scope.");int width=packet.ReadInt(),height=packet.ReadInt();var data=packet.ReadByteArray();if(width<1||height<1||width>256||height>256||data==null||packet.GetPos()!=packet.Size())throw new InvalidDataException("Migration inventory dimensions.");}
        }
        List<OwnerDecisionCodec.Entry> Entries(Container chest){
            var zdo=chest.m_nview.GetZDO();var bytes=zdo.GetByteArray(MigrationKey);var entries=bytes==null||bytes.Length==0?new List<OwnerDecisionCodec.Entry>():OwnerDecisionCodec.ReadMigration(bytes);
            // Existing migration belongs to this exact persisted ZDO. Its typed
            // original scope can bootstrap legacy aliases without guessing a source.
            if(NativeChestIdentity.Address(zdo)==zdo.m_uid.ToString()&&entries.Count>0){
                var aliases=new HashSet<string>(StringComparer.Ordinal);
                foreach(var entry in entries){var value=OwnerDecisionCodec.ReadMetadata(entry.Metadata);if(value.Request.World!=world)throw new InvalidDataException("Legacy migration world.");foreach(var image in new[]{entry.Before,entry.After}){var packet=new ZPackage(image);if(packet.ReadInt()!=1||packet.ReadInt()!=1||packet.ReadString()!="chest:"+value.Request.Chest)throw new InvalidDataException("Legacy migration source.");}aliases.Add(value.Request.Chest);}
                NativeChestIdentity.Ensure(zdo,aliases);
            }else NativeChestIdentity.Ensure(zdo);
            foreach(var entry in entries)ValidateEntry(entry,chest,world);return entries;
        }
        byte[] Migration(Container chest,OwnerDecisionCodec.Entry added){
            ValidateEntry(added,chest,world);var entries=Entries(chest);string source=OwnerDecisionCodec.Source(OwnerDecisionCodec.ReadMetadata(added.Metadata).Request);
            var existing=entries.FirstOrDefault(e=>OwnerDecisionCodec.Source(OwnerDecisionCodec.ReadMetadata(e.Metadata).Request)==source);
            if(existing==null)entries.Add(added);else if(!existing.Metadata.SequenceEqual(added.Metadata)||!existing.Before.SequenceEqual(added.Before)||!existing.After.SequenceEqual(added.After))throw new InvalidDataException("Migration decision conflict.");return OwnerDecisionCodec.Migration(entries);
        }
        void Import(Container chest,bool force=false){
            uint revision=chest.m_nview.GetZDO().DataRevision;
            if(!force&&importedRevisions.TryGetValue(chest,out var previous)&&previous==revision)return;
            try{foreach(var entry in Entries(chest)){
                var value=OwnerDecisionCodec.ReadMetadata(entry.Metadata);string source=OwnerDecisionCodec.Source(value.Request),fingerprint=OwnerDecisionCodec.Fingerprint(value.Request);var record=journal.Find(source);
                if(record==null){if(!journal.CanAdmit)throw new InvalidOperationException("Owner receipt capacity.");native.Bind(source,entry.Before,entry.After);record=journal.Prepare(source,source,fingerprint,Time.realtimeSinceStartupAsDouble+10,entry.Before,entry.After,entry.Metadata);journal.Transition(record,InventoryDecision.Decided);}
                else if(record.Source!=source||record.Fingerprint!=fingerprint||!record.Metadata.SequenceEqual(entry.Metadata)||!record.Before.SequenceEqual(entry.Before)||!record.After.SequenceEqual(entry.After)||record.State==InventoryDecision.Aborted)throw new InvalidDataException("Local migration conflict.");
                if(record.State==InventoryDecision.Prepared)journal.Transition(record,InventoryDecision.Decided);
            }blocked.Remove(chest);importedRevisions[chest]=revision;}catch{blocked.Add(chest);importedRevisions[chest]=revision;throw;}
        }
        void Release(InventoryDecisionRecord record,InventoryDecision state){if(state==InventoryDecision.Committed||state==InventoryDecision.Aborted)native.Release(record.Source);else native.Hold(record.Source,true);}
        void RefreshLocks(){held.Clear();if(journal==null||native==null)return;foreach(var record in journal.Snapshot().Where(r=>r.State==InventoryDecision.Prepared||r.State==InventoryDecision.Decided||r.State==InventoryDecision.Committed&&!native.ReceiptConfirmed(r.Source)))foreach(var inventory in native.Inventories(record.Source))if(inventory!=null)held.Add(inventory);foreach(var chest in blocked)if(chest&&chest.GetInventory()!=null)held.Add(chest.GetInventory());}
        internal bool IsLocked(Inventory inventory)=>inventory!=null&&held.Contains(inventory);
        internal void Tick(){
            UnityThread();if(disposed)return;var currentWorld=NativeChestIdentity.World;if(currentWorld==0){Close();return;}if(!Open(currentWorld))return;
            double now=Time.realtimeSinceStartupAsDouble;if(now<nextRecoveryTick)return;nextRecoveryTick=now+0.5;
            // Losing ownership invalidates the cache even when the inventory itself
            // did not change. Reacquisition always imports the current decision blob.
            foreach(var chest in importedRevisions.Keys.Where(c=>!Owns(c)).ToArray())importedRevisions.Remove(chest);
            foreach(var chest in ContainerScan.All().Where(Owns))try{Import(chest);}catch(Exception error){Plugin.Log.LogWarning("Burrow shared migration held: "+error.GetType().Name);}
            try{participant.Expire();foreach(var record in journal.Snapshot()){if(record.State==InventoryDecision.Aborted)native.Release(record.Source);else if(record.State==InventoryDecision.Decided||record.State==InventoryDecision.Committed&&!native.ReceiptConfirmed(record.Source)){native.Hold(record.Source,true);Release(record,participant.Commit(record.Id));}}RefreshLocks();}
            catch(Exception error){fault=true;Plugin.Log.LogError("Burrow shared recovery retained: "+error.GetType().Name);RefreshLocks();}
        }
        void Close(){journal?.Dispose();journal=null;native=null;store=null;participant=null;held.Clear();blocked.Clear();importedRevisions.Clear();nextRecoveryTick=0;}
        public void Dispose(){disposed=true;Close();}
        sealed class OwnerStore:IInventoryImageStore,IInventoryCheckpointStore,IInventoryDecisionObserver {
            readonly SharedTransferOwner owner;readonly NativeBundleStore store;
            internal OwnerStore(SharedTransferOwner owner,NativeBundleStore store){this.owner=owner;this.store=store;}
            public bool IsBound(string source)=>store.IsBound(source);
            public byte[] Capture(string source)=>store.Capture(source);
            public void Apply(string source,byte[] bytes)=>store.Apply(source,bytes);
            public void Save(string source)=>store.Save(source);
            public bool ReceiptConfirmed(string source)=>store.ReceiptConfirmed(source);
            public void DecisionFlushed(InventoryDecisionRecord record){
                var value=OwnerDecisionCodec.ReadMetadata(record.Metadata);var chest=Chest(value.Request.Chest);if(!Owns(chest))throw new IOException("Shared chest owner changed.");
                byte[] blob=owner.Migration(chest,new OwnerDecisionCodec.Entry{Metadata=record.Metadata,Before=record.Before,After=record.After});chest.m_nview.GetZDO().Set(MigrationKey,blob);
                if(!chest.m_nview.GetZDO().GetByteArray(MigrationKey).SequenceEqual(blob))throw new IOException("Shared chest migration save failed.");store.Hold(record.Source,true);
            }
        }
    }
}
