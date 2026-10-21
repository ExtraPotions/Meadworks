using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Contracts;

namespace ExtraPotions.Burrow {
    public static class AllocationPlanner {
        public static IReadOnlyList<ResourceCost> Normalize(IReadOnlyList<ResourceCost> costs){
            if(costs==null||costs.Count==0)throw new ArgumentException("At least one material cost is required.");
            var grouped=new SortedDictionary<string,int>(StringComparer.Ordinal);
            foreach(var cost in costs){if(cost==null||cost.Amount<=0)throw new ArgumentException("Invalid material cost.");IdentityText.Validate(cost.ItemId);grouped.TryGetValue(cost.ItemId,out var units);grouped[cost.ItemId]=checked(units+cost.Amount);}
            return Array.AsReadOnly(grouped.Select(p=>new ResourceCost(p.Key,p.Value)).ToArray());
        }
        static SourceSnapshot[] Sources(IReadOnlyList<SourceSnapshot> sources){
            if(sources==null)throw new ArgumentNullException(nameof(sources));
            var unique=new Dictionary<string,SourceSnapshot>(StringComparer.Ordinal);
            foreach(var source in sources){
                if(source==null)throw new ArgumentException("Missing storage snapshot.");
                if(unique.TryGetValue(source.Id,out var old)){if(!old.Same(source))throw new ArgumentException("Conflicting storage identity.");}else unique.Add(source.Id,source);
            }
            var stacks=new HashSet<string>(StringComparer.Ordinal);
            foreach(var source in unique.Values.Where(s=>s.Eligible))foreach(var stack in source.Stacks)if(!stacks.Add(stack.Id))throw new ArgumentException("Stack identity exists in multiple sources.");
            return unique.Values.Where(s=>s.Eligible).OrderBy(s=>s.IsPlayer?0:1).ThenBy(s=>s.Distance).ThenBy(s=>s.Id,StringComparer.Ordinal).ToArray();
        }
        static int Free(SourceSnapshot source,ItemStackSnapshot stack,IReadOnlyDictionary<StackKey,int> holds){
            if(holds==null)throw new ArgumentNullException(nameof(holds));
            holds.TryGetValue(new StackKey(source.Id,stack.Id),out var held);if(held<0)throw new ArgumentException("Negative material hold.");
            return held>=stack.Units?0:stack.Units-held;
        }
        public static int Available(string item,IReadOnlyList<SourceSnapshot> sources,IReadOnlyDictionary<StackKey,int> holds){
            IdentityText.Validate(item);int units=0;
            foreach(var source in Sources(sources))foreach(var stack in source.Stacks.Where(s=>s.ItemId==item))units=checked(units+Free(source,stack,holds));return units;
        }
        public static bool TryAllocate(IReadOnlyList<ResourceCost> costs,IReadOnlyList<SourceSnapshot> sources,IReadOnlyDictionary<StackKey,int> holds,out AllocationPlan? plan){
            var normalized=Normalize(costs);var eligible=Sources(sources);var allocations=new List<Allocation>();plan=null;
            foreach(var cost in normalized){
                var needed=cost.Amount;
                foreach(var source in eligible){
                    foreach(var stack in source.Stacks.Where(s=>s.ItemId==cost.ItemId).OrderBy(s=>s.Slot).ThenBy(s=>s.Id,StringComparer.Ordinal)){
                        var units=Math.Min(needed,Free(source,stack,holds));if(units>0){allocations.Add(new Allocation(source,stack,units));needed-=units;}if(needed==0)break;
                    }
                    if(needed==0)break;
                }
                if(needed!=0)return false;
            }
            plan=new AllocationPlan(normalized,allocations);return true;
        }
    }
}
