using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using Burrow.Core;
using Burrow.Resources;
using Burrow.Shared;
using ExtraPotions.Burrow;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace Burrow.SharedTransfers {
    /// <summary>Requester escrow and native save reconciliation. Network tasks are
    /// started and inspected only by Tick; their continuations never touch Unity.</summary>
    internal sealed class SharedTransferClient:IDisposable {
        static readonly HashSet<Player> loadingProfiles=new HashSet<Player>();
        // Native spawn assigns m_localPlayer before loading its saved inventory.
        // Hydration must finish before journals compare before/after images.
        [HarmonyPatch(typeof(PlayerProfile),nameof(PlayerProfile.LoadPlayerData))]
        internal static class ProfileLoad {
            static void Prefix(Player __0)=>loadingProfiles.Add(__0);
            static Exception Finalizer(Player __0,Exception __exception){loadingProfiles.Remove(__0);return __exception;}
        }
        readonly int unityThread=Thread.CurrentThread.ManagedThreadId;
        readonly Dictionary<InventoryDecisionJournal,NativeBundleStore> stores=new Dictionary<InventoryDecisionJournal,NativeBundleStore>();
        readonly Dictionary<InventoryDecisionJournal,HashSet<string>> bound=new Dictionary<InventoryDecisionJournal,HashSet<string>>();
        readonly HashSet<string> verified=new HashSet<string>(StringComparer.Ordinal);
        TransferClientLedger ledger;
        Player player;
        long world;
        string actor="",callbackRoot="";
        Action<ZPackage> granted;
        Action<string> refused;
        Task<OperationResult<TransferReceipt>> pending;
        string pendingRoot="";
        bool disposed,opening,fault,held=true,evaluated;
        readonly TransferRetry retry=new TransferRetry();
        readonly TransferPassGate passes=new TransferPassGate();
        readonly TransferPassGate reopen=new TransferPassGate();
        long seenGeneration=-1;
        long retrySession=-1;
        double nextWarning,callbackAt;

        internal SharedTransferClient() { }
        static string Actor(Player value)=>value.GetPlayerID().ToString(CultureInfo.InvariantCulture);
        void AssertThread(){if(Thread.CurrentThread.ManagedThreadId!=unityThread)throw new InvalidOperationException("Shared inventory work requires Unity Update.");}
        bool Open(){
            if(disposed||opening||!Player.m_localPlayer||loadingProfiles.Contains(Player.m_localPlayer))return false;
            var current=Player.m_localPlayer;var currentWorld=NativeChestIdentity.World;
            if(currentWorld==0||current.GetPlayerID()==0)return false;
            if(ReferenceEquals(current,player)&&currentWorld==world&&Actor(current)==actor){
                if(!fault)return ledger!=null;
                if(!reopen.Due(Time.realtimeSinceStartupAsDouble,false))return false;
                // A failed append may have reached disk without updating the cached intents. Reopen every journal;
                // never clear the hold from the old cache. Keep the request id, callbacks and any in-flight reply.
                ledger?.Dispose();ledger=null;stores.Clear();bound.Clear();verified.Clear();evaluated=false;held=true;
            }else{Close();player=current;world=currentWorld;actor=Actor(current);held=true;}
            opening=true;
            try{
                string scope=TransferWire.Hash(world.ToString(CultureInfo.InvariantCulture)+"\n"+actor);
                ledger=new TransferClientLedger(Path.Combine(Paths.BepInExRootPath,"Burrow.shared-transfers",scope));
                foreach(var intent in ledger.All)if(intent.Request.Actor!=actor||intent.Request.World!=world)throw new InvalidDataException("Requester journal scope mismatch.");
                if(fault)Plugin.Log?.LogWarning("Burrow shared transfer journal reopened; reconciling durable intents before releasing inventory.");
                fault=false;passes.Wake();
                return true;
            }catch(Exception error){Fault(error);return false;}
            finally{opening=false;}
        }
        internal bool IsLocked(Inventory inventory){
            if(disposed||!Player.m_localPlayer||loadingProfiles.Contains(Player.m_localPlayer)||!ReferenceEquals(inventory,Player.m_localPlayer.GetInventory()))return false;
            if(opening)return true;
            AssertThread();
            // No ledger yet (player id or world not known during spawn): this session has no transfer to protect, and a
            // ledger from an earlier session is checked the moment it opens. Holding here refused every add to the player's
            // inventory for the first frames after spawn, so conversions run at spawn lost items (playtest 2026-10-20).
            if(!Open())return fault;
            if(!evaluated)Evaluate();
            return fault||held||ledger.Incomplete.Count>0;
        }
        /// <summary>Right after the ledger opens: held only while an earlier transfer is still unsettled (read-only check;
        /// Tick reconciles it). Before this, <c>held</c> stayed true until the first Tick.</summary>
        void Evaluate(){
            evaluated=true;
            try{
                foreach(var intent in ledger.All){
                    if(verified.Contains(intent.Root))continue;
                    if(intent.Record.State==InventoryDecision.Committed&&ledger.FinalReceiptConfirmed(intent,Store)){verified.Add(intent.Root);continue;}
                    held=true;return;
                }
                if(callbackRoot.Length==0)held=ledger.Incomplete.Count>0;
            }catch(Exception error){Fault(error);}
        }
        internal bool IsPending(Container container,Vector2i slot)=>Player.m_localPlayer&&IsLocked(Player.m_localPlayer.GetInventory());
        internal void Send(Container container,string rpc,Vector2i slot,Action<ZPackage> writeBody,Action<ZPackage> onGranted,Action<string> onRefused,Vector2i? targetSlot=null){
            AssertThread();
            // A transfer attempt is an explicit player action: it re-arms a stopped resend.
            if(retry.Exhausted)retry.Rearm(Time.realtimeSinceStartupAsDouble);
            if(!Open()||held||!ledger.CanAdmit||!CruciblePlugin.IsReady||!CruciblePlugin.Session.AuthorityReady||CruciblePlugin.Session.WorldId!=world||
               !player||player.IsDead()||player.IsTeleporting()||!player.m_nview||!player.m_nview.IsValid()||!player.m_nview.IsOwner()||
               !container||!container.m_nview||!container.m_nview.IsValid()||container.GetInventory()==null||NativeInventoryGuards.Held(player.GetInventory(),true)){
                Refuse(onRefused,ChestRequests.Denied);return;
            }
            bool writingIntent=false;
            try{
                var id=Guid.NewGuid();long header=BitConverter.ToInt64(id.ToByteArray(),0)&long.MaxValue;
                var packet=ChestRequests.NewRequest(header);writeBody(packet);
                byte[] expected;using(var sha=SHA256.Create())expected=sha.ComputeHash(NativeBundleStore.Bytes(container.GetInventory()));
                var request=new TransferRequest{Root=id.ToString("N"),Actor=actor,World=world,Session=CruciblePlugin.Session.SessionId,
                    Chest=NativeChestIdentity.Address(container.m_nview.GetZDO()),Kind=rpc,Body=packet.GetArray(),Expected=expected};
                var original=SharedTransferItems.Image(player.GetInventory(),actor);
                var escrow=SharedTransferItems.Escrow(request,original,player);
                var destination=targetSlot??ChestOps.NoSlot;
                SharedTransferItems.CheckTakeCapacity(request,container.GetInventory(),original,player,destination);
                writingIntent=true;passes.Wake();
                ledger.Admit(request,NativeBundleStore.Encode(new[]{original}),NativeBundleStore.Encode(new[]{escrow}),destination.x,destination.y);
                held=true;callbackRoot=request.Root;granted=onGranted;refused=onRefused;callbackAt=Time.realtimeSinceStartupAsDouble;retry.Reset();
                // The durable intent now owns the operation. Debit, save and send
                // happen in Tick, never in a UI callback or an async continuation.
            }catch(Exception error){
                // Append failure can be uncertain. Retain the hold if a raw intent
                // was written; never tell the UI it was denied after admission.
                if(writingIntent||ledger.Incomplete.Count>0){Fault(error);}
                else Refuse(onRefused,ChestRequests.Bad);
            }
        }
        IInventoryImageStore Store(InventoryDecisionJournal journal){
            if(!stores.TryGetValue(journal,out var store)){
                store=new NativeBundleStore(journal,world,actor);stores.Add(journal,store);
                store.BeforeChanged=(source,inventory,original)=>{
                    if(!source.StartsWith("shared.settle.",StringComparison.Ordinal))return;
                    var intent=ledger.All.Single(i=>"shared.settle."+i.Root==source);
                    SharedTransferItems.NativePickup(intent,ledger.Outcome(intent),inventory,original);
                };
                bound.Add(journal,new HashSet<string>(journal.Snapshot().Select(r=>r.Source),StringComparer.Ordinal));
            }
            foreach(var record in journal.Snapshot())if(bound[journal].Add(record.Source))store.Bind(record.Source,record.Before,record.After);
            return store;
        }
        internal void Tick(){
            AssertThread();if(disposed)return;
            if(!Player.m_localPlayer||!ZNet.instance||ZNet.instance.GetWorld()==null){Close();return;}
            if(!Open())return;
            double now=Time.realtimeSinceStartupAsDouble;
            bool signal=pending!=null&&pending.IsCompleted;
            long generation=CruciblePlugin.Session.Generation;if(generation!=seenGeneration){seenGeneration=generation;signal=true;}
            if(!passes.Due(now,signal))return;
            int verifiedBefore=verified.Count,incompleteBefore=ledger.Incomplete.Count;
            Pass();
            if(ledger==null)return;
            if(!fault)reopen.Wake();
            if(!held||pending!=null||callbackRoot.Length>0||verified.Count!=verifiedBefore||ledger.Incomplete.Count!=incompleteBefore)passes.Working(now);
            else passes.Stalled(now,retry.NextAt);
        }
        void Pass(){
            try{
                evaluated=true;held=false;
                var history=ledger.All;
                for(int index=0;index<history.Count;index++){
                    var intent=history[index];
                    if(verified.Contains(intent.Root))continue;
                    if(intent.Record.State==InventoryDecision.Committed&&ledger.FinalReceiptConfirmed(intent,Store)){verified.Add(intent.Root);continue;}
                    held=true;
                    var state=ledger.Reconcile(intent,Store);
                    if(state==InventoryDecision.Committed){verified.Add(intent.Root);Notify(intent);continue;}
                    // One unresolved actor operation is the admission rule. A
                    // restored older character save also blocks new operations.
                    if(state!=InventoryDecision.Prepared)return;
                    if(ledger.HasOutcome(intent)){
                        if(ledger.Settlement(intent)==null){
                            var before=SharedTransferItems.Image(player.GetInventory(),actor);
                            if(!SharedTransferItems.Settlement(intent,ledger.Outcome(intent),before,player,out var after))return;
                            ledger.PrepareSettlement(intent,NativeBundleStore.Encode(new[]{before}),NativeBundleStore.Encode(new[]{after}));
                        }
                        if(ledger.CommitSettlement(intent,Store)==InventoryDecision.Committed){verified.Add(intent.Root);Notify(intent);}
                        else return;
                        continue;
                    }
                    Poll(intent);return;
                }
                held=ledger.Incomplete.Count>0;
            }catch(Exception error){Fault(error);}
        }
        void Poll(TransferClientIntent intent){
            var now=Time.realtimeSinceStartupAsDouble;
            // Request Timeout bounds only the waiting UI; resends and the held
            // escrow continue, and a later receipt still settles the items.
            if(callbackRoot==intent.Root&&now-callbackAt>=(SharedSettings.RequestTimeout?.Value??2f)){Messages.Center(SharedWords.NoAnswer);Unanswered(intent);}
            if(pending!=null){
                if(!pending.IsCompleted)return;
                var task=pending;pending=null;
                if(pendingRoot==intent.Root&&!task.IsCanceled&&!task.IsFaulted){
                    var result=task.Result;
                    if(result.State==OperationState.Succeeded&&result.Reply!=null&&ledger.RecordOutcome(intent,result.Reply)){retry.Reset();return;}
                }
                // Core rejection, cancellation, timeout, and product Recovery are
                // uncertainty, not evidence that the owner kept the items.
                if(retry.Failed(now))GiveUp(intent);
            }
            // A new authority session (reconnect) re-arms a stopped resend.
            var session=CruciblePlugin.Session.Generation;
            if(session!=retrySession){retrySession=session;if(retry.Exhausted)retry.Rearm(now);}
            if(!retry.Due(now)||!CruciblePlugin.IsReady||!CruciblePlugin.Session.AuthorityReady||CruciblePlugin.Session.WorldId!=world)return;
            pendingRoot=intent.Root;
            pending=CruciblePlugin.RequestClient.InvokeAsync<TransferRequest,TransferReceipt>(TransferWire.Operation,intent.Request,CancellationToken.None);
        }
        // Automatic resends stopped. The escrow stays held (no refund without a
        // receipt); the waiting UI hears the existing refusal path once, with the
        // no-answer message, and a later receipt still settles the items.
        void GiveUp(TransferClientIntent intent){
            Plugin.Log?.LogWarning("Burrow shared transfer: no answer after "+TransferRetry.MaxAttempts+" attempts; retained until reconnect or the next transfer.");
            Messages.Center(SharedWords.NoAnswer);
            Unanswered(intent);
        }
        void Unanswered(TransferClientIntent intent){
            if(callbackRoot!=intent.Root)return;
            var failure=refused;callbackRoot="";granted=null;refused=null;
            try{failure?.Invoke(ChestRequests.Denied);}catch(Exception error){Warn(error);}
        }
        void Notify(TransferClientIntent intent){
            if(callbackRoot!=intent.Root)return;
            var success=granted;var failure=refused;callbackRoot="";granted=null;refused=null;
            held=ledger.Incomplete.Count>0;
            var receipt=ledger.Outcome(intent);
            try{if(receipt.Disposition==TransferDisposition.Approved)success?.Invoke(new ZPackage(receipt.Payload));else Refuse(failure,receipt.Reason);}catch(Exception error){Warn(error);}
        }
        static void Refuse(Action<string> callback,string reason){
            Messages.Center(reason==ChestRequests.ChestFull?SharedWords.ChestFull:SharedWords.Denied);
            callback?.Invoke(reason);
        }
        void Warn(Exception error){if(Time.realtimeSinceStartupAsDouble<nextWarning)return;nextWarning=Time.realtimeSinceStartupAsDouble+10;Plugin.Log?.LogWarning("Burrow shared transfer retained for recovery: "+error.GetType().Name);}
        void Fault(Exception error){fault=true;held=true;reopen.Stalled(Time.realtimeSinceStartupAsDouble,double.PositiveInfinity);Warn(error);}
        void Close(){ledger?.Dispose();ledger=null;stores.Clear();bound.Clear();verified.Clear();player=null;world=0;actor="";pending=null;pendingRoot="";callbackRoot="";granted=null;refused=null;fault=false;held=true;evaluated=false;retry.Reset();retrySession=-1;passes.Wake();reopen.Wake();seenGeneration=-1;}
        public void Dispose(){disposed=true;Close();}
    }

    internal static class SharedTransferItems {
        const int MaximumEntries=1024;
        internal static NativeImage Image(Inventory inventory,string actor)=>new NativeImage{Id="player:"+actor,Width=inventory.GetWidth(),Height=inventory.GetHeight(),Data=NativeBundleStore.Bytes(inventory)};
        static void End(ZPackage packet){if(packet.GetPos()!=packet.Size())throw new InvalidDataException("Transfer body trailing data.");}
        static int Count(ZPackage packet){int count=packet.ReadInt();if(count<0||count>MaximumEntries)throw new InvalidDataException("Transfer item count.");return count;}
        static ZPackage Body(TransferRequest request){
            if(request.Body.Length>TransferWire.Maximum)throw new InvalidDataException("Transfer body bound.");
            var packet=new ZPackage(request.Body);var header=ChestRequests.ReadHeader(packet);
            if(header.PlayerId.ToString(CultureInfo.InvariantCulture)!=request.Actor)throw new InvalidDataException("Transfer actor mismatch.");return packet;
        }
        static ItemDrop.ItemData Item(ZPackage packet){
            var item=ItemPacket.Read(packet);
            if(item==null||!item.m_dropPrefab||item.m_stack<=0||item.m_shared.m_maxStackSize<1||item.m_stack>item.m_shared.m_maxStackSize)throw new InvalidDataException("Invalid transfer item.");
            return item;
        }
        static byte[] Bytes(ItemDrop.ItemData item){var packet=new ZPackage();if(!ItemPacket.Write(packet,item))throw new InvalidDataException("Missing transfer prefab.");return packet.GetArray();}
        static List<ItemDrop.ItemData> Offers(TransferRequest request){
            var packet=Body(request);var result=new List<ItemDrop.ItemData>();
            if(request.Kind==ChestRequests.PutRpc){result.Add(Item(packet));packet.ReadBool();packet.ReadVector2i();}
            else if(request.Kind==ChestRequests.StackAllRpc){int count=Count(packet);for(int i=0;i<count;i++){if(packet.ReadInt()!=i)throw new InvalidDataException("Escrow index mismatch.");result.Add(Item(packet));}}
            else return result;
            End(packet);return result;
        }
        internal static NativeImage Escrow(TransferRequest request,NativeImage before,Player player){
            TransferWire.Decode(TransferWire.Encode(request));
            var result=before.Copy();var shadow=result.Load();var used=new HashSet<Vector2i>();
            if(!NativeBundleStore.Bytes(shadow).SequenceEqual(before.Data))throw new InvalidDataException("Inventory does not load back exactly; nothing escrowed.");
            foreach(var offered in Offers(request)){
                if(!used.Add(offered.m_gridPos))throw new InvalidDataException("Aliased escrow slot.");
                var actual=player.GetInventory().GetItemAt(offered.m_gridPos.x,offered.m_gridPos.y);
                if(actual==null||actual.m_stack<offered.m_stack||actual.m_equipped||player.IsItemEquiped(actual)||PackPanelGrid.InSlot(player,player.GetInventory(),actual)||
                   !Bytes(ItemPacket.Copy(actual,offered.m_stack)).SequenceEqual(Bytes(offered)))throw new InvalidDataException("Offered stack changed before escrow.");
                var stack=shadow.GetItemAt(offered.m_gridPos.x,offered.m_gridPos.y);
                if(stack==null||stack.m_stack<offered.m_stack||!Bytes(ItemPacket.Copy(stack,offered.m_stack)).SequenceEqual(Bytes(offered)))throw new InvalidDataException("Escrow snapshot mismatch.");
                stack.m_stack-=offered.m_stack;if(stack.m_stack==0)shadow.GetAllItems().Remove(stack);
            }
            result.Data=NativeBundleStore.Bytes(shadow);return result;
        }
        static Dictionary<Vector2i,(string Name,int Units)> TakeEntries(TransferRequest request){
            var packet=Body(request);var entries=new Dictionary<Vector2i,(string,int)>();
            if(request.Kind==ChestRequests.TakeRpc){var slot=packet.ReadVector2i();string name=packet.ReadString();int expected=packet.ReadInt(),amount=packet.ReadInt();if(expected<amount||amount<=0)throw new InvalidDataException("Invalid take count.");entries.Add(slot,(name,amount));}
            else if(request.Kind==ChestRequests.TakeAllRpc){int count=Count(packet);for(int i=0;i<count;i++){var slot=packet.ReadVector2i();string name=packet.ReadString();int amount=packet.ReadInt();if(amount<=0||entries.ContainsKey(slot))throw new InvalidDataException("Invalid take entry.");entries.Add(slot,(name,amount));}}
            else if(request.Kind==ChestRequests.MoveRpc){packet.ReadVector2i();packet.ReadVector2i();if(packet.ReadInt()<=0)throw new InvalidDataException("Invalid move count.");packet.ReadString();}
            else return entries;
            End(packet);return entries;
        }
        internal static void CheckTakeCapacity(TransferRequest request,Inventory chest,NativeImage before,Player player,Vector2i target){
            var shadow=before.Load();
            foreach(var entry in TakeEntries(request)){
                var item=chest.GetItemAt(entry.Key.x,entry.Key.y);
                if(item==null||item.m_shared.m_name!=entry.Value.Name||item.m_stack<entry.Value.Units||!Add(shadow,ItemPacket.Copy(item,entry.Value.Units),player,target))throw new InvalidDataException("Take does not fit safely.");
            }
        }
        internal static bool Settlement(TransferClientIntent intent,TransferReceipt receipt,NativeImage before,Player player,out NativeImage after){
            var request=intent.Request;after=before.Copy();
            if(!receipt.Matches(request)||receipt.Disposition==TransferDisposition.Recovery)throw new InvalidDataException("Unmatched transfer receipt.");
            var shadow=after.Load();var credits=new List<ItemDrop.ItemData>();var offers=Offers(request);
            if(request.Kind==ChestRequests.PutRpc||request.Kind==ChestRequests.StackAllRpc){
                var accepted=new Dictionary<int,int>();
                if(receipt.Disposition==TransferDisposition.Approved){
                    var payload=new ZPackage(receipt.Payload);
                    if(request.Kind==ChestRequests.PutRpc)accepted.Add(0,payload.ReadInt());
                    else {int count=Count(payload);for(int i=0;i<count;i++){int index=payload.ReadInt(),units=payload.ReadInt();if(accepted.ContainsKey(index))throw new InvalidDataException("Duplicate acceptance.");accepted.Add(index,units);}}
                    End(payload);
                }
                var refunds=TransferClientLedger.RefundCounts(offers.Select(i=>i.m_stack).ToArray(),accepted);
                for(int i=0;i<refunds.Length;i++)if(refunds[i]>0)credits.Add(ItemPacket.Copy(offers[i],refunds[i]));
            }else if(receipt.Disposition==TransferDisposition.Approved&&(request.Kind==ChestRequests.TakeRpc||request.Kind==ChestRequests.TakeAllRpc)){
                var allowed=TakeEntries(request);var seen=new HashSet<Vector2i>();var payload=new ZPackage(receipt.Payload);
                int count=request.Kind==ChestRequests.TakeRpc?1:Count(payload);
                for(int i=0;i<count;i++){
                    var item=Item(payload);
                    if(!seen.Add(item.m_gridPos)||!allowed.TryGetValue(item.m_gridPos,out var entry)||entry.Name!=item.m_shared.m_name||item.m_stack>entry.Units)throw new InvalidDataException("Receipt exceeds requested items.");
                    credits.Add(item);
                }
                End(payload);
            }else if(receipt.Disposition==TransferDisposition.Approved&&request.Kind==ChestRequests.MoveRpc&&receipt.Payload.Length!=0)throw new InvalidDataException("Unexpected move payload.");
            foreach(var item in credits){
                var slot=request.Kind==ChestRequests.PutRpc||request.Kind==ChestRequests.StackAllRpc?item.m_gridPos:new Vector2i(intent.TargetX,intent.TargetY);
                if(!Add(shadow,item,player,slot))return false;
            }
            after.Data=NativeBundleStore.Bytes(shadow);return true;
        }
        // Strict native item equality preserves foreign custom data and all saved
        // item properties. Grid allocation never calls ItemDrop as a fallback.
        static bool Same(ItemDrop.ItemData first,ItemDrop.ItemData second){
            var a=first.Clone();var b=second.Clone();a.m_stack=b.m_stack=1;a.m_gridPos=b.m_gridPos=new Vector2i(0,0);a.m_equipped=b.m_equipped=false;
            a.m_pickedUp=b.m_pickedUp=true;
            return Bytes(a).SequenceEqual(Bytes(b));
        }
        // Player.OnInventoryChanged normalizes newly acquired stacks and records
        // their normal pickup effects. Freeze its final flag in the after-image,
        // then restore only the incoming flag before that native callback runs.
        // Applying an already installed after-image never invokes this hook.
        internal static void NativePickup(TransferClientIntent intent,TransferReceipt receipt,Inventory inventory,IReadOnlyList<ItemDrop.ItemData> original){
            if(receipt.Disposition!=TransferDisposition.Approved||intent.Request.Kind!=ChestRequests.TakeRpc&&intent.Request.Kind!=ChestRequests.TakeAllRpc)return;
            var packet=new ZPackage(receipt.Payload);int count=intent.Request.Kind==ChestRequests.TakeRpc?1:Count(packet);
            var incoming=new List<ItemDrop.ItemData>();for(int i=0;i<count;i++)incoming.Add(Item(packet));End(packet);
            foreach(var item in inventory.GetAllItems())
                if(!original.Any(old=>ReferenceEquals(old,item))&&incoming.Any(credit=>!credit.m_pickedUp&&Same(credit,item)))item.m_pickedUp=false;
        }
        static bool Add(Inventory inventory,ItemDrop.ItemData item,Player player,Vector2i preferred){
            int width=inventory.GetWidth(),rows=Math.Min(inventory.GetHeight(),Burrow.Stow.MainGrid.Rows(player,player.GetInventory()));
            var blocked=new HashSet<Vector2i>(PackPanelGrid.BlockedCells(player,player.GetInventory()));
            bool Cell(Vector2i p)=>p.x>=0&&p.y>=0&&p.x<width&&p.y<rows&&!blocked.Contains(p);
            int remaining=item.m_stack;int maximum=item.m_shared.m_maxStackSize;
            void Place(Vector2i slot){
                if(remaining==0||!Cell(slot))return;
                var old=inventory.GetItemAt(slot.x,slot.y);
                if(old!=null){if(!Same(old,item)||old.m_stack>=maximum)return;int units=Math.Min(remaining,maximum-old.m_stack);old.m_stack+=units;remaining-=units;}
                else {var copy=item.Clone();copy.m_gridPos=slot;copy.m_equipped=false;copy.m_pickedUp=true;copy.m_stack=Math.Min(remaining,maximum);inventory.GetAllItems().Add(copy);remaining-=copy.m_stack;}
            }
            Place(preferred);
            foreach(var old in inventory.GetAllItems().ToArray())if(Cell(old.m_gridPos)&&Same(old,item))Place(old.m_gridPos);
            for(int row=0;row<rows&&remaining>0;row++){
                int y=(row+1)%rows;
                for(int x=0;x<width&&remaining>0;x++)if(inventory.GetItemAt(x,y)==null)Place(new Vector2i(x,y));
            }
            return remaining==0;
        }
    }
}
