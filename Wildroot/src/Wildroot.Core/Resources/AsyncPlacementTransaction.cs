using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Contracts;

namespace ExtraPotions.Wildroot {
    public interface IConfirmedPlacementHandle:IPlacementHandle {void Confirm();}
    public sealed class AsyncPlacementJob {
        internal readonly AsyncResourceContext Context;internal readonly PlacementOperation[] Operations;internal readonly IPlacementBackend[] Backends;
        internal IAsyncResourceProvider? Provider;internal Task<ResourceReservationResult>? Reserve;internal Task<AsyncReservationState>? Action;
        internal IAsyncResourceReservation? Reservation;internal readonly IPlacementHandle?[] Placed;
        internal readonly PlacementJournalRecord Record;internal int Phase;internal bool Restored,IntentSaved,CancellationSaved;
        public PlacementResult? Result{get;internal set;}public bool NeedsRecovery{get;internal set;}
        public bool Cancelled=>Record.Cancelled;public void Cancel()=>Record.Cancelled=true;
        internal bool Active=>!Cancelled&&Operations.All(o=>o.IsActive());
        internal AsyncPlacementJob(PlacementJournalRecord record,PlacementOperation[] operations,IPlacementBackend[] backends,IAsyncResourceProvider? provider){Record=record;Context=record.Context;Operations=operations;Backends=backends;Provider=provider;Placed=new IPlacementHandle?[operations.Length];}
    }
    /// <summary>Advance only from Unity Update. Durable intent precedes reserve/create,
    /// durable decision precedes commit/rollback, and replay keeps the original identity.
    /// Cancellation cannot reverse a commit decision. Absence is never removal proof.</summary>
    public sealed class AsyncPlacementTransaction {
        readonly Dictionary<string,AsyncPlacementJob> jobs=new Dictionary<string,AsyncPlacementJob>(StringComparer.Ordinal);
        // Keep replay/removal evidence without retaining completed native backends, actor closures or providers.
        readonly Dictionary<string,SettledReceipt> settled=new Dictionary<string,SettledReceipt>(StringComparer.Ordinal);
        sealed class SettledReceipt {
            internal readonly PlacementJournalRecord Record;internal readonly PlacementResult Result;
            internal SettledReceipt(AsyncPlacementJob job){Record=job.Record;Result=job.Result!;}
        }
        readonly IPlacementJournal? journal;
        bool retiring;Task? retirement;
        public AsyncPlacementTransaction(IPlacementJournal? journal=null){this.journal=journal;}
        static string Fingerprint(IEnumerable<ResourceCost> costs)=>string.Join("\n",costs.GroupBy(c=>c.ItemId,StringComparer.Ordinal).OrderBy(g=>g.Key,StringComparer.Ordinal).Select(g=>g.Key+"="+g.Aggregate(0,(sum,c)=>checked(sum+c.Amount))));
        public static string ProviderIdentity(IAsyncResourceProvider provider)=>provider.GetType().Assembly.GetName().Name+":"+provider.GetType().FullName;
        public AsyncPlacementJob Begin(AsyncResourceContext context,PlacementOperation operation,IPlacementBackend backend,IAsyncResourceProvider provider,string? providerId=null)=>BeginBatch(context,new[]{operation},new[]{backend},provider,providerId);
        public AsyncPlacementJob BeginBatch(AsyncResourceContext context,IReadOnlyList<PlacementOperation> operations,IReadOnlyList<IPlacementBackend> backends,IAsyncResourceProvider provider,string? providerId=null){
            if(context==null||operations==null||backends==null||provider==null||operations.Count==0||operations.Count!=backends.Count||operations.Any(o=>o==null)||backends.Any(b=>b==null))throw new ArgumentException("Invalid placement batch.");
            if(operations.Any(o=>o.Context.ActorId!=context.ActorId||o.Context.WorldId!=context.WorldId.ToString(System.Globalization.CultureInfo.InvariantCulture)))throw new ArgumentException("Placement actor/world mismatch.");
            var costs=operations.SelectMany(o=>o.Costs).GroupBy(c=>c.ItemId,StringComparer.Ordinal).Select(g=>new ResourceCost(g.Key,g.Aggregate(0,(sum,c)=>checked(sum+c.Amount)))).ToList();
            var record=new PlacementJournalRecord{ProviderId=providerId??ProviderIdentity(provider),ActorId=context.ActorId,WorldId=context.WorldId,SessionId=context.SessionId,RequestId=context.RequestId,Costs=costs,Cells=operations.Select((o,i)=>new PlacementCellRecord{RequestId=o.Context.RequestId,RecoveryData=(backends[i] as IDurablePlacementBackend)?.RecoveryData??"",Costs=o.Costs.ToList()}).ToList()};record.Validate();
            if(retiring)return Rejected(record,operations,backends,provider);
            if(jobs.TryGetValue(record.Key,out var old)){if(SamePlan(old.Record,record))return old;return Rejected(record,operations,backends,provider);}
            if(settled.TryGetValue(record.Key,out var receipt)){if(SamePlan(receipt.Record,record))return new AsyncPlacementJob(receipt.Record,operations.ToArray(),backends.ToArray(),null){Result=receipt.Result};return Rejected(record,operations,backends,provider);}
            if(jobs.Count>=4096)return Rejected(record,operations,backends,provider);
            var job=new AsyncPlacementJob(record,operations.ToArray(),backends.ToArray(),provider);jobs.Add(record.Key,job);
            if(!job.Active){job.Record.Completed=true;job.Result=new PlacementResult(PlacementResultKind.Cancelled,context.RequestId);Archive(job);return job;}
            StartReserve(job);return job;
        }
        static bool SamePlan(PlacementJournalRecord a,PlacementJournalRecord b)=>Fingerprint(a.Costs)==Fingerprint(b.Costs)&&a.Cells.Count==b.Cells.Count&&a.Cells.Zip(b.Cells,(x,y)=>x.RequestId==y.RequestId&&x.RecoveryData==y.RecoveryData&&Fingerprint(x.Costs)==Fingerprint(y.Costs)).All(v=>v);
        static AsyncPlacementJob Rejected(PlacementJournalRecord record,IReadOnlyList<PlacementOperation> operations,IReadOnlyList<IPlacementBackend> backends,IAsyncResourceProvider? provider){var job=new AsyncPlacementJob(record,operations.ToArray(),backends.ToArray(),provider);job.Result=new PlacementResult(PlacementResultKind.Cancelled,record.RequestId);return job;}
        static bool Same(AsyncResourceContext a,AsyncResourceContext b)=>a.ActorId==b.ActorId&&a.WorldId==b.WorldId&&a.SessionId==b.SessionId&&a.RequestId==b.RequestId;
        public AsyncPlacementJob Restore(PlacementJournalRecord record,IReadOnlyList<PlacementOperation> operations,IReadOnlyList<IPlacementBackend> backends,IAsyncResourceProvider? provider,string providerId){
            if(retiring)throw new InvalidOperationException("Placement transaction is retiring.");
            // A Completed record reached a terminal outcome; reopening it re-probes finished, paid cells every load.
            if(record!=null&&record.Completed)throw new ArgumentException("Completed placement record cannot be restored.");
            record!.Validate();if(record.ProviderId!=providerId||record.Cells.Count!=operations.Count||operations.Count!=backends.Count||record.Cells.Where((c,i)=>c.RequestId!=operations[i].Context.RequestId||c.RecoveryData!=(backends[i] as IDurablePlacementBackend)?.RecoveryData).Any()||Fingerprint(record.Costs)!=Fingerprint(operations.SelectMany(o=>o.Costs)))throw new ArgumentException("Recovery plan does not match original intent.");
            if(jobs.TryGetValue(record.Key,out var old)){
                if(!SamePlan(old.Record,record))throw new ArgumentException("Recovery identity collision.");
                for(int i=0;i<operations.Count;i++){old.Operations[i]=operations[i];old.Backends[i]=backends[i];old.Placed[i]=null;old.Record.Cells[i].Settled=false;}
                old.Restored=true;old.Result=null;old.NeedsRecovery=true;old.Action=null;old.Reservation=null;old.Provider=provider;
                if(old.Record.Decision==PlacementDecisionState.None){old.Record.Cancelled=true;old.Record.Decision=PlacementDecisionState.Abort;old.Record.AbortKind=PlacementResultKind.Cancelled;}
                StartReserve(old);return old;
            }
            if(settled.TryGetValue(record.Key,out var receipt)){
                if(!SamePlan(receipt.Record,record))throw new ArgumentException("Recovery identity collision.");
                // This process already settled the record; an older loaded snapshot cannot reopen it.
                return new AsyncPlacementJob(receipt.Record,operations.ToArray(),backends.ToArray(),null){Result=receipt.Result};
            }
            foreach(var cell in record.Cells)cell.Settled=false;
            var job=new AsyncPlacementJob(record,operations.ToArray(),backends.ToArray(),provider){Restored=true,IntentSaved=true,NeedsRecovery=true};jobs.Add(record.Key,job);
            if(record.Decision==PlacementDecisionState.None){record.Cancelled=true;record.Decision=PlacementDecisionState.Abort;record.AbortKind=PlacementResultKind.Cancelled;}
            StartReserve(job);return job;
        }
        public void Bind(string providerId,IAsyncResourceProvider? provider){if(retiring)return;foreach(var job in jobs.Values.Where(j=>j.Result==null&&j.Record.ProviderId==providerId)){
            if(ReferenceEquals(job.Provider,provider)&&!(job.Phase==6&&job.NeedsRecovery))continue;
            job.Provider=provider;job.Reservation=null;job.Action=null;job.Reserve=null;job.NeedsRecovery=true;job.Phase=6;
            if(provider!=null)StartReserve(job);
        }}
        void Save(AsyncPlacementJob job){journal?.Save(job.Record);job.IntentSaved=true;}
        void StartReserve(AsyncPlacementJob job){
            try{Save(job);if(job.Provider==null){job.NeedsRecovery=true;if(job.Restored&&job.Record.Decision!=PlacementDecisionState.Commit)AbandonUnpaid(job);else job.Phase=6;return;}job.Phase=0;job.Reserve=job.Provider.TryReserveAsync(job.Context,job.Record.Costs,CancellationToken.None);}catch{job.NeedsRecovery=true;job.Phase=6;}
        }
        public void Poll(int budget=100){if(retiring)return;int remaining=Math.Max(1,budget);foreach(var job in jobs.Values.Where(j=>j.Result==null).ToArray()){Step(job,ref remaining);if(remaining<=0)break;}}
        void Step(AsyncPlacementJob job,ref int budget,bool terminal=false){
            try{
                if(job.Phase==0){
                    if(job.Reserve==null||!job.Reserve.IsCompleted)return;
                    var result=job.Reserve.GetAwaiter().GetResult();job.Reserve=null;job.Reservation=result.Reservation;
                    if(job.Reservation!=null&&!Same(job.Reservation.Identity,job.Context)){job.Reservation=null;job.NeedsRecovery=true;job.Phase=6;return;}
                    if(result.State==AsyncReservationState.Failed){
                        job.Reservation=null;
                        if(job.Record.Decision==PlacementDecisionState.Commit){job.NeedsRecovery=true;job.Phase=6;return;}
                        if(job.Restored||job.Record.Cells.Any(c=>c.Created)){AbandonUnpaid(job);return;}
                        Finish(job,job.Cancelled?PlacementResultKind.Cancelled:PlacementResultKind.InsufficientResources);return;
                    }
                    if(result.State==AsyncReservationState.RolledBack){job.NeedsRecovery=false;job.Record.Payment=result.State;Save(job);job.Phase=4;return;}
                    if(result.State==AsyncReservationState.Committed){job.NeedsRecovery=false;job.Record.Payment=result.State;job.Record.Decision=PlacementDecisionState.Commit;Save(job);job.Phase=7;return;}
                    if(result.State!=AsyncReservationState.Reserved||job.Reservation==null){job.NeedsRecovery=true;job.Phase=job.Reservation==null?6:5;return;}
                    job.NeedsRecovery=false;job.Record.Payment=result.State;Save(job);job.Phase=job.Record.Decision==PlacementDecisionState.Abort?8:job.Record.Decision==PlacementDecisionState.Commit?2:1;
                    if(job.Phase==8)Rollback(job,job.Record.AbortKind);return;
                }
                if(job.Phase==1){
                    job.NeedsRecovery=false;
                    if(terminal||!job.Active){Rollback(job,PlacementResultKind.Cancelled);return;}
                    for(int i=0;i<job.Operations.Length&&budget>0;i++){
                        if(job.Record.Cells[i].Created)continue;
                        if(job.Restored){var probe=Probe(job,i);if(probe.Presence==PlacementPresence.Present)job.Placed[i]=probe.Handle;else if(probe.Presence==PlacementPresence.Unavailable||probe.Presence==PlacementPresence.Ambiguous){job.NeedsRecovery=true;return;}}
                        Save(job);
                        try{job.Placed[i]=job.Placed[i]??job.Backends[i].Create(job.Operations[i]);if(job.Placed[i]==null)throw new InvalidOperationException("No placement handle.");}
                        catch{Rollback(job,PlacementResultKind.CreateFailed);return;}
                        budget--;job.Record.Cells[i].Created=true;job.Record.Cells[i].NativeId=(job.Placed[i] as IDurablePlacementHandle)?.NativeId??"";Save(job);
                        if(!job.Active){Rollback(job,PlacementResultKind.Cancelled);return;}
                    }
                    if(job.Record.Cells.All(c=>c.Created))job.Phase=2;return;
                }
                if(job.Phase==2){
                    if(job.Record.Decision==PlacementDecisionState.Abort){Rollback(job,job.Record.AbortKind);return;}
                    if(job.Record.Decision==PlacementDecisionState.None){if(!job.Active){Rollback(job,PlacementResultKind.Cancelled);return;}job.Record.Decision=PlacementDecisionState.Commit;}
                    Save(job);job.Phase=3;job.Action=job.Reservation!.CommitAsync(CancellationToken.None);return;
                }
                if(job.Phase==3||job.Phase==8){
                    if(job.Action==null||!job.Action.IsCompleted)return;
                    var state=job.Action.GetAwaiter().GetResult();job.Action=null;AcceptStatus(job,state);return;
                }
                if(job.Phase==4){Settle(job,false,ref budget,terminal);return;}
                if(job.Phase==7){Settle(job,true,ref budget,terminal);return;}
                if(job.Phase==5){
                    if(job.Reservation==null){job.Phase=6;return;}
                    if(job.Action==null){job.Action=job.Reservation.GetStatusAsync(CancellationToken.None);return;}
                    if(!job.Action.IsCompleted)return;
                    var state=job.Action.GetAwaiter().GetResult();job.Action=null;
                    if(state==AsyncReservationState.Reserved){job.NeedsRecovery=false;job.Phase=job.Record.Decision==PlacementDecisionState.Commit?2:job.Record.Decision==PlacementDecisionState.Abort?8:job.Active?1:8;if(job.Phase==8)Rollback(job,job.Record.AbortKind);return;}
                    AcceptStatus(job,state);
                }
            }catch{job.NeedsRecovery=true;job.Action=null;job.Phase=job.Reservation==null?6:5;}
        }
        void AcceptStatus(AsyncPlacementJob job,AsyncReservationState state){
            if(state==AsyncReservationState.Committed||state==AsyncReservationState.RolledBack){job.NeedsRecovery=false;job.Record.Payment=state;if(state==AsyncReservationState.Committed)job.Record.Decision=PlacementDecisionState.Commit;Save(job);job.Phase=state==AsyncReservationState.Committed?7:4;}
            // The provider holds no reservation under this identity (an admission overflow reports RecoveryRequired, then Failed).
            else if(state==AsyncReservationState.Failed&&job.Record.Decision!=PlacementDecisionState.Commit)AbandonUnpaid(job);
            else{job.NeedsRecovery=true;job.Phase=5;}
        }
        /// <summary>Payment is committed only after a durable Commit decision, so without one nothing was paid: any created
        /// cell is removed and the provider's own ledger releases a hold it may still keep. The job then settles as unpaid.</summary>
        void AbandonUnpaid(AsyncPlacementJob job){
            job.Reservation=null;job.Action=null;job.Reserve=null;
            if(job.Record.Decision!=PlacementDecisionState.Abort){job.Record.Decision=PlacementDecisionState.Abort;job.Record.AbortKind=job.Cancelled?PlacementResultKind.Cancelled:PlacementResultKind.InsufficientResources;}
            job.Record.Payment=AsyncReservationState.Failed;Save(job);job.Phase=4;
        }
        void Rollback(AsyncPlacementJob job,PlacementResultKind kind){
            if(job.Record.Decision==PlacementDecisionState.Commit){job.Phase=5;job.NeedsRecovery=true;return;}
            job.Record.Decision=PlacementDecisionState.Abort;job.Record.AbortKind=kind;Save(job);job.Phase=8;job.Action=job.Reservation!.RollbackAsync(CancellationToken.None);
        }
        PlacementProbe Probe(AsyncPlacementJob job,int index)=>job.Backends[index] is IDurablePlacementBackend durable?durable.Probe(job.Record.Cells[index]):new PlacementProbe(job.Placed[index]==null?PlacementPresence.Ambiguous:PlacementPresence.Present,job.Placed[index]);
        void Settle(AsyncPlacementJob job,bool paid,ref int budget,bool terminal){
            job.NeedsRecovery=false;
            for(int i=0;i<job.Record.Cells.Count&&budget>0;i++){
                var cell=job.Record.Cells[i];if(cell.Settled)continue;
                // Completion (even one not yet flushed) means every cell already settled: never probe, re-create, confirm or compensate again.
                if(job.Record.Completed){cell.Settled=true;Save(job);continue;}
                if(cell.Retired){cell.Settled=true;Save(job);continue;}
                var probe=job.Backends[i] is IDurablePlacementBackend||job.Restored||job.Placed[i]==null?Probe(job,i):new PlacementProbe(PlacementPresence.Present,job.Placed[i]);
                if(probe.Presence==PlacementPresence.Unavailable||probe.Presence==PlacementPresence.Ambiguous&&(paid||cell.Created)){job.NeedsRecovery=true;return;}
                if(probe.Presence==PlacementPresence.Removed){cell.Settled=true;Save(job);continue;}
                if(paid&&probe.Presence==PlacementPresence.ProvenMissing){if(terminal){job.NeedsRecovery=true;return;}Save(job);job.Placed[i]=job.Backends[i].Create(job.Operations[i]);cell.Created=true;cell.NativeId=(job.Placed[i] as IDurablePlacementHandle)?.NativeId??"";Save(job);budget--;}
                else job.Placed[i]=probe.Handle;
                if(paid){if(job.Placed[i]==null){job.NeedsRecovery=true;return;}(job.Placed[i] as IConfirmedPlacementHandle)?.Confirm();}
                else job.Placed[i]?.Compensate();
                budget--;cell.Settled=true;Save(job);
            }
            if(job.Record.Cells.All(c=>c.Settled))Finish(job,paid?PlacementResultKind.Committed:job.Record.AbortKind==PlacementResultKind.Cancelled&&job.Record.Decision==PlacementDecisionState.Commit?PlacementResultKind.CommitFailed:job.Record.AbortKind);
        }
        void Archive(AsyncPlacementJob job){settled[job.Record.Key]=new SettledReceipt(job);jobs.Remove(job.Record.Key);}
        void Finish(AsyncPlacementJob job,PlacementResultKind kind){job.Record.Completed=true;Save(job);job.NeedsRecovery=false;job.Result=new PlacementResult(kind,job.Context.RequestId);try{job.Reservation?.Dispose();}catch{}job.Reservation=null;Archive(job);}
        public void CancelAll(){foreach(var job in jobs.Values.Where(j=>j.Result==null)){
            job.Cancel();job.CancellationSaved=false;
            if(job.Record.Decision==PlacementDecisionState.None){job.Record.Decision=PlacementDecisionState.Abort;job.Record.AbortKind=PlacementResultKind.Cancelled;}
            try{Save(job);job.CancellationSaved=true;}catch{job.NeedsRecovery=true;}
        }}
        public bool HasRecoveryRequired=>jobs.Values.Any(job=>job.Result==null&&job.NeedsRecovery);
        /// <summary>Called on the native owner's synchronization context. Drain exact outstanding tasks independently;
        /// no new reservations, creation, rebind or retry of uncertain outcomes is admitted after retirement.</summary>
        public Task RetireAsync(Func<bool> onOwnerContext){
            if(onOwnerContext==null)throw new ArgumentNullException(nameof(onOwnerContext));
            if(retirement!=null)return retirement;
            retiring=true;CancelAll();var pending=jobs.Values.Where(job=>job.Result==null).ToArray();
            foreach(var job in pending.Skip(4096))job.NeedsRecovery=true;
            return retirement=Task.WhenAll(pending.Take(4096).Select(job=>RetireJob(job,onOwnerContext)));
        }
        async Task RetireJob(AsyncPlacementJob job,Func<bool> onOwnerContext){
            // 128 stages cover the existing 10,000-cell maximum in batches of 100 plus payment stages.
            // A pending task is awaited without a timeout: discarding its late result would leak its hold.
            for(int stage=0;stage<128&&job.Result==null;stage++){
                if(!onOwnerContext()||!job.CancellationSaved||job.NeedsRecovery){job.NeedsRecovery=true;return;}
                int budget=100;Step(job,ref budget,true);
                if(job.Result!=null||job.NeedsRecovery)return;
                Task? pending=job.Phase==0?job.Reserve:job.Action;
                if(pending!=null&&!pending.IsCompleted){try{await pending;}catch{ /* Step observes the exact fault and retains recovery. */ }}
                else await Task.Yield();
            }
            if(job.Result==null)job.NeedsRecovery=true;
        }
        public void RecordRemoval(string actorId,long worldId,string sessionId,string requestId,string nativeId){
            foreach(var job in jobs.Values.Where(j=>j.Context.ActorId==actorId&&j.Context.WorldId==worldId&&j.Context.SessionId==sessionId))foreach(var cell in job.Record.Cells.Where(c=>c.RequestId==requestId&&c.NativeId==nativeId)){cell.Retired=true;Save(job);}
            foreach(var record in settled.Values.Select(receipt=>receipt.Record).Where(r=>r.ActorId==actorId&&r.WorldId==worldId&&r.SessionId==sessionId))foreach(var cell in record.Cells.Where(c=>c.RequestId==requestId&&c.NativeId==nativeId)){cell.Retired=true;journal?.Save(record);}
        }
    }
}
