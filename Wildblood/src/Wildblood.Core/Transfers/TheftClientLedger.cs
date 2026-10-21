// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from Timber8b9c5ad SiteDeliveryClientLedger; stable theft correlation metadata replaces site wire.
// Adapted from Burrow 14f92c83fdfced32a0439fac7164718306a09e6e src/Burrow.Plugin/SharedTransfers/TransferClientLedger.cs.
// Retains intent/debit/outcome/settlement recovery; product wire substituted, grid coordinates and partial transfer refund removed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


namespace ExtraPotions.Wildblood.Transfers {
    // Intent is deliberately a raw journal: Prepared means resumable work, not
    // an expiring inventory reservation. Only the two decided native phases use
    // DurableInventoryParticipant. No elapsed time can manufacture a refund.
    public sealed class TheftIntent {
        public readonly InventoryDecisionRecord Record;
        readonly byte[] request;
        public TheftRequest Request=>TheftMetadata.Decode(request);
        public string Root=>Record.Id;
        /// <summary>1: legacy native ask (no root on the wire, no retained owner outcome); 2: root-bound v2 ask.</summary>
        public int WireVersion{get;}
        internal TheftIntent(InventoryDecisionRecord record){
            Record=record;
            using(var stream=new MemoryStream(record.Metadata,false))using(var reader=new BinaryReader(stream,Encoding.UTF8)){
                WireVersion=reader.ReadInt32();
                if(WireVersion!=1&&WireVersion!=2)throw new InvalidDataException("Requester intent version.");
                request=reader.ReadBytes(checked((int)(stream.Length-stream.Position)));
            }
            var decoded=Request;
            if(decoded.Root!=record.Id||TheftMetadata.Hash(request)!=record.Fingerprint)throw new InvalidDataException("Requester intent identity.");
        }
    }
    public sealed class TheftClientLedger:IDisposable {
        readonly InventoryDecisionJournal intents,outcomes,debits,settlements;
        public TheftClientLedger(string directory){
            try{
                intents=new InventoryDecisionJournal(Path.Combine(directory,"intent.bin"));
                outcomes=new InventoryDecisionJournal(Path.Combine(directory,"outcome.bin"));
                debits=new InventoryDecisionJournal(Path.Combine(directory,"debit.bin"));
                settlements=new InventoryDecisionJournal(Path.Combine(directory,"settlement.bin"));
                // A phase exists only after immutable intent/outcome admission.
                // Flush the decision before constructing the participant, whose
                // ordinary reservation restart semantics abort Prepared holds.
                foreach(var journal in new[]{debits,settlements})foreach(var record in journal.Snapshot())
                    if(record.State==InventoryDecision.Prepared)journal.Transition(record,InventoryDecision.Decided);
                foreach(var intent in All){
                    if(intent.Record.State==InventoryDecision.Aborted)throw new InvalidDataException("Requester intent was aborted.");
                    if(intent.Record.State==InventoryDecision.Committed&&Settlement(intent)==null)throw new InvalidDataException("Requester settlement missing.");
                }
            }catch{Dispose();throw;}
        }
        public IReadOnlyList<TheftIntent> All=>intents.Snapshot().Select(r=>new TheftIntent(r)).ToArray();
        public IReadOnlyList<TheftIntent> Incomplete=>All.Where(i=>i.Record.State!=InventoryDecision.Committed).ToArray();
        public bool CanAdmit=>intents.CanAdmit&&outcomes.CanAdmit&&debits.CanAdmit&&settlements.CanAdmit&&Incomplete.Count==0;
        public TheftIntent Admit(TheftRequest request,byte[] before,byte[] escrow,int wireVersion=2){
            if(wireVersion!=1&&wireVersion!=2)throw new ArgumentOutOfRangeException(nameof(wireVersion));
            if(!CanAdmit)throw new InvalidOperationException("Requester already holds inventory or ledger is full.");
            var frozen=TheftMetadata.Encode(request);TheftMetadata.Decode(frozen);
            byte[] metadata;
            using(var stream=new MemoryStream()){
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(wireVersion);writer.Write(frozen);}
                metadata=stream.ToArray();
            }
            if(metadata.Length>TheftMetadata.Maximum)throw new InvalidDataException("Requester intent exceeds bound.");
            return new TheftIntent(intents.Prepare(request.Root,request.Root,TheftMetadata.Hash(frozen),0,before,escrow,metadata));
        }
        public bool HasOutcome(TheftIntent intent)=>outcomes.Find(intent.Root)!=null;
        public TheftReceipt Outcome(TheftIntent intent){
            var record=outcomes.Find(intent.Root);if(record==null)throw new InvalidOperationException("No authoritative outcome.");
            var receipt=TheftMetadata.Receipt(record.Metadata);
            if(!receipt.Matches(intent.Request)||receipt.Disposition==TheftDisposition.Recovery)throw new InvalidDataException("Unsettleable outcome.");
            return receipt;
        }
        public bool RecordOutcome(TheftIntent intent,TheftReceipt receipt){
            if(receipt==null||!receipt.Matches(intent.Request)||receipt.Disposition==TheftDisposition.Recovery)return false;
            var bytes=TheftMetadata.Encode(receipt);TheftMetadata.Receipt(bytes);
            var old=outcomes.Find(intent.Root);
            if(old!=null)return old.Metadata.SequenceEqual(bytes);
            if(intent.Record.State==InventoryDecision.Committed)return false;
            var record=outcomes.Prepare(intent.Root,intent.Root,TheftMetadata.Hash(bytes),0,Array.Empty<byte>(),Array.Empty<byte>(),bytes);
            outcomes.Transition(record,InventoryDecision.Decided);
            return true;
        }
        public InventoryDecisionRecord? Settlement(TheftIntent intent)=>settlements.Find(intent.Root);
        public void PrepareSettlement(TheftIntent intent,byte[] before,byte[] after){
            var receipt=Outcome(intent); // Typed product decision must be on disk first.
            if(Settlement(intent)!=null)throw new InvalidOperationException("Settlement image is already immutable.");
            var record=settlements.Prepare(intent.Root,"wildblood.theft.settle."+intent.Root,TheftMetadata.Hash(TheftMetadata.Encode(receipt)),0,before,after);
            settlements.Transition(record,InventoryDecision.Decided);
        }
        public InventoryDecision CommitDebit(TheftIntent intent,Func<InventoryDecisionJournal,IInventoryImageStore> storeFactory){
            var record=debits.Find(intent.Root);
            if(record==null){
                record=debits.Prepare(intent.Root,"wildblood.theft.debit."+intent.Root,intent.Record.Fingerprint,0,intent.Record.Before,intent.Record.After);
                debits.Transition(record,InventoryDecision.Decided);
            }
            return Commit(debits,record,storeFactory);
        }
        public InventoryDecision CommitSettlement(TheftIntent intent,Func<InventoryDecisionJournal,IInventoryImageStore> storeFactory){
            Outcome(intent);
            var record=Settlement(intent);if(record==null)return InventoryDecision.RecoveryRequired;
            var state=Commit(settlements,record,storeFactory);
            if(state==InventoryDecision.Committed){
                var outcome=outcomes.Find(intent.Root)??throw new InvalidDataException("Requester outcome missing.");
                if(outcome.State==InventoryDecision.Prepared)outcomes.Transition(outcome,InventoryDecision.Decided);
                outcomes.Transition(outcome,InventoryDecision.Committed);
                if(intent.Record.State==InventoryDecision.Prepared)intents.Transition(intent.Record,InventoryDecision.Decided);
                intents.Transition(intent.Record,InventoryDecision.Committed);
            }
            return state;
        }
        public InventoryDecision Reconcile(TheftIntent intent,Func<InventoryDecisionJournal,IInventoryImageStore> storeFactory){
            var final=Settlement(intent);
            // The final per-character receipt proves all earlier phases and must
            // not cause an older debit image to replace a newer saved inventory.
            if(final!=null&&storeFactory(settlements) is IInventoryCheckpointStore checkpoint&&checkpoint.ReceiptConfirmed(final.Source))
                return CommitSettlement(intent,storeFactory);
            var debit=CommitDebit(intent,storeFactory);
            return debit!=InventoryDecision.Committed?debit:final==null?InventoryDecision.Prepared:CommitSettlement(intent,storeFactory);
        }
        public bool FinalReceiptConfirmed(TheftIntent intent,Func<InventoryDecisionJournal,IInventoryImageStore> storeFactory){
            var final=Settlement(intent);
            return final!=null&&final.State==InventoryDecision.Committed&&storeFactory(settlements) is IInventoryCheckpointStore checkpoint&&checkpoint.ReceiptConfirmed(final.Source);
        }
        static InventoryDecision Commit(InventoryDecisionJournal journal,InventoryDecisionRecord record,Func<InventoryDecisionJournal,IInventoryImageStore> storeFactory){
            if(record.State==InventoryDecision.Prepared)journal.Transition(record,InventoryDecision.Decided);
            var store=storeFactory(journal);
            var participant=new DurableInventoryParticipant(journal,store,()=>0);
            var state=participant.Commit(record.Id);
            return state==InventoryDecision.Committed&&store is IInventoryCheckpointStore checkpoint&&!checkpoint.ReceiptConfirmed(record.Source)?InventoryDecision.RecoveryRequired:state;
        }
        public void Dispose(){settlements?.Dispose();debits?.Dispose();outcomes?.Dispose();intents?.Dispose();}
    }
}
