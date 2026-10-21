using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Reuses <see cref="MischiefGate"/> decisions and <see cref="BlockedOption"/> statuses while their inputs are unchanged.
    /// Products ask the gate from native hooks several times a frame (Hex's carry-weight postfix alone runs it about 20
    /// times), and each fresh answer allocated a decision, an authority decision and the local platform ID. An entry is
    /// reused only for the same facts and policy objects (both immutable; <see cref="AuthorityMemo"/> returns the same
    /// facts object while the session is unchanged, and every policy change builds a new policy) and, on a remote
    /// client, the same platform ID. Main thread only.
    /// </summary>
    public sealed class MischiefMemo {
        const int MaxEntries=512;
        const int CategoryCount=6;
        sealed class Slot { public AuthorityFacts? Facts;public MischiefPolicy? Policy;public string? PlatformId;public MischiefDecision? Decision; }
        readonly Dictionary<string,Slot?[]> decisions=new Dictionary<string,Slot?[]>(StringComparer.Ordinal);
        readonly Dictionary<(string Product,string Cheat),(MischiefDecision Decision,BlockedOption Status)> statuses=new Dictionary<(string,string),(MischiefDecision,BlockedOption)>();

        /// <summary>The gate's decision; <paramref name="platformId"/> is read only for a remote client, the only role the policy applies to.</summary>
        public MischiefDecision Decide(string productId,MischiefCategory category,AuthorityFacts facts,MischiefPolicy policy,Func<string?> platformId){
            if((uint)category>=CategoryCount||productId==null||facts==null||policy==null||platformId==null)
                return MischiefGate.Decide(category,facts!,policy!,platformId?.Invoke());
            string? id=facts.Role==SessionRole.RemoteClient?platformId():null;
            if(!decisions.TryGetValue(productId,out var slots)){
                if(decisions.Count>=MaxEntries)decisions.Clear();
                decisions[productId]=slots=new Slot?[CategoryCount];
            }
            var slot=slots[(int)category]??(slots[(int)category]=new Slot());
            if(slot.Decision!=null&&ReferenceEquals(slot.Facts,facts)&&ReferenceEquals(slot.Policy,policy)&&string.Equals(slot.PlatformId,id,StringComparison.Ordinal))return slot.Decision;
            var decision=MischiefGate.Decide(category,facts,policy,id);
            slot.Facts=facts;slot.Policy=policy;slot.PlatformId=id;slot.Decision=decision;
            return decision;
        }

        /// <summary>The cheat's status for a decision; the same status object while the decision object is the same.</summary>
        public BlockedOption Query(string productId,string cheat,MischiefDecision decision){
            if(productId==null||cheat==null||decision==null)return BlockedOption.From(productId!,cheat!,decision!);
            var key=(productId,cheat);
            if(statuses.TryGetValue(key,out var known)&&ReferenceEquals(known.Decision,decision))return known.Status;
            // Built first so an invalid name still throws and is never cached.
            var status=BlockedOption.From(productId,cheat,decision);
            if(statuses.Count>=MaxEntries)statuses.Clear();
            statuses[key]=(decision,status);
            return status;
        }
    }
}
