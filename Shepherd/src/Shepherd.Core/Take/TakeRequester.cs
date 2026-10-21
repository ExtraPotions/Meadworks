// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Shepherd.Take
{
    /// <summary>What an animal's machine does with an arriving <c>shepherd.meal</c>.</summary>
    public enum MealDecision
    {
        /// <summary>Apply through <c>m_onConsumedItem</c>, consume effects and the <c>consume</c> trigger, credit experience.</summary>
        Apply,
        /// <summary>This machine already applied that request's meal: nothing happens.</summary>
        Duplicate,
        /// <summary>This machine does not own the animal: nothing happens (the routed RPC normally prevents this).</summary>
        NotOwner,
    }

    /// <summary>
    /// The animal-owner side (design 3.3 steps 1, 4 and 5). At most one pending request per animal; a pending request
    /// older than <see cref="TakeProtocol.ReplyTimeoutSeconds"/> is dropped and the animal searches again with a new id,
    /// never the same one. A meal is applied by whichever machine owns the animal when it arrives, at most once per
    /// request id on that machine: a reply to a changed owner, and a reply that arrives after the timeout, still feed the
    /// animal, so the item the trough owner removed is not wasted. The only lost item is a reply that never arrives.
    /// </summary>
    public sealed class TakeRequester
    {
        private readonly long issuer;
        private readonly double timeout;
        private readonly Dictionary<NetObjectId, (TakeRequest Request, double At)> pending = new Dictionary<NetObjectId, (TakeRequest, double)>();
        private readonly DedupeRing applied;
        private uint sequence;

        public TakeRequester(long issuer, double timeout = TakeProtocol.ReplyTimeoutSeconds)
        {
            if (!(timeout > 0)) throw new ArgumentOutOfRangeException(nameof(timeout));
            this.issuer = issuer;
            this.timeout = timeout;
            applied = new DedupeRing();
        }

        public int PendingCount => pending.Count;

        public bool HasPending(NetObjectId animal) => pending.ContainsKey(animal);

        /// <summary>
        /// A new request for the animal, or null while one is pending and not yet timed out. An overdue pending request
        /// is dropped first. The caller sends the result once and never again.
        /// </summary>
        public TakeRequest? TryBegin(NetObjectId animal, string itemName, double now)
        {
            if (!TakeCodec.IsItem(itemName)) throw new ArgumentException("Invalid item name.", nameof(itemName));
            if (pending.TryGetValue(animal, out var current))
            {
                if (now - current.At < timeout) return null;
                pending.Remove(animal);
            }
            if (sequence == uint.MaxValue) throw new InvalidOperationException("Request sequence exhausted for this session.");
            var request = new TakeRequest(new TakeRequestId(issuer, ++sequence), itemName, animal);
            pending[animal] = (request, now);
            return request;
        }

        /// <summary>Drops and returns every pending request that reached the timeout (logged at Verbose by the plugin).</summary>
        public IReadOnlyList<TakeRequest> Expire(double now)
        {
            var overdue = pending.Where(p => now - p.Value.At >= timeout).Select(p => p.Value.Request).ToList();
            foreach (var request in overdue) pending.Remove(request.Animal);
            return overdue;
        }

        /// <summary>Forgets the animal's pending request (it unloaded or changed owner away from this machine).</summary>
        public void Forget(NetObjectId animal) => pending.Remove(animal);

        public MealDecision OnMeal(MealMessage meal, NetObjectId animal, bool ownedHere, double now)
        {
            if (meal == null) throw new ArgumentNullException(nameof(meal));
            if (!ownedHere) return MealDecision.NotOwner;
            if (!applied.CheckAndRemember(meal.Id, now)) return MealDecision.Duplicate;
            if (pending.TryGetValue(animal, out var current) && current.Request.Id.Equals(meal.Id))
                pending.Remove(animal);
            return MealDecision.Apply;
        }
    }

    /// <summary>Where a message goes.</summary>
    public enum TakeRoute
    {
        /// <summary>Do not send: the target is gone or has no owner.</summary>
        None,
        /// <summary>This machine owns the target: handle it directly.</summary>
        Local,
        /// <summary>Invoke the RPC on the target's view; the game routes it to the current owner.</summary>
        Remote,
    }

    /// <summary>
    /// Ownership routing (design 3.1): container changes run on the container owner, meals on the animal owner. The
    /// protocol never claims ownership of a trough, a container or an animal.
    /// </summary>
    public static class TakeRouting
    {
        public const bool ClaimsOwnership = false;

        /// <summary>Owner 0 means no peer owns the object right now (nobody near): take nothing from it.</summary>
        public static TakeRoute ForTake(bool troughValid, long troughOwner, long localPeer) => Route(troughValid, troughOwner, localPeer);

        /// <summary>An animal that is gone or ownerless when the reply is due is the protocol's one lost item.</summary>
        public static TakeRoute ForMeal(bool animalValid, long animalOwner, long localPeer) => Route(animalValid, animalOwner, localPeer);

        private static TakeRoute Route(bool valid, long owner, long local)
        {
            if (!valid || owner == 0) return TakeRoute.None;
            return owner == local ? TakeRoute.Local : TakeRoute.Remote;
        }
    }
}
