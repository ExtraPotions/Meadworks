using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ExtraPotions.Meadworks.Contracts;

namespace ExtraPotions.Burrow {
    internal readonly struct TransactionKey:IEquatable<TransactionKey> {
        readonly string actor,session,request;readonly long world;
        internal TransactionKey(AsyncResourceContext id){if(id==null)throw new ArgumentNullException(nameof(id));actor=id.ActorId;world=id.WorldId;session=id.SessionId;request=id.RequestId;}
        public bool Equals(TransactionKey other)=>actor==other.actor&&world==other.world&&session==other.session&&request==other.request;
        public override bool Equals(object? other)=>other is TransactionKey key&&Equals(key);
        public override int GetHashCode(){unchecked{return (((actor.GetHashCode()*397)^world.GetHashCode())*397^session.GetHashCode())*397^request.GetHashCode();}}
    }
    public sealed class ReservationRecord {
        public AsyncResourceContext Identity{get;}public AllocationPlan Plan{get;}public double Deadline{get;}public AsyncReservationState State{get;internal set;}
        internal ReservationRecord(AsyncResourceContext identity,AllocationPlan plan,double deadline){Identity=identity;Plan=plan;Deadline=deadline;State=AsyncReservationState.Reserved;}
    }
    public sealed class ReservationAdmission {
        public bool Accepted{get;}public string Code{get;}public ReservationRecord? Record{get;}
        internal ReservationAdmission(bool accepted,string code,ReservationRecord? record=null){Accepted=accepted;Code=code;Record=record;}
    }
    public sealed class ReservationBook {
        readonly Func<double> clock;
        readonly Dictionary<TransactionKey,ReservationRecord> records=new Dictionary<TransactionKey,ReservationRecord>();
        readonly Dictionary<StackKey,int> holds=new Dictionary<StackKey,int>();
        public IReadOnlyDictionary<StackKey,int> Holds=>new ReadOnlyDictionary<StackKey,int>(new Dictionary<StackKey,int>(holds));
        public ReservationBook(Func<double> monotonicClock){clock=monotonicClock??throw new ArgumentNullException(nameof(monotonicClock));}
        public ReservationAdmission TryReserve(AsyncResourceContext identity,IReadOnlyList<ResourceCost> costs,IReadOnlyList<SourceSnapshot> sources){
            var key=new TransactionKey(identity);var normalized=AllocationPlanner.Normalize(costs);Expire();
            if(records.TryGetValue(key,out var old)){
                var same=old.Plan.Costs.Count==normalized.Count&&old.Plan.Costs.Zip(normalized,(a,b)=>a.ItemId==b.ItemId&&a.Amount==b.Amount).All(s=>s);
                return new ReservationAdmission(same,same?"Existing":"IdentityConflict",same?old:null);
            }
            if(records.Count>=4096)return new ReservationAdmission(false,"LedgerCapacity");
            if(!AllocationPlanner.TryAllocate(normalized,sources,holds,out var plan))return new ReservationAdmission(false,"InsufficientMaterials");
            var record=new ReservationRecord(identity,plan!,clock()+10);
            foreach(var allocation in plan!.Allocations){holds.TryGetValue(allocation.Key,out var current);holds[allocation.Key]=checked(current+allocation.Units);}
            records.Add(key,record);return new ReservationAdmission(true,"Reserved",record);
        }
        public ReservationRecord? Find(AsyncResourceContext identity){Expire();return records.TryGetValue(new TransactionKey(identity),out var record)?record:null;}
        internal void Release(ReservationRecord record){foreach(var allocation in record.Plan.Allocations){var units=holds[allocation.Key]-allocation.Units;if(units==0)holds.Remove(allocation.Key);else holds[allocation.Key]=units;}}
        public AsyncReservationState Rollback(AsyncResourceContext identity){
            if(!records.TryGetValue(new TransactionKey(identity),out var record))return AsyncReservationState.Failed;
            if(record.State==AsyncReservationState.Reserved){Release(record);record.State=AsyncReservationState.RolledBack;}return record.State;
        }
        public void Expire(){var now=clock();foreach(var record in records.Values.Where(r=>r.State==AsyncReservationState.Reserved&&now>=r.Deadline).ToArray()){Release(record);record.State=AsyncReservationState.RolledBack;}}
    }
}
