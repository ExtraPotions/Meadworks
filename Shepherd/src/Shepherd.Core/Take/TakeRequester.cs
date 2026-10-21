// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

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
    /// The animal-owner side (design 3.3 steps 1, 4 and 5). At most one pending request per animal. After
    /// <see cref="TakeProtocol.ReplyTimeoutSeconds"/> the same id is returned again, so a trough that already removed
    /// one item does not remove a second. A meal is applied by whichever machine owns the animal when it arrives, at
    /// most once per request id on that machine: a reply to a changed owner, and a reply that arrives after the
    /// timeout, still feed the animal.
    /// </summary>
    public sealed class TakeRequester
    {
        private readonly long issuer;
        private readonly double timeout;
        private readonly Dictionary<NetObjectId, (TakeRequest Request, double At)> pending = new Dictionary<NetObjectId, (TakeRequest, double)>();
        private readonly HashSet<TakeRequestId> applied = new HashSet<TakeRequestId>();
        private uint sequence;

        public TakeRequester(long issuer, double timeout = TakeProtocol.ReplyTimeoutSeconds)
        {
            if (!(timeout > 0)) throw new ArgumentOutOfRangeException(nameof(timeout));
            this.issuer = issuer;
            this.timeout = timeout;
        }

        public int PendingCount => pending.Count;

        public bool HasPending(NetObjectId animal) => pending.ContainsKey(animal);

        /// <summary>True while a request is pending and still inside the reply window.</summary>
        public bool IsBlocking(NetObjectId animal, double now) =>
            pending.TryGetValue(animal, out var current) && now - current.At < timeout;

        /// <summary>
        /// A new request, or the same request once the reply window has passed. Null while one is pending inside the window.
        /// </summary>
        public TakeRequest? TryBegin(NetObjectId animal, string itemName, double now)
        {
            if (!TakeCodec.IsItem(itemName)) throw new ArgumentException("Invalid item name.", nameof(itemName));
            if (pending.TryGetValue(animal, out var current))
            {
                if (now - current.At < timeout) return null;
                pending[animal] = (current.Request, now);
                return current.Request;
            }
            if (sequence == uint.MaxValue) throw new InvalidOperationException("Request sequence exhausted for this session.");
            var request = new TakeRequest(new TakeRequestId(issuer, ++sequence), itemName, animal);
            pending[animal] = (request, now);
            return request;
        }

        /// <summary>Pending requests stay until a meal or <see cref="Forget"/>. Nothing is dropped on the timeout.</summary>
        public IReadOnlyList<TakeRequest> Expire(double now) => Array.Empty<TakeRequest>();

        /// <summary>Forgets the animal's pending request (it unloaded or changed owner away from this machine).</summary>
        public void Forget(NetObjectId animal) => pending.Remove(animal);

        public MealDecision OnMeal(MealMessage meal, NetObjectId animal, bool ownedHere, double now)
        {
            if (meal == null) throw new ArgumentNullException(nameof(meal));
            if (!ownedHere) return MealDecision.NotOwner;
            if (!applied.Add(meal.Id)) return MealDecision.Duplicate;
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

        /// <summary>An animal that is gone or has no owner is not sent a meal. The food stays in the trough.</summary>
        public static TakeRoute ForMeal(bool animalValid, long animalOwner, long localPeer) => Route(animalValid, animalOwner, localPeer);

        private static TakeRoute Route(bool valid, long owner, long local)
        {
            if (!valid || owner == 0) return TakeRoute.None;
            return owner == local ? TakeRoute.Local : TakeRoute.Remote;
        }
    }
}
