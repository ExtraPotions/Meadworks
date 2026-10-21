using System;
using System.Linq;

namespace ExtraPotions.Burrow {
    public interface IInventoryImageStore {
        bool IsBound(string source);
        byte[] Capture(string source);
        void Apply(string source,byte[] image);
        void Save(string source);
    }
    public interface IInventoryCheckpointStore {
        bool ReceiptConfirmed(string source);
    }
    public interface IInventoryDecisionObserver {void DecisionFlushed(InventoryDecisionRecord record);}
    /// <summary>A store whose payments change only some positions of their sources. When a source changed since the hold
    /// only at positions the payment leaves alone (a tool's durability, an item picked up into another slot), the payment
    /// is moved onto the live source instead of being refused: every paid position must still hold exactly the reserved
    /// item and count, and both rebased images must load back exactly. Nothing changes until the decision is durable.</summary>
    public interface IInventoryImageRebase {
        bool TryRebase(string source,out byte[] before,out byte[] after);
        void Rebind(string source,byte[] before,byte[] after);
    }
    public interface IInventoryRecoveryDiagnostics {void RecoveryRequired(string code);}
    /// <summary>Prepare never changes items. Commit is flushed before applying an image;
    /// recovery accepts only its recorded before or after image and never guesses a refund.</summary>
    public sealed class DurableInventoryParticipant {
        readonly InventoryDecisionJournal journal;readonly IInventoryImageStore store;readonly Func<double> clock;
        public DurableInventoryParticipant(InventoryDecisionJournal journal,IInventoryImageStore store,Func<double> monotonicClock){
            this.journal=journal??throw new ArgumentNullException(nameof(journal));this.store=store??throw new ArgumentNullException(nameof(store));clock=monotonicClock??throw new ArgumentNullException(nameof(monotonicClock));
            foreach(var record in journal.Snapshot().Where(r=>r.State==InventoryDecision.Prepared))journal.Transition(record,InventoryDecision.Aborted);
            // Nothing was removed, so the next session must not take the gear without a salvage payment.
            foreach(var record in journal.Snapshot().Where(r=>r.State==InventoryDecision.Decided&&ChestItemTake.IsSalvage(r)&&!Applied(r.Id)))journal.Transition(record,InventoryDecision.Aborted);
        }
        /// <summary>The live source already holds this record's after-image.</summary>
        public bool Applied(string id){
            var record=journal.Find(id);
            if(record==null||!store.IsBound(record.Source))return false;
            try{return store.Capture(record.Source).SequenceEqual(record.After);}catch{return false;}
        }
        public bool IsHeld(string source)=>journal.Snapshot().Any(r=>r.Source==source&&(r.State==InventoryDecision.Prepared||r.State==InventoryDecision.Decided||r.State==InventoryDecision.Committed&&store is IInventoryCheckpointStore checkpoint&&!checkpoint.ReceiptConfirmed(source)));
        public InventoryDecision Prepare(string id,string source,string fingerprint,byte[] before,byte[] after,byte[]? metadata=null,double holdSeconds=HoldSeconds){
            Expire();var old=journal.Find(id);
            if(old!=null)return old.Source==source&&old.Fingerprint==fingerprint&&old.Before.SequenceEqual(before)&&old.After.SequenceEqual(after)&&old.Metadata.SequenceEqual(metadata??Array.Empty<byte>())?Result(old):InventoryDecision.Rejected;
            if(!journal.CanAdmit||IsHeld(source)||!store.IsBound(source)||!store.Capture(source).SequenceEqual(before))return InventoryDecision.Rejected;
            if(double.IsNaN(holdSeconds)||holdSeconds<=0||holdSeconds>MaxHoldSeconds)return InventoryDecision.Rejected;
            try{journal.Prepare(id,source,fingerprint,clock()+holdSeconds,before,after,metadata);return InventoryDecision.Prepared;}catch{return InventoryDecision.Rejected;}
        }
        public InventoryDecision Status(string id){Expire();var record=journal.Find(id);return record==null?InventoryDecision.Rejected:Result(record);}
        InventoryDecision Result(InventoryDecisionRecord record)=>record.State==InventoryDecision.Decided||record.State==InventoryDecision.Committed&&store is IInventoryCheckpointStore checkpoint&&!checkpoint.ReceiptConfirmed(record.Source)?InventoryDecision.RecoveryRequired:record.State;
        public InventoryDecision Commit(string id){
            Expire();var record=journal.Find(id);if(record==null)return InventoryDecision.Rejected;
            if(record.State==InventoryDecision.Aborted||record.State==InventoryDecision.Committed&&Result(record)==InventoryDecision.Committed)return record.State;
            try{
                if(record.State==InventoryDecision.Prepared){
                    // A changed source before the decision is a rejection, with no deduction.
                    if(!store.IsBound(record.Source)){journal.Transition(record,InventoryDecision.Decided);return Recovery("BindingUnavailable");}
                    if(store.Capture(record.Source).SequenceEqual(record.Before))journal.Transition(record,InventoryDecision.Decided);
                    else if(store is IInventoryImageRebase rebase&&rebase.TryRebase(record.Source,out var before,out var after)){
                        // The rebased images become the durable decision; recovery then accepts only those.
                        rebase.Rebind(record.Source,before,after);journal.Decide(record,before,after);
                    }
                    else{journal.Transition(record,InventoryDecision.Aborted);return InventoryDecision.Aborted;}
                }
                if(!store.IsBound(record.Source))return Recovery("BindingUnavailable");
                (store as IInventoryDecisionObserver)?.DecisionFlushed(record);
                byte[] current=store.Capture(record.Source);
                if(current.SequenceEqual(record.Before))store.Apply(record.Source,record.After);
                else if(!current.SequenceEqual(record.After))return Recovery("InventoryImageConflict");
                if(!store.IsBound(record.Source)||!store.Capture(record.Source).SequenceEqual(record.After))return Recovery("ImageNotApplied");
                store.Save(record.Source);journal.Transition(record,InventoryDecision.Committed);return InventoryDecision.Committed;
            }catch(Exception error){return Recovery("Persistence"+error.GetType().Name);}
        }
        InventoryDecision Recovery(string code){(store as IInventoryRecoveryDiagnostics)?.RecoveryRequired(code);return InventoryDecision.RecoveryRequired;}
        public InventoryDecision Rollback(string id){var record=journal.Find(id);if(record==null)return InventoryDecision.Rejected;if(record.State==InventoryDecision.Prepared){try{journal.Transition(record,InventoryDecision.Aborted);}catch{return InventoryDecision.RecoveryRequired;}}return Result(record);}
        /// <summary>How long an undecided hold lasts by default, and the longest one may last.</summary>
        public const double HoldSeconds=10,MaxHoldSeconds=60;
        /// <summary>A payment of many units is usually staged piece by piece (a planting grid places one cell per unit) before
        /// its commit, so its hold lasts a quarter second longer per unit, up to <see cref="MaxHoldSeconds"/>.</summary>
        public static double HoldFor(long units)=>Math.Min(MaxHoldSeconds,HoldSeconds+Math.Max(0L,Math.Min(units,1000L))*0.25);
        /// <summary>Undecided holds end at their deadline. Deadlines are on the monotonic clock of the run that wrote them,
        /// which restarts at zero with the game: opening the journal already aborts every undecided hold of an earlier run,
        /// and a deadline further ahead than a fresh hold could be (another run's clock) also ends here. Aborting an
        /// undecided hold never changes items: Prepare deducts nothing, so nothing is lost or charged twice.</summary>
        public void Expire(){double now=clock();foreach(var record in journal.Snapshot().Where(r=>r.State==InventoryDecision.Prepared&&(now>=r.Deadline||r.Deadline-now>MaxHoldSeconds)))journal.Transition(record,InventoryDecision.Aborted);}
    }
}
