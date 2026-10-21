// Source baseline: Timber8b9c5adfbc346adca1e3acae110aeecc9b0fd068; namespace-only reuse.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Reused from Burrow 14f92c83fdfced32a0439fac7164718306a09e6e src/Burrow.Core/DurableInventoryParticipant.cs; namespace only.
using System;
using System.Linq;

namespace ExtraPotions.Wildblood.Transfers {
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
    public interface IInventoryRecoveryDiagnostics {void RecoveryRequired(string code);}
    /// <summary>Prepare never changes items. Commit is flushed before applying an image;
    /// recovery accepts only its recorded before or after image and never guesses a refund.</summary>
    public sealed class DurableInventoryParticipant {
        readonly InventoryDecisionJournal journal;readonly IInventoryImageStore store;readonly Func<double> clock;
        public DurableInventoryParticipant(InventoryDecisionJournal journal,IInventoryImageStore store,Func<double> monotonicClock){
            this.journal=journal??throw new ArgumentNullException(nameof(journal));this.store=store??throw new ArgumentNullException(nameof(store));clock=monotonicClock??throw new ArgumentNullException(nameof(monotonicClock));
            foreach(var record in journal.Snapshot().Where(r=>r.State==InventoryDecision.Prepared))journal.Transition(record,InventoryDecision.Aborted);
        }
        public bool IsHeld(string source)=>journal.Snapshot().Any(r=>r.Source==source&&(r.State==InventoryDecision.Prepared||r.State==InventoryDecision.Decided||r.State==InventoryDecision.Committed&&store is IInventoryCheckpointStore checkpoint&&!checkpoint.ReceiptConfirmed(source)));
        public InventoryDecision Prepare(string id,string source,string fingerprint,byte[] before,byte[] after,byte[]? metadata=null){
            Expire();var old=journal.Find(id);
            if(old!=null)return old.Source==source&&old.Fingerprint==fingerprint&&old.Before.SequenceEqual(before)&&old.After.SequenceEqual(after)&&old.Metadata.SequenceEqual(metadata??Array.Empty<byte>())?Result(old):InventoryDecision.Rejected;
            if(!journal.CanAdmit||IsHeld(source)||!store.IsBound(source)||!store.Capture(source).SequenceEqual(before))return InventoryDecision.Rejected;
            try{journal.Prepare(id,source,fingerprint,clock()+10,before,after,metadata);return InventoryDecision.Prepared;}catch{return InventoryDecision.Rejected;}
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
                    if(!store.Capture(record.Source).SequenceEqual(record.Before)){journal.Transition(record,InventoryDecision.Aborted);return InventoryDecision.Aborted;}
                    journal.Transition(record,InventoryDecision.Decided);
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
        public void Expire(){foreach(var record in journal.Snapshot().Where(r=>r.State==InventoryDecision.Prepared&&clock()>=r.Deadline))journal.Transition(record,InventoryDecision.Aborted);}
    }
}
