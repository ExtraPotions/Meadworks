using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using Burrow.Core;
using ExtraPotions.Burrow;
using ExtraPotions.Meadworks.Contracts;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace Burrow.Resources {
    /// <summary>The native owner performs all reads, reservations and payments in Update.
    /// Idle chests follow the game's ownership claim; occupied chests are never claimed.</summary>
    public sealed class NativeResourceProvider:IAsyncResourceProvider,IDisposable {
        readonly ConcurrentQueue<Action> work=new ConcurrentQueue<Action>();int queued;bool disposed;
        readonly int unityThread=Thread.CurrentThread.ManagedThreadId;
        readonly ConfigEntry<bool> enabled,includePlayer,includeChests;
        readonly ConfigEntry<float> radius;
        InventoryDecisionJournal journal;DurableInventoryParticipant participant;NativeBundleStore store;
        readonly HashSet<Inventory> heldInventories=new HashSet<Inventory>();
        readonly ChestTakeRecovery salvageRecovery=new ChestTakeRecovery();
        long world;string actor="";bool sessionFault;
        // Request ids the open ledger has a record for, readable from any thread: a request that cannot be queued is
        // rejected outright only when the ledger is open and holds nothing under its id.
        readonly ConcurrentDictionary<string,byte> recorded=new ConcurrentDictionary<string,byte>(StringComparer.Ordinal);volatile bool ledgerOpen;
        public NativeResourceProvider(){
            enabled=Plugin.Synced.Bind("8. Shared Materials","Enabled",true,"Let other MeadWorks mods, such as Wildroot planting, take materials from your inventory and nearby chests.");
            includePlayer=Plugin.Synced.Bind("8. Shared Materials","Player Inventory",true,"Use the player's inventory before nearby chests.");
            includeChests=Plugin.Synced.Bind("8. Shared Materials","Nearby Chests",true,"Use permitted, loaded chests that can be owned without interrupting another player.");
            radius=Plugin.Synced.Bind("8. Shared Materials","Radius",30f,new ConfigDescription("Nearby shared material range in meters.",new AcceptableValueRange<float>(0f,50f)));
        }
        static string Actor=>Player.m_localPlayer?Player.m_localPlayer.GetPlayerID().ToString(CultureInfo.InvariantCulture):"";
        bool Active(AsyncResourceContext context,bool recovery=false)=>!disposed&&(recovery||enabled.Value)&&CruciblePlugin.IsReady&&CruciblePlugin.Session.AuthorityReady&&Player.m_localPlayer&&(recovery||!Busy)&&context.ActorId==Actor&&context.WorldId==CruciblePlugin.Session.WorldId&&(recovery||context.SessionId==CruciblePlugin.Session.SessionId);
        /// <summary>Burrow cannot read its ledger now; nothing about a request can be decided (see <see cref="ReservationAnswers"/>).</summary>
        bool Unavailable=>disposed||!CruciblePlugin.IsReady||!CruciblePlugin.Session.AuthorityReady||!Player.m_localPlayer;
        static bool Busy=>Player.m_localPlayer.IsDead()||Player.m_localPlayer.IsTeleporting();
        ResourceReservationResult Answer(AsyncResourceContext context,ReservationAnswers.Answer answer){var state=ReservationAnswers.State(answer);return new ResourceReservationResult(state,state==AsyncReservationState.Failed?null:new Reservation(this,context,Key(context),state));}
        /// <summary>A temporary condition with nothing recorded: wait inside Burrow until the deadline, then a definite Failed.</summary>
        static ResourceReservationResult Wait(double deadline)=>new ResourceReservationResult(Time.realtimeSinceStartupAsDouble<deadline?AsyncReservationState.Pending:AsyncReservationState.Failed,null);
        sealed class RequestWait {
            internal double Started=-1,Deadline;
            internal bool SawUnready;
            internal readonly HashSet<Container> Awaiting=new HashSet<Container>();
            internal string LastReason="";
            internal void Start(){if(Started<0){Started=Time.realtimeSinceStartupAsDouble;Deadline=Started+10;}}
            internal void Observe(Container chest,bool unready){if(unready){SawUnready=true;Awaiting.Add(chest);}else Awaiting.Remove(chest);}
        }
        sealed class SourceReadiness {internal int Eligible,Remote,Unloaded,Held,InUse;}
        ResourceReservationResult Defer(RequestWait wait,string reason,int sources=0,SourceReadiness readiness=null){
            wait.SawUnready=true;
            var result=Wait(wait.Deadline);
            Diagnose(wait,result.State==AsyncReservationState.Pending?reason:reason+"_deadline",sources,readiness);
            return result;
        }
        void Diagnose(RequestWait wait,string reason,int sources=0,SourceReadiness readiness=null){
            if(wait.LastReason==reason)return;wait.LastReason=reason;
            Plugin.Log.LogDebug("Burrow resources reason="+reason+" elapsed="+(Time.realtimeSinceStartupAsDouble-wait.Started).ToString("F2",CultureInfo.InvariantCulture)
                +" deadline="+wait.Deadline.ToString("F2",CultureInfo.InvariantCulture)+" admission="+(journal?.CanAdmit==true?"open":"closed")+" sources="+sources
                +" eligible="+(readiness?.Eligible??0)+" remote="+(readiness?.Remote??0)+" unloaded="+(readiness?.Unloaded??0)+" held="+(readiness?.Held??0)+" in_use="+(readiness?.InUse??0)+" unresolved="+wait.Awaiting.Count);
        }
        void Remember(string id){if(journal?.Find(id)!=null)recorded[id]=0;}
        static string Key(AsyncResourceContext context)=>Hash(Encoding.UTF8.GetBytes(context.ActorId+"\n"+context.WorldId+"\n"+context.SessionId+"\n"+context.RequestId));
        internal static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        bool Open(){
            if(!Player.m_localPlayer||!CruciblePlugin.Session.AuthorityReady)return false;
            if(journal!=null&&world==CruciblePlugin.Session.WorldId&&actor==Actor)return !sessionFault;
            Close();world=CruciblePlugin.Session.WorldId;actor=Actor;sessionFault=false;
            try{
                string name=Hash(Encoding.UTF8.GetBytes(world.ToString(CultureInfo.InvariantCulture)+"\n"+actor));
                journal=new InventoryDecisionJournal(Path.Combine(Paths.BepInExRootPath,"Burrow.transactions",name+".bin"));
                store=new NativeBundleStore(journal,world,actor,rebaseUnpaid:true);
                participant=new DurableInventoryParticipant(journal,store,()=>Time.realtimeSinceStartupAsDouble);
                RefreshLocks();
                foreach(var record in journal.Snapshot())recorded[record.Id]=0;
                ledgerOpen=true;
                return true;
            }catch(Exception error){sessionFault=true;Plugin.Log.LogError("Burrow resource recovery is unavailable: "+error.GetType().Name);return false;}
        }
        /// <summary><paramref name="notQueued"/> answers a request that never ran (queue full, shutting down);
        /// <paramref name="failure"/> answers one that threw while running and may have left a record.</summary>
        Task<T> Queue<T>(Func<T> action,T failure,Func<T,bool> defer=null,int admissionLimit=32,Func<T> notQueued=null){
            var completion=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            T Refused()=>notQueued!=null?notQueued():failure;
            if(disposed){completion.SetResult(Refused());return completion.Task;}
            if(Interlocked.Increment(ref queued)>admissionLimit){Interlocked.Decrement(ref queued);completion.SetResult(Refused());return completion.Task;}
            Action step=null;step=()=>{
                bool waiting=false;
                try{var result=disposed?Refused():action();if(!disposed&&defer!=null&&defer(result)){waiting=true;work.Enqueue(step);}else completion.TrySetResult(result);}
                catch(Exception error){Plugin.Log.LogError("Burrow resource operation retained for recovery: "+error.GetType().Name);completion.TrySetResult(failure);}
                finally{if(!waiting)Interlocked.Decrement(ref queued);}
            };work.Enqueue(step);return completion.Task;
        }
        public Task<int> GetAvailableAsync(AsyncResourceContext context,string itemId,CancellationToken cancellation){
            var wait=new RequestWait();
            return Queue(()=>{
                wait.Start();
                if(cancellation.IsCancellationRequested||!Active(context)||!Open())return 0;
                var sources=Sources(true);var readiness=Readiness(wait);
                bool playerHeld=includePlayer.Value&&NativeInventoryGuards.Held(Player.m_localPlayer.GetInventory(),true);
                if((playerHeld||wait.Awaiting.Count>0)&&Time.realtimeSinceStartupAsDouble<wait.Deadline){Diagnose(wait,"availability_wait",sources.Count,readiness);return -1;}
                Diagnose(wait,playerHeld||wait.Awaiting.Count>0?"availability_deadline":"availability_ready",sources.Count,readiness);
                return AllocationPlanner.Available(itemId,sources.Select(s=>s.Snapshot()).ToArray(),new Dictionary<StackKey,int>());
            },0,result=>result<0,admissionLimit:24);
        }
        public Task<ResourceReservationResult> TryReserveAsync(AsyncResourceContext context,IReadOnlyList<ResourceCost> costs,CancellationToken cancellation){
            var frozen=costs?.ToArray();var wait=new RequestWait();
            // Deferred reservations retain their queue slots; leave eight of the bounded 32 slots for settlement.
            // A request beyond the 24 admitted ones never ran: Failed with nothing held, unless Burrow may hold a record for it.
            return Queue(()=>{wait.Start();return Reserve(context,frozen,cancellation,wait);},new ResourceReservationResult(AsyncReservationState.RecoveryRequired,new Reservation(this,context,Key(context),AsyncReservationState.RecoveryRequired)),result=>result.State==AsyncReservationState.Pending,24,
                ()=>{Plugin.Log.LogDebug("Burrow resources reason=not_queued admission="+Volatile.Read(ref queued)+" ledger_open="+(ledgerOpen&&!disposed)+" recorded="+recorded.ContainsKey(Key(context)));return Answer(context,ReservationAnswers.NotQueued(ledgerOpen&&!disposed,recorded.ContainsKey(Key(context))));});
        }
        ResourceReservationResult Reserve(AsyncResourceContext context,IReadOnlyList<ResourceCost> costs,CancellationToken cancellation,RequestWait wait){
            if(costs==null){Diagnose(wait,"invalid_costs");return new ResourceReservationResult(AsyncReservationState.Failed,null);}
            // Temporarily unavailable is not a failure: the ledger may hold a record for this request.
            if(Unavailable||!Open()){Diagnose(wait,"ledger_unavailable");return Answer(context,ReservationAnswers.Unavailable());}
            if(!Active(context,true)){Diagnose(wait,"context_mismatch");return new ResourceReservationResult(AsyncReservationState.Failed,null);}
            var normalized=costs.Count==0?Array.Empty<ResourceCost>():AllocationPlanner.Normalize(costs).ToArray();
            string fingerprint=Hash(Encoding.UTF8.GetBytes(string.Join("\n",normalized.Select(c=>c.ItemId+"="+c.Amount))));string id=Key(context);
            var old=journal.Find(id);
            if(old!=null){recorded[id]=0;if(old.Fingerprint!=fingerprint){Diagnose(wait,"fingerprint_mismatch");return new ResourceReservationResult(AsyncReservationState.Failed,null);}return Result(context,id,participant.Status(id));}
            // A cancelled replay must still report an existing hold or durable commit under its original identity.
            if(cancellation.IsCancellationRequested){Diagnose(wait,"cancelled");return new ResourceReservationResult(AsyncReservationState.Failed,null);}
            if(enabled.Value&&context.SessionId==CruciblePlugin.Session.SessionId&&Busy)return Defer(wait,"actor_busy");
            if(!Active(context)){Diagnose(wait,"inactive");return new ResourceReservationResult(AsyncReservationState.Failed,null);}
            // Known locally held full images are temporary contention. This reads eligible stock only;
            // no locked/foreign chest is claimed or allocated, and normal admission is rerun after unlock.
            if(HasBlockedStock(normalized))return Defer(wait,"stock_held");
            if(Plugin.Instance?.Transfers?.IsLocked(Player.m_localPlayer.GetInventory())==true||NativeInventoryGuards.ForeignInventoryHeld(Player.m_localPlayer.GetInventory(),true))return Defer(wait,"player_held");
            if(!journal.CanAdmit){Diagnose(wait,"admission_closed");return new ResourceReservationResult(AsyncReservationState.Failed,null);}
            var sources=Sources(true);
            AllocationPlan plan=null;
            if(normalized.Length>0&&!AllocationPlanner.TryAllocate(normalized,sources.Select(s=>s.Snapshot()).ToArray(),new Dictionary<StackKey,int>(),out plan)){
                // OpenKeep's readiness boundary: ask the current owner for fresh data, then retry.
                // Remote inventory is never used to fund a reservation before handover completes.
                float range=radius.Value;
                bool awaitingOwner=includeChests.Value&&!float.IsNaN(range)&&!float.IsInfinity(range)&&range>=0&&range<=50&&ContainerReadiness.AskNearby(range,ContainerUse.Reach);
                var readiness=Readiness(wait);
                if(awaitingOwner||wait.SawUnready)return Defer(wait,"sources_unready",sources.Count,readiness);
                Diagnose(wait,"insufficient_stock",sources.Count,readiness);
                return new ResourceReservationResult(AsyncReservationState.Failed,null);
            }
            var needed=plan==null?new HashSet<string>():new HashSet<string>(plan.Allocations.Select(a=>a.SourceId));
            var entries=sources.Where(s=>needed.Contains(s.Id)).ToArray();
            // An empty payment still retains its identity and a native actor binding.
            if(entries.Length==0)entries=new[]{new NativeSource(Player.m_localPlayer)};
            var before=entries.Select(s=>s.Image()).ToArray();var after=before.Select(e=>e.Copy()).ToArray();
            if(plan!=null)foreach(var allocation in plan.Allocations){
                var entry=after.Single(e=>e.Id==allocation.SourceId);var inventory=entry.Load();
                // The payment is built from the loaded image: one that would drop or cut an item it does not pay from (unknown prefab,
                // a stack above the stack size, two items in one slot) is refused with nothing held.
                if(!NativeBundleStore.Bytes(inventory).SequenceEqual(entry.Data))return new ResourceReservationResult(AsyncReservationState.Failed,null);
                int slot=int.Parse(allocation.StackId.Substring(allocation.StackId.LastIndexOf(':')+1),CultureInfo.InvariantCulture);
                var item=inventory.GetItemAt(slot%entry.Width,slot/entry.Width);
                if(item==null||NativeSource.Revision(item)!=allocation.Revision||item.m_stack<allocation.Units){Diagnose(wait,"stack_changed",sources.Count);return new ResourceReservationResult(AsyncReservationState.Failed,null);}
                item.m_stack-=allocation.Units;if(item.m_stack==0)inventory.GetAllItems().Remove(item);entry.Data=NativeBundleStore.Bytes(inventory);
            }
            byte[] original=NativeBundleStore.Encode(before),changed=NativeBundleStore.Encode(after);
            store.Bind(id,original,changed);
            double hold=DurableInventoryParticipant.HoldFor(normalized.Sum(c=>(long)c.Amount));
            var admitted=participant.Prepare(id,id,fingerprint,original,changed,holdSeconds:hold);Remember(id);
            if(admitted==InventoryDecision.Prepared){store.Hold(id,false,hold);RefreshLocks();}
            else if(admitted==InventoryDecision.Rejected)Diagnose(wait,"prepare_rejected",sources.Count);
            return Result(context,id,admitted);
        }
        SourceReadiness Readiness(RequestWait wait){
            var result=new SourceReadiness();float range=radius.Value;var player=Player.m_localPlayer;
            if(!includeChests.Value||float.IsNaN(range)||float.IsInfinity(range)||range<0||range>50)return result;
            foreach(var chest in ContainerScan.All()){
                if(!chest||Vector3.Distance(player.transform.position,chest.transform.position)>range||!ContainerScan.MayBecomeUsable(chest,ContainerUse.Reach))continue;
                result.Eligible++;
                bool remote=!chest.m_nview.IsOwner(),unloaded=chest.m_lastRevision==uint.MaxValue;
                bool held=IsLocked(chest.GetInventory())||NativeInventoryGuards.Held(chest.GetInventory(),true)||NativeInventoryGuards.ForeignHold(chest.m_nview);
                bool inUse=ContainerScan.InUseByAnother(chest);
                if(remote)result.Remote++;if(unloaded)result.Unloaded++;if(held)result.Held++;if(inUse)result.InUse++;
                // Classification ignores the discovery hold filter, but never reads or allocates these inventories.
                wait.Observe(chest,remote||unloaded||held||inUse);
            }
            return result;
        }
        /// <summary>
        /// Takes one listed item out of a reachable chest through this ledger (Kindle's salvage from chests, see
        /// <see cref="Burrow.Integration.ChestSalvageLink"/>): the chest is claimed the way Reach and Shared Materials claim it,
        /// the slot must still hold the listed item, then <see cref="ChestItemTake.Run"/> holds, decides, applies and saves the
        /// removal. <paramref name="deliver"/> runs once the removal is on the chest, including when a later recovery commit
        /// is what removes it. Unity main thread only; answers with a <see cref="ChestItemTake"/> code.
        /// </summary>
        internal int TakeItem(Container chest,ItemDrop.ItemData listed,Action deliver){
            if(Thread.CurrentThread.ManagedThreadId!=unityThread||disposed||chest==null||listed==null||deliver==null)return ChestItemTake.Unavailable;
            if(Unavailable||Busy||!Open())return ChestItemTake.Unavailable;
            if(salvageRecovery.PendingId is string pending)return ContinueSalvage(chest,pending,deliver);
            if(!Burrow.Integration.ChestSalvageLink.Reachable(chest))return ChestItemTake.Unavailable;
            var player=Player.m_localPlayer;
            // Another hold on the chest or on the inventory the materials go to is brief: ask again.
            if(IsLocked(chest.GetInventory())||NativeInventoryGuards.Held(chest.GetInventory(),true)||NativeInventoryGuards.ForeignHold(chest.m_nview)
                ||Plugin.Instance?.Transfers?.IsLocked(player.GetInventory())==true||NativeInventoryGuards.Held(player.GetInventory(),true))return ChestItemTake.Pending;
            // The owner hands the chest over first; its reload may replace the item instances, so the slot is matched by content.
            if(!ContainerScan.Claim(chest))return ChestItemTake.Pending;
            var inventory=chest.GetInventory();var slot=listed.m_gridPos;
            var live=inventory.GetItemAt(slot.x,slot.y);
            if(live==null||!TakeIdentity(live).SequenceEqual(TakeIdentity(listed))||Math.Abs(live.m_durability-listed.m_durability)>0.015f)return ChestItemTake.Changed;
            if(!journal.CanAdmit)return ChestItemTake.Unavailable;
            var before=new NativeSource(chest,player).Image();
            var image=before.Load();
            if(!NativeBundleStore.Bytes(image).SequenceEqual(before.Data))return ChestItemTake.Unavailable;
            var taken=image.GetItemAt(slot.x,slot.y);if(taken==null)return ChestItemTake.Changed;
            image.GetAllItems().Remove(taken);
            var after=before.Copy();after.Data=NativeBundleStore.Bytes(image);
            byte[] original=NativeBundleStore.Encode(new[]{before}),changed=NativeBundleStore.Encode(new[]{after});
            string id=Hash(Encoding.UTF8.GetBytes("salvage\n"+actor+"\n"+world+"\n"+CruciblePlugin.Session.SessionId+"\n"+Guid.NewGuid().ToString("N")));
            store.Bind(id,original,changed);
            int result=ChestItemTake.Run(participant,id,Hash(TakeIdentity(listed)),original,changed,()=>store.Hold(id,true),deliver,out var state);
            salvageRecovery.Observe(id,result,deliver);
            if(state==InventoryDecision.Committed||state==InventoryDecision.Aborted||state==InventoryDecision.Rejected)store.Release(id);
            RefreshLocks();
            return result;
        }
        /// <summary>Finishes a salvage whose removal was decided but not on the chest yet. A different chest is not taken while that one is open.</summary>
        int ContinueSalvage(Container chest,string id,Action deliver){
            var inventory=chest.GetInventory();
            if(inventory==null||store==null||participant==null||!store.Contains(id,inventory))return ChestItemTake.Pending;
            var state=participant.Commit(id);
            bool paid=false;
            try{paid=salvageRecovery.Pay(participant,id,state,deliver);}
            catch(Exception error){Plugin.Log.LogWarning("Burrow chest salvage delivery: "+error.GetType().Name);}
            if(state==InventoryDecision.Committed||state==InventoryDecision.Aborted||state==InventoryDecision.Rejected)store.Release(id);
            RefreshLocks();
            if(paid||state==InventoryDecision.Committed)return ChestItemTake.Taken;
            if(state==InventoryDecision.RecoveryRequired)return ChestItemTake.Pending;
            return state==InventoryDecision.Aborted?ChestItemTake.Changed:ChestItemTake.Unavailable;
        }
        /// <summary>An item's saved content without its durability, which a reload may leave a hundredth short (compared apart).</summary>
        static byte[] TakeIdentity(ItemDrop.ItemData item){var copy=item.Clone();copy.m_equipped=false;copy.m_durability=0f;var packet=new ZPackage();Burrow.Shared.ItemPacket.Write(packet,copy);return packet.GetArray();}
        ResourceReservationResult Result(AsyncResourceContext context,string id,InventoryDecision state){var mapped=Map(state);return new ResourceReservationResult(mapped,mapped==AsyncReservationState.Failed||mapped==AsyncReservationState.RolledBack?null:new Reservation(this,context,id,mapped));}
        bool HasBlockedStock(IReadOnlyList<ResourceCost> costs){
            if(costs.Count==0)return false;
            bool Wanted(Inventory inventory)=>inventory.GetAllItems().Any(item=>item.m_stack>0&&item.m_dropPrefab&&item.m_worldLevel>=Game.m_worldLevel&&costs.Any(cost=>cost.ItemId==item.m_dropPrefab.name));
            var player=Player.m_localPlayer;
            if(includePlayer.Value&&NativeInventoryGuards.Held(player.GetInventory())&&Wanted(player.GetInventory()))return true;
            float range=radius.Value;if(!includeChests.Value||float.IsNaN(range)||float.IsInfinity(range)||range<0||range>50)return false;
            var containers=ContainerScan.All();
            foreach(var record in journal.Snapshot().Where(record=>record.State==InventoryDecision.Prepared||record.State==InventoryDecision.Decided||record.State==InventoryDecision.Committed&&!store.ReceiptConfirmed(record.Source))){
                // Reuse the store's exact bound-identity/native permission check, which already handles its own hold.
                if(!store.Permitted(record.Source,range))continue;
                foreach(var inventory in store.Inventories(record.Source))if(inventory!=null&&IsLocked(inventory)&&containers.Any(chest=>chest&&chest.m_nview&&chest.m_nview.IsValid()&&chest.m_nview.IsOwner()&&ReferenceEquals(chest.GetInventory(),inventory))&&Wanted(inventory))return true;
            }
            return false;
        }
        internal static AsyncReservationState Map(InventoryDecision state)=>state==InventoryDecision.Prepared?AsyncReservationState.Reserved:state==InventoryDecision.Committed?AsyncReservationState.Committed:state==InventoryDecision.Aborted?AsyncReservationState.RolledBack:state==InventoryDecision.Rejected?AsyncReservationState.Failed:AsyncReservationState.RecoveryRequired;
        Task<AsyncReservationState> Act(AsyncResourceContext context,string id,int operation,CancellationToken cancellation)=>Queue(()=>{
            if(!Active(context,true)||!Open())return AsyncReservationState.RecoveryRequired;
            if(Key(context)!=id)return AsyncReservationState.Failed;
            if(operation==1&&cancellation.IsCancellationRequested&&participant.Status(id)==InventoryDecision.Prepared)operation=2;
            if(operation==1){
                if(participant.Status(id)==InventoryDecision.Prepared&&(!enabled.Value||!store.Permitted(id,radius.Value))){participant.Rollback(id);store.Release(id);RefreshLocks();return AsyncReservationState.RolledBack;}
                store.Hold(id,true);
            }
            var state=operation==1?participant.Commit(id):operation==2?participant.Rollback(id):participant.Status(id);
            if(state==InventoryDecision.Committed||state==InventoryDecision.Aborted)store.Release(id);
            RefreshLocks();
            return Map(state);
        },AsyncReservationState.RecoveryRequired);
        public void Tick(){
            if(Thread.CurrentThread.ManagedThreadId!=unityThread)throw new InvalidOperationException("Native inventory operations require Unity Update.");
            if(journal==null&&!sessionFault&&Player.m_localPlayer&&CruciblePlugin.IsReady&&CruciblePlugin.Session.AuthorityReady)Open();
            // Requeued contention polls once per Update; commits behind them must be able to release the hold.
            int budget=Math.Min(8,work.Count);for(int i=0;i<budget&&work.TryDequeue(out var action);i++)action();
            if(journal==null||store==null||sessionFault)return;
            if(!Player.m_localPlayer||Actor!=actor||CruciblePlugin.Session.WorldId!=world){Close();return;}
            try{
                participant.Expire();
                foreach(var record in journal.Snapshot()){
                    if(record.State==InventoryDecision.Aborted)store.Release(record.Source);
                    else if(record.State==InventoryDecision.Decided||record.State==InventoryDecision.Committed&&!store.ReceiptConfirmed(record.Source)){
                        store.Hold(record.Source,true);
                        var state=participant.Commit(record.Id);
                        if(state==InventoryDecision.Committed)store.Release(record.Source);
                        if(salvageRecovery.Waiting(record.Id))try{salvageRecovery.Pay(participant,record.Id,state,null);}catch(Exception error){Plugin.Log.LogWarning("Burrow chest salvage delivery: "+error.GetType().Name);}
                    }
                }
                RefreshLocks();
            }catch(Exception error){sessionFault=true;Plugin.Log.LogError("Burrow retained unresolved resource state: "+error.GetType().Name);}
        }
        List<NativeSource> Sources(bool claim){
            var result=new List<NativeSource>();var player=Player.m_localPlayer;
            if(includePlayer.Value&&!NativeInventoryGuards.Held(player.GetInventory(),true))result.Add(new NativeSource(player));
            float range=radius.Value;if(float.IsNaN(range)||float.IsInfinity(range)||range<0||range>50)return result;
            if(includeChests.Value)foreach(var chest in ContainerScan.Nearby(player.transform.position,range,ContainerUse.Reach)){
                if(NativeInventoryGuards.Held(chest.GetInventory(),true)||NativeInventoryGuards.ForeignHold(chest.m_nview))continue;
                if(claim&&!ContainerScan.Claim(chest))continue;
                result.Add(new NativeSource(chest,player));if(result.Count>=32)break;
            }
            return result;
        }
        void RefreshLocks(){heldInventories.Clear();if(journal==null||store==null)return;foreach(var record in journal.Snapshot().Where(r=>r.State==InventoryDecision.Prepared||r.State==InventoryDecision.Decided||r.State==InventoryDecision.Committed&&!store.ReceiptConfirmed(r.Source)))foreach(var inventory in store.Inventories(record.Source))if(inventory!=null)heldInventories.Add(inventory);}
        public bool IsLocked(Inventory inventory)=>heldInventories.Contains(inventory);
        void Close(){salvageRecovery.Clear();ledgerOpen=false;journal?.Dispose();journal=null;store=null;participant=null;heldInventories.Clear();recorded.Clear();}
        public void Dispose(){disposed=true;while(work.TryDequeue(out var action))action();Close();}
        sealed class Reservation:IAsyncResourceReservation {
            readonly NativeResourceProvider owner;readonly string id;bool disposed;
            public AsyncResourceContext Identity{get;}public AsyncReservationState State{get;private set;}
            public Reservation(NativeResourceProvider owner,AsyncResourceContext context,string id,AsyncReservationState state){this.owner=owner;Identity=context;this.id=id;State=state;}
            async Task<AsyncReservationState> Action(int operation,CancellationToken cancellation){var state=await owner.Act(Identity,id,operation,cancellation).ConfigureAwait(false);State=state;return state;}
            public Task<AsyncReservationState> CommitAsync(CancellationToken cancellation)=>Action(1,cancellation);
            public Task<AsyncReservationState> RollbackAsync(CancellationToken cancellation)=>Action(2,cancellation);
            public Task<AsyncReservationState> GetStatusAsync(CancellationToken cancellation)=>Action(0,cancellation);
            public void Dispose(){if(disposed)return;disposed=true;if(State==AsyncReservationState.Reserved)_=RollbackAsync(CancellationToken.None);}
        }
        sealed class NativeSource {
            public string Id;public Inventory Inventory;public double Distance;public bool Player;
            public NativeSource(Player player){Id="player:"+player.GetPlayerID();Inventory=player.GetInventory();Player=true;}
            public NativeSource(Container chest,Player player){string token="";if(chest.m_nview.IsOwner())NativeBundleStore.WriteKeys(chest,zdo=>token=NativeChestIdentity.Ensure(zdo));else token=NativeChestIdentity.Address(chest.m_nview.GetZDO());Id="chest:"+token;Inventory=chest.GetInventory();Distance=Vector3.Distance(player.transform.position,chest.transform.position);}
            public SourceSnapshot Snapshot()=>new SourceSnapshot(Id,ZNet.GetUID().ToString(CultureInfo.InvariantCulture),Player,Distance,true,Inventory.GetAllItems().Where(i=>i.m_stack>0&&i.m_dropPrefab&&i.m_worldLevel>=Game.m_worldLevel).Select(i=>new ItemStackSnapshot(Id+":"+(i.m_gridPos.y*Inventory.GetWidth()+i.m_gridPos.x),i.m_dropPrefab.name,i.m_stack,Revision(i),i.m_gridPos.y*Inventory.GetWidth()+i.m_gridPos.x)).ToArray());
            public static string Revision(ItemDrop.ItemData item){var packet=new ZPackage();Burrow.Shared.ItemPacket.Write(packet,item);return Hash(packet.GetArray());}
            public NativeImage Image()=>new NativeImage{Id=Id,Width=Inventory.GetWidth(),Height=Inventory.GetHeight(),Data=NativeBundleStore.Bytes(Inventory)};
        }
    }
}
