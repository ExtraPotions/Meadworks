// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Shepherd.Take
{
    /// <summary>
    /// Request ids seen recently: at most <see cref="TakeProtocol.DedupeCapacity"/>, each for
    /// <see cref="TakeProtocol.DedupeWindowSeconds"/>; the oldest is evicted when full. Requesters never resend an id,
    /// so the ring only guards against a repeated delivery.
    /// </summary>
    public sealed class DedupeRing
    {
        private readonly int capacity;
        private readonly double window;
        private readonly Queue<(TakeRequestId Id, double At)> order = new Queue<(TakeRequestId, double)>();
        private readonly Dictionary<TakeRequestId, double> seen = new Dictionary<TakeRequestId, double>();

        public DedupeRing(int capacity = TakeProtocol.DedupeCapacity, double window = TakeProtocol.DedupeWindowSeconds)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (!(window > 0)) throw new ArgumentOutOfRangeException(nameof(window));
            this.capacity = capacity;
            this.window = window;
        }

        public int Count => seen.Count;

        /// <summary>True when the id is new (and remembers it now); false when it was seen within the window.</summary>
        public bool CheckAndRemember(TakeRequestId id, double now)
        {
            Prune(now);
            if (seen.ContainsKey(id)) return false;
            while (order.Count >= capacity) seen.Remove(order.Dequeue().Id);
            order.Enqueue((id, now));
            seen[id] = now;
            return true;
        }

        private void Prune(double now)
        {
            while (order.Count > 0 && now - order.Peek().At >= window)
                seen.Remove(order.Dequeue().Id);
        }
    }

    /// <summary>What a trough (or container) owner decides for one take request.</summary>
    public enum TakeVerdict
    {
        /// <summary>Remove one item through the container's normal save path, then send <c>shepherd.meal</c>.</summary>
        Granted,
        /// <summary>The id was seen within the dedupe window: nothing changes.</summary>
        Duplicate,
        /// <summary>This machine does not own the trough (ownership moved in flight): nothing changes, nothing remembered.</summary>
        NotOwner,
        TroughGone,
        TroughDisabled,
        ItemMissing,
        ItemDisallowed,
    }

    public static class TakeVerdicts
    {
        /// <summary>Only a grant removes an item; every refusal changes nothing.</summary>
        public static bool RemovesItem(TakeVerdict verdict) => verdict == TakeVerdict.Granted;
    }

    /// <summary>What the trough owner knows at the moment a request arrives.</summary>
    public readonly struct TroughFacts
    {
        public TroughFacts(bool exists, bool isOwner, bool enabled, bool hasItem, bool disallowed)
        {
            Exists = exists; IsOwner = isOwner; Enabled = enabled; HasItem = hasItem; Disallowed = disallowed;
        }

        public bool Exists { get; }
        public bool IsOwner { get; }
        public bool Enabled { get; }
        public bool HasItem { get; }
        public bool Disallowed { get; }
    }

    /// <summary>
    /// The trough-owner side (design 3.3 step 3). Checks, in order: the trough exists, this machine owns it (a request
    /// that reached a non-owner is not remembered, so the real owner can still serve it), the id is new, the trough is
    /// enabled, the item is allowed and present. Every id that passes the owner check is remembered, refused or not,
    /// because a requester never resends one.
    /// </summary>
    public sealed class TakeServer
    {
        private readonly DedupeRing ring;

        public TakeServer(DedupeRing? ring = null) { this.ring = ring ?? new DedupeRing(); }

        public TakeVerdict Decide(TakeRequest request, TroughFacts facts, double now)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!facts.Exists) return TakeVerdict.TroughGone;
            if (!facts.IsOwner) return TakeVerdict.NotOwner;
            if (!ring.CheckAndRemember(request.Id, now)) return TakeVerdict.Duplicate;
            if (!facts.Enabled) return TakeVerdict.TroughDisabled;
            if (facts.Disallowed) return TakeVerdict.ItemDisallowed;
            if (!facts.HasItem) return TakeVerdict.ItemMissing;
            return TakeVerdict.Granted;
        }
    }
}
