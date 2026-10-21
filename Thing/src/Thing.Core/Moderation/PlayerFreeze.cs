// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: connection-only freeze synchronization. Never written to a save or ledger.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Thing.Moderation
{
    public readonly struct PlayerFreezeUpdate
    {
        public PlayerFreezeUpdate(string playerId, long peer, long revision, bool frozen)
        { PlayerId = playerId; Peer = peer; Revision = revision; Frozen = frozen; }
        public string PlayerId { get; }
        public long Peer { get; }
        public long Revision { get; }
        public bool Frozen { get; }
    }

    /// <summary>Every ready connection retains its desired state, including false, for periodic reconciliation.</summary>
    public sealed class PlayerFreezeSet
    {
        sealed class State
        {
            internal PlayerFreezeUpdate Update;
            internal bool Acknowledged;
            internal double LastSent = double.NegativeInfinity;
        }
        readonly Dictionary<string, State> players = new Dictionary<string, State>(StringComparer.Ordinal);
        long revision;

        public void Retain(IEnumerable<KeyValuePair<string, long>> online)
        {
            var keep = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var pair in online)
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value != 0) keep[pair.Key] = pair.Value;
            foreach (var id in players.Keys.ToArray())
                if (!keep.TryGetValue(id, out var peer) || peer != players[id].Update.Peer) players.Remove(id);
            foreach (var pair in keep)
                if (!players.ContainsKey(pair.Key))
                    players.Add(pair.Key, new State { Update = new PlayerFreezeUpdate(pair.Key, pair.Value, ++revision, false) });
        }

        public bool TrySet(string playerId, long peer, bool frozen, out PlayerFreezeUpdate update)
        {
            update = default;
            if (!players.TryGetValue(playerId, out var state) || peer == 0 || state.Update.Peer != peer) return false;
            update = new PlayerFreezeUpdate(playerId, peer, ++revision, frozen);
            state.Update = update;
            state.Acknowledged = false;
            state.LastSent = double.NegativeInfinity;
            return true;
        }

        public bool IsCurrent(PlayerFreezeUpdate update) => players.TryGetValue(update.PlayerId, out var state)
            && state.Update.Peer == update.Peer && state.Update.Revision == update.Revision;

        public bool Acknowledge(long peer, long receivedRevision, bool frozen)
        {
            foreach (var state in players.Values)
            {
                var update = state.Update;
                if (update.Peer != peer || update.Revision != receivedRevision || update.Frozen != frozen || state.Acknowledged) continue;
                state.Acknowledged = true;
                return true;
            }
            return false;
        }

        public IReadOnlyList<PlayerFreezeUpdate> Due(double now)
        {
            var due = new List<PlayerFreezeUpdate>();
            foreach (var state in players.Values)
            {
                if (now - state.LastSent < 1) continue;
                state.LastSent = now;
                due.Add(state.Update);
            }
            return due;
        }

        public void ClearAll() => players.Clear();
        public void Disconnect(string playerId) => players.Remove(playerId);
    }

    /// <summary>A lost unfreeze cannot outlive this lease, even when no further packets arrive.</summary>
    public sealed class PlayerFreezeLease
    {
        public const double LeaseSeconds = 4;
        long revision;
        bool desired;
        double expires;

        public bool IsFrozen(double now) => desired && now < expires;

        public bool Receive(long receivedRevision, bool frozen, double now)
        {
            if (receivedRevision <= 0 || receivedRevision < revision || (receivedRevision == revision && frozen != desired)) return false;
            revision = receivedRevision;
            desired = frozen;
            expires = now + LeaseSeconds;
            return true;
        }

        public void Clear() { revision = 0; desired = false; expires = 0; }
    }
}
