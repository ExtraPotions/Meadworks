// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Shepherd.Take
{
    /// <summary>
    /// Request ids seen recently: at most <see cref="TakeProtocol.DedupeCapacity"/>, each for
    /// <see cref="TakeProtocol.DedupeWindowSeconds"/>; the oldest is evicted when full.
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

        /// <summary>Drops the id so a later request can be granted. Removes it from the queue as well as the map.</summary>
        public void Forget(TakeRequestId id)
        {
            if (!seen.Remove(id)) return;
            int count = order.Count;
            for (int i = 0; i < count; i++)
            {
                var item = order.Dequeue();
                if (!item.Id.Equals(id)) order.Enqueue(item);
            }
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
        /// <summary>This id already removed one item. Send the meal again and do not remove another.</summary>
        Resend,
        /// <summary>The id was seen within the dedupe window and no item was removed for it: nothing changes.</summary>
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
        /// <summary>Only a grant removes an item. A resend and every refusal leave the stack as it is.</summary>
        public static bool RemovesItem(TakeVerdict verdict) => verdict == TakeVerdict.Granted;

        /// <summary>A grant and a resend both send the meal.</summary>
        public static bool SendsMeal(TakeVerdict verdict) => verdict == TakeVerdict.Granted || verdict == TakeVerdict.Resend;
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
    /// The trough-owner side (design 3.3 step 3). A trough that is gone, or that this machine does not own, is not
    /// remembered. A refusal (disabled, disallowed, missing) is not remembered, so a later try can still grant.
    /// An id that already removed an item is a resend. Any other id already in the ring is a duplicate.
    /// </summary>
    public sealed class TakeServer
    {
        private readonly DedupeRing ring;
        private readonly HashSet<TakeRequestId> granted = new HashSet<TakeRequestId>();

        public TakeServer(DedupeRing? ring = null) { this.ring = ring ?? new DedupeRing(); }

        public TakeVerdict Decide(TakeRequest request, TroughFacts facts, double now)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!facts.Exists) return TakeVerdict.TroughGone;
            if (!facts.IsOwner) return TakeVerdict.NotOwner;
            if (granted.Contains(request.Id)) return TakeVerdict.Resend;
            if (!facts.Enabled) return TakeVerdict.TroughDisabled;
            if (facts.Disallowed) return TakeVerdict.ItemDisallowed;
            if (!facts.HasItem) return TakeVerdict.ItemMissing;
            if (!ring.CheckAndRemember(request.Id, now)) return TakeVerdict.Duplicate;
            return TakeVerdict.Granted;
        }

        /// <summary>Call after the item has left the trough. A later request with this id resends and does not remove again.</summary>
        public void Commit(TakeRequestId id) => granted.Add(id);

        /// <summary>Call when a grant cannot be delivered and the item is still in the trough. The same id may grant later.</summary>
        public void Release(TakeRequestId id)
        {
            granted.Remove(id);
            ring.Forget(id);
        }
    }
}
