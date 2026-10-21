using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Burrow {
    internal static class IdentityText {
        internal static void Validate(string value){if(string.IsNullOrWhiteSpace(value)||value.Length>128||value.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new ArgumentException("Invalid storage identity.");}
    }
    public sealed class ItemStackSnapshot {
        public string Id{get;}public string ItemId{get;}public int Units{get;}public string Revision{get;}public int Slot{get;}
        public ItemStackSnapshot(string id,string item,int units,string revision,int slot){
            IdentityText.Validate(id);IdentityText.Validate(item);IdentityText.Validate(revision);
            if(units<0||slot<0)throw new ArgumentException("Invalid native stack count or slot.");
            Id=id;ItemId=item;Units=units;Revision=revision;Slot=slot;
        }
    }
    public sealed class SourceSnapshot {
        public string Id{get;}public string Owner{get;}public bool IsPlayer{get;}public double Distance{get;}public bool Eligible{get;}public IReadOnlyList<ItemStackSnapshot> Stacks{get;}
        public SourceSnapshot(string id,string owner,bool player,double distance,bool eligible,IReadOnlyList<ItemStackSnapshot> stacks){
            IdentityText.Validate(id);IdentityText.Validate(owner);
            if(double.IsNaN(distance)||double.IsInfinity(distance)||distance<0||stacks==null)throw new ArgumentException("Invalid storage snapshot.");
            var copy=stacks.ToArray();if(copy.Any(s=>s==null)||copy.Select(s=>s.Id).Distinct(StringComparer.Ordinal).Count()!=copy.Length)throw new ArgumentException("Ambiguous stack identity.");
            Id=id;Owner=owner;IsPlayer=player;Distance=distance;Eligible=eligible;Stacks=Array.AsReadOnly(copy);
        }
        internal bool Same(SourceSnapshot other)=>Owner==other.Owner&&IsPlayer==other.IsPlayer&&Distance==other.Distance&&Eligible==other.Eligible&&Stacks.Count==other.Stacks.Count&&Stacks.OrderBy(s=>s.Id,StringComparer.Ordinal).Zip(other.Stacks.OrderBy(s=>s.Id,StringComparer.Ordinal),(a,b)=>a.Id==b.Id&&a.ItemId==b.ItemId&&a.Units==b.Units&&a.Revision==b.Revision&&a.Slot==b.Slot).All(s=>s);
    }
    public readonly struct StackKey:IEquatable<StackKey> {
        public string SourceId{get;}public string StackId{get;}
        public StackKey(string source,string stack){IdentityText.Validate(source);IdentityText.Validate(stack);SourceId=source;StackId=stack;}
        public bool Equals(StackKey other)=>SourceId==other.SourceId&&StackId==other.StackId;
        public override bool Equals(object? other)=>other is StackKey key&&Equals(key);
        public override int GetHashCode(){unchecked{return (SourceId.GetHashCode()*397)^StackId.GetHashCode();}}
    }
    public sealed class Allocation {
        public string SourceId{get;}public string Owner{get;}public string StackId{get;}public string ItemId{get;}public string Revision{get;}public int Units{get;}
        public StackKey Key=>new StackKey(SourceId,StackId);
        internal Allocation(SourceSnapshot source,ItemStackSnapshot stack,int units){SourceId=source.Id;Owner=source.Owner;StackId=stack.Id;ItemId=stack.ItemId;Revision=stack.Revision;Units=units;}
    }
    public sealed class AllocationPlan {
        public IReadOnlyList<ExtraPotions.Meadworks.Contracts.ResourceCost> Costs{get;}
        public IReadOnlyList<Allocation> Allocations{get;}
        internal AllocationPlan(IEnumerable<ExtraPotions.Meadworks.Contracts.ResourceCost> costs,IEnumerable<Allocation> allocations){Costs=Array.AsReadOnly(costs.ToArray());Allocations=Array.AsReadOnly(allocations.ToArray());}
    }
}
