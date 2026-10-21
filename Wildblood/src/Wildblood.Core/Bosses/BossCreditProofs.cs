// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: server-retained proof for late boss credit. The authority records what it validated while the
// source was alive (source, its owner, the boss it belongs to) for a short grace window, so a credit that arrives after
// the source was removed or handed over can still be checked against that retained proof instead of being guessed. A
// relayed credit that reaches a machine no longer owning the boss may bounce back once per relay; the authority
// accepts a bounce only from the exact peer it relayed to, so a credit is applied at most once.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Wildblood.Bosses
{
    /// <summary>A Unity-free native object identity (ZDOID user and id).</summary>
    public readonly struct NativeRef : IEquatable<NativeRef>
    {
        public NativeRef(long user, uint id) { User = user; Id = id; }
        public long User { get; }
        public uint Id { get; }
        public bool Equals(NativeRef other) => User == other.User && Id == other.Id;
        public override bool Equals(object? obj) => obj is NativeRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(User, Id);
        public override string ToString() => User.ToString(CultureInfo.InvariantCulture) + ":" + Id.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class BossCreditProofs
    {
        /// <summary>At most this many bounces per credit (server to a stale owner and back).</summary>
        public const int MaxHops = 2;

        readonly double grace;
        readonly int capacity;
        readonly Dictionary<NativeRef, (long Owner, NativeRef Boss, double Seen)> proofs = new Dictionary<NativeRef, (long, NativeRef, double)>();
        readonly List<(string Key, long Peer, double At)> relays = new List<(string, long, double)>();

        public BossCreditProofs(double grace = 10, int capacity = 512)
        {
            if (grace <= 0 || double.IsNaN(grace) || double.IsInfinity(grace)) throw new ArgumentOutOfRangeException(nameof(grace));
            if (capacity < 1 || capacity > 65536) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.grace = grace; this.capacity = capacity;
        }

        public int Count => proofs.Count;

        public static bool MayBounce(int hops) => hops >= 0 && hops < MaxHops;

        /// <summary>One credit's identity: every field, plus the sender's per-credit sequence so equal hits stay distinct.</summary>
        public static string Key(NativeRef source, long sourceOwner, NativeRef boss, long playerId, float amount, long sequence) =>
            string.Join("|", source, sourceOwner.ToString(CultureInfo.InvariantCulture), boss, playerId.ToString(CultureInfo.InvariantCulture),
                BitConverter.SingleToInt32Bits(amount).ToString(CultureInfo.InvariantCulture), sequence.ToString(CultureInfo.InvariantCulture));

        /// <summary>The authority validated <paramref name="source"/> live: owned by <paramref name="owner"/> and belonging to
        /// <paramref name="boss"/>. Replaces an older proof of the same source.</summary>
        public void Observe(NativeRef source, long owner, NativeRef boss, double now)
        {
            Prune(now);
            proofs[source] = (owner, boss, now);
            while (proofs.Count > capacity)
                proofs.Remove(proofs.OrderBy(p => p.Value.Seen).First().Key);
        }

        public bool ProvenLate(NativeRef source, long sender, NativeRef boss, double now)
        {
            Prune(now);
            return sender != 0 && proofs.TryGetValue(source, out var proof) && proof.Owner == sender && proof.Boss.Equals(boss);
        }

        public void Relayed(string key, long peer, double now)
        {
            Prune(now);
            if (relays.Count >= capacity) relays.RemoveAt(0);
            relays.Add((key, peer, now));
        }

        public bool ConsumeBounce(string key, long peer, double now)
        {
            Prune(now);
            int index = relays.FindIndex(r => r.Peer == peer && r.Key == key);
            if (index < 0) return false;
            relays.RemoveAt(index);
            return true;
        }

        void Prune(double now)
        {
            foreach (var stale in proofs.Where(p => now - p.Value.Seen > grace).Select(p => p.Key).ToList()) proofs.Remove(stale);
            relays.RemoveAll(r => now - r.At > grace);
        }
    }
}
