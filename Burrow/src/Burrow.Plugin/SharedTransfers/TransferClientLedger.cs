using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExtraPotions.Burrow;

namespace Burrow.SharedTransfers {
    // Intent is deliberately a raw journal: Prepared means resumable work, not
    // an expiring inventory reservation. Only the two decided native phases use
    // DurableInventoryParticipant. No elapsed time can manufacture a refund.
    internal sealed class TransferClientIntent {
        internal readonly InventoryDecisionRecord Record;
        readonly byte[] request;
        internal int TargetX { get; }
        internal int TargetY { get; }
        internal TransferRequest Request=>TransferWire.Decode(request);
        internal string Root=>Record.Id;
        internal TransferClientIntent(InventoryDecisionRecord record){
            Record=record;
            using(var stream=new MemoryStream(record.Metadata,false))using(var reader=new BinaryReader(stream,Encoding.UTF8)){
                if(reader.ReadInt32()!=1)throw new InvalidDataException("Requester intent version.");
                TargetX=reader.ReadInt32();TargetY=reader.ReadInt32();
                request=reader.ReadBytes(checked((int)(stream.Length-stream.Position)));
            }
            var decoded=Request;
            if(decoded.Root!=record.Id||TransferWire.Hash(request)!=record.Fingerprint)throw new InvalidDataException("Requester intent identity.");
        }
    }
    internal sealed class TransferClientLedger:IDisposable {
        readonly InventoryDecisionJournal intents,outcomes,debits,settlements;
        internal TransferClientLedger(string directory){
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
        internal IReadOnlyList<TransferClientIntent> All=>intents.Snapshot().Select(r=>new TransferClientIntent(r)).ToArray();
        internal IReadOnlyList<TransferClientIntent> Incomplete=>All.Where(i=>i.Record.State!=InventoryDecision.Committed).ToArray();
        internal bool CanAdmit=>intents.CanAdmit&&outcomes.CanAdmit&&debits.CanAdmit&&settlements.CanAdmit&&Incomplete.Count==0;
        internal TransferClientIntent Admit(TransferRequest request,byte[] before,byte[] escrow,int targetX,int targetY){
            if(!CanAdmit)throw new InvalidOperationException("Requester already holds inventory or ledger is full.");
            var frozen=TransferWire.Encode(request);TransferWire.Decode(frozen);
            byte[] metadata;
            using(var stream=new MemoryStream()){
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(1);writer.Write(targetX);writer.Write(targetY);writer.Write(frozen);}
                metadata=stream.ToArray();
            }
            if(metadata.Length>TransferWire.Maximum)throw new InvalidDataException("Requester intent exceeds bound.");
            return new TransferClientIntent(intents.Prepare(request.Root,request.Root,TransferWire.Hash(frozen),0,before,escrow,metadata));
        }
        internal bool HasOutcome(TransferClientIntent intent)=>outcomes.Find(intent.Root)!=null;
        internal TransferReceipt Outcome(TransferClientIntent intent){
            var record=outcomes.Find(intent.Root);if(record==null)throw new InvalidOperationException("No authoritative outcome.");
            var receipt=TransferWire.Receipt(record.Metadata);
            if(!receipt.Matches(intent.Request)||receipt.Disposition==TransferDisposition.Recovery)throw new InvalidDataException("Unsettleable outcome.");
            return receipt;
        }
        internal bool RecordOutcome(TransferClientIntent intent,TransferReceipt receipt){
            if(receipt==null||!receipt.Matches(intent.Request)||receipt.Disposition==TransferDisposition.Recovery)return false;
            var bytes=TransferWire.Encode(receipt);TransferWire.Receipt(bytes);
            var old=outcomes.Find(intent.Root);
            if(old!=null)return old.Metadata.SequenceEqual(bytes);
            if(intent.Record.State==InventoryDecision.Committed)return false;
            var record=outcomes.Prepare(intent.Root,intent.Root,TransferWire.Hash(bytes),0,Array.Empty<byte>(),Array.Empty<byte>(),bytes);
            outcomes.Transition(record,InventoryDecision.Decided);
            return true;
        }
        internal InventoryDecisionRecord? Settlement(TransferClientIntent intent)=>settlements.Find(intent.Root);
        internal void PrepareSettlement(TransferClientIntent intent,byte[] before,byte[] after){
            var receipt=Outcome(intent); // Typed product decision must be on disk first.
            if(Settlement(intent)!=null)throw new InvalidOperationException("Settlement image is already immutable.");
            var record=settlements.Prepare(intent.Root,"shared.settle."+intent.Root,TransferWire.Hash(TransferWire.Encode(receipt)),0,before,after);
            settlements.Transition(record,InventoryDecision.Decided);
        }
        internal InventoryDecision CommitDebit(TransferClientIntent intent,Func<InventoryDecisionJournal,IInventoryImageStore> storeFactory){
            var record=debits.Find(intent.Root);
            if(record==null){
                record=debits.Prepare(intent.Root,"shared.debit."+intent.Root,intent.Record.Fingerprint,0,intent.Record.Before,intent.Record.After);
                debits.Transition(record,InventoryDecision.Decided);
            }
            return Commit(debits,record,storeFactory);
        }
        internal InventoryDecision CommitSettlement(TransferClientIntent intent,Func<InventoryDecisionJournal,IInventoryImageStore> storeFactory){
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
        internal InventoryDecision Reconcile(TransferClientIntent intent,Func<InventoryDecisionJournal,IInventoryImageStore> storeFactory){
            var final=Settlement(intent);
            // The final per-character receipt proves all earlier phases and must
            // not cause an older debit image to replace a newer saved inventory.
            if(final!=null&&storeFactory(settlements) is IInventoryCheckpointStore checkpoint&&checkpoint.ReceiptConfirmed(final.Source))
                return CommitSettlement(intent,storeFactory);
            var debit=CommitDebit(intent,storeFactory);
            return debit!=InventoryDecision.Committed?debit:final==null?InventoryDecision.Prepared:CommitSettlement(intent,storeFactory);
        }
        internal bool FinalReceiptConfirmed(TransferClientIntent intent,Func<InventoryDecisionJournal,IInventoryImageStore> storeFactory){
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
        internal static int[] RefundCounts(int[] offered,IReadOnlyDictionary<int,int> accepted){
            var refunds=(int[])offered.Clone();
            if(offered.Any(n=>n<=0))throw new InvalidDataException("Invalid escrow count.");
            foreach(var entry in accepted){
                if(entry.Key<0||entry.Key>=offered.Length||entry.Value<0||entry.Value>offered[entry.Key])throw new InvalidDataException("Invalid accepted count.");
                refunds[entry.Key]-=entry.Value;
            }
            return refunds;
        }
        public void Dispose(){settlements?.Dispose();debits?.Dispose();outcomes?.Dispose();intents?.Dispose();}
    }
}
