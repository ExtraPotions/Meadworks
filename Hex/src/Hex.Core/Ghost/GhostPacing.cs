// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: request and publish pacing for server-confirmed Ghost Mode. Crucible's request and peer-command
// ledgers keep every identity for the whole session (4096), so nothing here polls. Unity-free; times are monotonic seconds.
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Hex.Ghost
{
    /// <summary>
    /// Remote client: asks only when the wish differs from the confirmed state. One request in flight, one per second;
    /// an answered request is not repeated until the key (session, character, wish, policy stamp) changes; a transport
    /// failure may be retried twice for the same key, five seconds apart. No periodic retry.
    /// </summary>
    public sealed class GhostRequester
    {
        public const double Spacing = 1, FailureDelay = 5;
        public const int MaxAttempts = 3;
        GhostRequestFacts? current, sent;
        int attempts;
        bool answered, inFlight;
        double nextAllowed = double.NegativeInfinity;

        public bool InFlight => inFlight;

        public GhostVerb? Next(double now, GhostRequestFacts facts)
        {
            if (facts.Character.IsNone) return null;
            if (current == null || !current.Value.SameKey(facts)) { current = facts; attempts = 0; answered = false; }
            if (facts.Wish == facts.Confirmed || inFlight || answered || attempts >= MaxAttempts || now < nextAllowed) return null;
            attempts++;
            inFlight = true;
            sent = facts;
            nextAllowed = now + Spacing;
            return facts.Wish ? GhostVerb.On : GhostVerb.Off;
        }

        /// <param name="wasAnswered">The server replied (confirmed, off or refused); false when the outcome is unknown.</param>
        public void Completed(double now, bool wasAnswered)
        {
            if (!inFlight) return;
            inFlight = false;
            var same = sent != null && current != null && sent.Value.SameKey(current.Value);
            if (wasAnswered) { if (same) answered = true; }
            else nextAllowed = Math.Max(nextAllowed, now + FailureDelay);
            sent = null;
        }

        public void Reset() { current = sent = null; attempts = 0; answered = inFlight = false; nextAllowed = double.NegativeInfinity; }
    }

    /// <summary>
    /// Server: per-peer delivery of the confirmed set. One command in flight per peer, at most one send per peer every
    /// two seconds, failure backoff 2, 4, 8 then 10 seconds while connected. A new peer is sent only a non-empty set.
    /// </summary>
    public sealed class GhostPublisher
    {
        public const double Spacing = 2, MaxBackoff = 10;
        sealed class Peer { public long Delivered = -1; public bool InFlight; public double NextAttempt; public int Failures; }
        readonly Dictionary<string, Peer> peers = new Dictionary<string, Peer>(StringComparer.Ordinal);

        public bool Due(string peer, long revision, bool empty, double now)
        {
            if (!peers.TryGetValue(peer, out var state)) return !empty;
            return !state.InFlight && state.Delivered != revision && now >= state.NextAttempt;
        }

        public void Sent(string peer, long revision, double now)
        {
            if (!peers.TryGetValue(peer, out var state)) peers[peer] = state = new Peer();
            state.InFlight = true;
            state.NextAttempt = now + Spacing;
        }

        public void Delivered(string peer, long revision)
        {
            if (!peers.TryGetValue(peer, out var state)) return;
            state.InFlight = false; state.Delivered = revision; state.Failures = 0;
        }

        public void Failed(string peer, double now)
        {
            if (!peers.TryGetValue(peer, out var state)) return;
            state.InFlight = false;
            state.Failures = Math.Min(state.Failures + 1, 8);
            state.NextAttempt = now + Math.Min(MaxBackoff, Math.Pow(2, state.Failures));
        }

        /// <summary>Forgets every peer not in <paramref name="live"/> (disconnected).</summary>
        public void Retain(IEnumerable<string> live)
        {
            // Runs every frame on a server: nothing is built while no peer is tracked or none left.
            if (peers.Count == 0) return;
            var keep = live as ICollection<string> ?? new HashSet<string>(live ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<string>? gone = null;
            foreach (var peer in peers.Keys) if (!keep.Contains(peer)) (gone ??= new List<string>()).Add(peer);
            if (gone != null) foreach (var peer in gone) peers.Remove(peer);
        }

        public void Clear() => peers.Clear();
    }
}
