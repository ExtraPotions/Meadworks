// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using ExtraPotions.Wildblood.Transfers;

namespace ExtraPotions.Wildblood.Raids {
    // Proofs use the existing journal format, never an inventory participant.
    public sealed class RaidStakeCheckpoints {
        readonly InventoryDecisionJournal journal;
        readonly Func<string,InventoryDecisionRecord?> intent;
        public RaidStakeCheckpoints(InventoryDecisionJournal journal,Func<string,InventoryDecisionRecord?> intent){
            this.journal=journal??throw new ArgumentNullException(nameof(journal));this.intent=intent??throw new ArgumentNullException(nameof(intent));
            foreach(var proof in journal.Snapshot()){
                var receipt=RaidStakeReceipt.Decode(proof.Metadata);var original=Intent(receipt.EventRoot,out var plan);Validate(proof,original,plan,receipt);
            }
            foreach(var root in journal.Snapshot().Select(r=>RaidStakeReceipt.Decode(r.Metadata).EventRoot).Distinct())Latest(root);
        }
        InventoryDecisionRecord Intent(string root,out RaidStakePlan plan){
            var record=intent(root)??throw new InvalidDataException("Raid checkpoint has no authority decision.");
            plan=RaidStakePlan.Decode(record.Metadata);
            RaidStakePlan.Verify(record,plan.WorldUid,plan.ChestToken,root,plan.RequestId,record.Fingerprint);
            if(record.State!=InventoryDecision.Decided&&record.State!=InventoryDecision.Committed)throw new InvalidDataException("Raid checkpoint requires a decided authority intent.");
            return record;
        }
        static string Id(string root,RaidStakeStage stage)=>root+":"+(byte)stage;
        static string Source(InventoryDecisionRecord intent,RaidStakeStage stage)=>intent.Source+":proof:"+(byte)stage;
        static void Validate(InventoryDecisionRecord proof,InventoryDecisionRecord original,RaidStakePlan plan,RaidStakeReceipt receipt){
            receipt.Verify(plan,original.Fingerprint);
            if(proof.Id!=Id(plan.EventRoot,receipt.Stage)||proof.Source!=Source(original,receipt.Stage)||proof.Fingerprint!=original.Fingerprint
                ||!proof.Before.SequenceEqual(original.Before)||!proof.After.SequenceEqual(original.After)
                ||proof.State==InventoryDecision.Aborted)throw new InvalidDataException("Conflicting raid checkpoint.");
        }
        void Finish(InventoryDecisionRecord proof){
            if(proof.State==InventoryDecision.Prepared)journal.Transition(proof,InventoryDecision.Decided);
            if(proof.State==InventoryDecision.Decided)journal.Transition(proof,InventoryDecision.Committed);
            if(proof.State!=InventoryDecision.Committed)throw new InvalidDataException("Uncommitted raid checkpoint.");
        }
        public RaidStakeReceipt? Latest(string root){
            var original=Intent(root,out var plan);RaidStakeReceipt? last=null;bool gap=false;
            for(var stage=RaidStakeStage.Debited;stage<=RaidStakeStage.Terminal;stage++){
                var proof=journal.Find(Id(root,stage));if(proof==null){gap=true;continue;}
                if(gap)throw new InvalidDataException("Raid checkpoint skips a stage.");
                var receipt=RaidStakeReceipt.Decode(proof.Metadata);Validate(proof,original,plan,receipt);
                if(receipt.Stage!=stage)throw new InvalidDataException("Raid checkpoint stage mismatch.");
                if(last!=null)last.Merge(receipt,plan,original.Fingerprint);
                Finish(proof);last=receipt;
            }
            return last;
        }
        public RaidStakeReceipt Record(string root,RaidStakeStage stage,byte endReason=0,long endedAtMs=0){
            var original=Intent(root,out var plan);
            var next=new RaidStakeReceipt(plan.WorldUid,plan.ChestToken,root,plan.RequestId,original.Fingerprint,stage,endReason,endedAtMs);
            var last=Latest(root);var existing=journal.Find(Id(root,stage));
            if(existing!=null){var receipt=RaidStakeReceipt.Decode(existing.Metadata);receipt.Merge(next,plan,original.Fingerprint);return receipt;}
            if(last==null){if(stage!=RaidStakeStage.Debited)throw new InvalidDataException("Raid checkpoint must begin with debit.");}
            else last.Merge(next,plan,original.Fingerprint);
            var proof=journal.Prepare(Id(root,stage),Source(original,stage),original.Fingerprint,0,original.Before,original.After,next.Encode());
            Finish(proof);return next;
        }
    }
}
