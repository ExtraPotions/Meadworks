// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// The completed request ids an authority router keeps (RequestRouter, PeerCommandRouter). A completed id is held for
    /// <see cref="Retention"/> seconds, so a duplicate or replay of it inside that window returns the stored outcome and never
    /// executes again; after the window it is pruned. Clients stop sending Request, Status and Cancel frames for an id
    /// ten seconds after they sent it, so the window outlives every legitimate retry. Under pressure an id may leave
    /// earlier, but never before <see cref="MinimumRetention"/>: an actor holding <see cref="PerActor"/> younger ids is
    /// refused (only that actor), and a ledger at <see cref="Capacity"/> with no id past the minimum refuses new work.
    /// Unresolved work (pending, recovery required) is never held or evicted here. Ids are actor-scoped, so an id that
    /// was pruned can only be sent again by the same authenticated actor, which could equally send a fresh id.
    /// </summary>
    public sealed class RequestLedger {
        public const double Retention=120;
        public const double MinimumRetention=30;
        public const int Capacity=4096;
        public const int PerActor=1024;

        readonly LinkedList<(RequestIdentity Id,double At)> order=new LinkedList<(RequestIdentity,double)>();
        readonly Dictionary<RequestIdentity,LinkedListNode<(RequestIdentity Id,double At)>> nodes=new Dictionary<RequestIdentity,LinkedListNode<(RequestIdentity,double)>>();
        readonly Dictionary<string,int> perActor=new Dictionary<string,int>(StringComparer.Ordinal);

        public int Count=>nodes.Count;
        public int CountFor(string actor)=>actor!=null&&perActor.TryGetValue(actor,out var n)?n:0;
        public bool Holds(RequestIdentity id)=>id!=null&&nodes.ContainsKey(id);

        /// <summary>The id reached a terminal outcome at <paramref name="at"/> (monotonic seconds). A later call keeps the first time.</summary>
        public void Completed(RequestIdentity id,double at){
            if(id==null)throw new ArgumentNullException(nameof(id));
            if(nodes.ContainsKey(id))return;
            nodes[id]=order.AddLast((id,at));
            perActor[id.ActorId]=CountFor(id.ActorId)+1;
        }

        /// <summary>The router dropped the id itself (session reset, old epoch).</summary>
        public void Forget(RequestIdentity id){
            if(id==null||!nodes.TryGetValue(id,out var node))return;
            Remove(node);
        }

        /// <summary>Prunes ids whose window has passed; each pruned id is added to <paramref name="removed"/> for the router to drop.</summary>
        public void Expire(double now,ICollection<RequestIdentity> removed){
            while(order.First!=null&&now-order.First.Value.At>=Retention){var id=order.First.Value.Id;Remove(order.First);removed?.Add(id);}
        }

        /// <summary>
        /// Room for one more request of <paramref name="actor"/> while <paramref name="unresolved"/> other entries are retained
        /// by the router. Expires old ids, then evicts ids past <see cref="MinimumRetention"/> as needed (the actor's own
        /// oldest for its quota, the globally oldest for capacity). False when no room can be made; nothing is evicted then
        /// beyond what had expired.
        /// </summary>
        public bool Admit(string actor,int unresolved,double now,ICollection<RequestIdentity> removed){
            Expire(now,removed);
            while(CountFor(actor)>=PerActor){
                var oldest=OldestOf(actor);
                if(oldest==null||now-oldest.Value.At<MinimumRetention)return false;
                removed?.Add(oldest.Value.Id);Remove(oldest);
            }
            while(Count+Math.Max(0,unresolved)>=Capacity){
                var oldest=order.First;
                if(oldest==null||now-oldest.Value.At<MinimumRetention)return false;
                removed?.Add(oldest.Value.Id);Remove(oldest);
            }
            return true;
        }

        LinkedListNode<(RequestIdentity Id,double At)>? OldestOf(string actor){
            for(var node=order.First;node!=null;node=node.Next)if(node.Value.Id.ActorId==actor)return node;
            return null;
        }

        void Remove(LinkedListNode<(RequestIdentity Id,double At)> node){
            var id=node.Value.Id;
            order.Remove(node);nodes.Remove(id);
            int n=CountFor(id.ActorId)-1;
            if(n<=0)perActor.Remove(id.ActorId);else perActor[id.ActorId]=n;
        }
    }
}
