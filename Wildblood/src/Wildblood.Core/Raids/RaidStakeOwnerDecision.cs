// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Linq;
using ExtraPotions.Wildblood.Transfers;

namespace ExtraPotions.Wildblood.Raids {
    public static class RaidStakeOwnerDecision {
        public static void AbortUndecided(InventoryDecisionJournal owner,InventoryDecisionJournal authority,long world,string chest,string root,byte[] current,bool receiptConfirmed){
            var decision=authority.Find(root)??throw new InvalidDataException("Missing retained raid authority intent.");
            var plan=RaidStakePlan.Decode(decision.Metadata);RaidStakePlan.Verify(decision,world,chest,root,plan.RequestId,decision.Fingerprint);
            var record=owner.Find(root)??throw new InvalidDataException("Missing raid owner intent.");
            RaidStakePlan.Verify(record,world,chest,root,plan.RequestId,decision.Fingerprint);
            if(record.State!=InventoryDecision.Prepared||(decision.State!=InventoryDecision.Prepared&&decision.State!=InventoryDecision.Aborted)
                ||receiptConfirmed||!current.SequenceEqual(record.Before)||!record.Metadata.SequenceEqual(decision.Metadata)
                ||!record.Before.SequenceEqual(decision.Before)||!record.After.SequenceEqual(decision.After))throw new InvalidDataException("Cannot abort uncertain raid debit.");
            if(decision.State==InventoryDecision.Prepared)authority.Transition(decision,InventoryDecision.Aborted);
            owner.Transition(record,InventoryDecision.Aborted);
        }
        // Run before constructing an inventory participant: its startup aborts Prepared records.
        public static InventoryDecisionRecord Accept(InventoryDecisionJournal owner,InventoryDecisionRecord authority,long world,string chest){
            if(authority==null)throw new InvalidDataException("Missing retained raid authority decision.");
            var plan=RaidStakePlan.Decode(authority.Metadata);
            RaidStakePlan.Verify(authority,world,chest,plan.EventRoot,plan.RequestId,authority.Fingerprint);
            if(authority.State!=InventoryDecision.Decided&&authority.State!=InventoryDecision.Committed)throw new InvalidDataException("Authority has not decided raid debit.");
            var record=owner.Find(authority.Id);
            if(record==null)record=owner.Prepare(authority.Id,authority.Source,authority.Fingerprint,authority.Deadline,authority.Before,authority.After,authority.Metadata);
            RaidStakePlan.Verify(record,world,chest,plan.EventRoot,plan.RequestId,authority.Fingerprint);
            if(!record.Metadata.SequenceEqual(authority.Metadata)||!record.Before.SequenceEqual(authority.Before)||!record.After.SequenceEqual(authority.After)
                ||record.State==InventoryDecision.Aborted)throw new InvalidDataException("Owner and authority raid decisions conflict.");
            if(record.State==InventoryDecision.Prepared)owner.Transition(record,InventoryDecision.Decided);
            return record;
        }
    }
}
