// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the server's confirmed Ghost registry and a client's view of the published set. Unity-free.
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Hex.Ghost
{
    /// <summary>
    /// Server only: native peer id to that peer's confirmed character. Every change advances <see cref="Revision"/>,
    /// which never goes back, so a client can refuse anything older than what it already accepted.
    /// </summary>
    public sealed class GhostRegistry
    {
        public const int MaxEntries = 256;
        readonly Dictionary<string, GhostKey> entries = new Dictionary<string, GhostKey>(StringComparer.Ordinal);
        readonly HashSet<GhostKey> keys = new HashSet<GhostKey>();

        public long Revision { get; private set; }
        public int Count => entries.Count;
        public bool Has(string peer) => peer != null && entries.ContainsKey(peer);
        public bool Contains(GhostKey key) => !key.IsNone && keys.Contains(key);
        public bool CanAdmit(string peer) => Has(peer) || entries.Count < MaxEntries;

        /// <summary>Records or replaces the peer's confirmed character. True when anything changed.</summary>
        public bool Grant(string peer, GhostKey key)
        {
            if (string.IsNullOrEmpty(peer)) throw new ArgumentException("Expected a peer.", nameof(peer));
            if (key.IsNone) throw new ArgumentException("Expected a character.", nameof(key));
            if (entries.TryGetValue(peer, out var existing) && existing == key) return false;
            if (!CanAdmit(peer)) throw new InvalidOperationException("Ghost registry is full.");
            entries[peer] = key;
            Rebuild();
            return true;
        }

        /// <summary>Removes the peer's entry. True when it existed.</summary>
        public bool Revoke(string peer)
        {
            if (peer == null || !entries.Remove(peer)) return false;
            Rebuild();
            return true;
        }

        /// <summary>Removes every entry the check refuses, as one revision; returns the removed peers.</summary>
        public IReadOnlyList<string> RetainWhere(Func<string, GhostKey, bool> keep)
        {
            if (keep == null) throw new ArgumentNullException(nameof(keep));
            var removed = entries.Where(pair => !keep(pair.Key, pair.Value)).Select(pair => pair.Key).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (removed.Length == 0) return removed;
            foreach (var peer in removed) entries.Remove(peer);
            Rebuild();
            return removed;
        }

        public void Clear() { if (entries.Count == 0) return; entries.Clear(); Rebuild(); }

        public GhostKey[] Snapshot() => keys.OrderBy(k => k).ToArray();

        void Rebuild()
        {
            keys.Clear();
            foreach (var key in entries.Values) keys.Add(key);
            Revision = checked(Revision + 1);
        }
    }

    /// <summary>
    /// Client only: the last confirmed set accepted from the current server connection. Accepts only its own session
    /// generation and strictly newer revisions; <see cref="Reset"/> on a new generation forgets everything.
    /// </summary>
    public sealed class GhostView
    {
        readonly HashSet<GhostKey> keys = new HashSet<GhostKey>();
        long generation = -1, revision = -1;

        public int Count => keys.Count;
        public long Generation => generation;
        public bool Contains(GhostKey key) => !key.IsNone && keys.Contains(key);

        public void Reset(long sessionGeneration) { generation = sessionGeneration; revision = -1; keys.Clear(); }

        public bool Accept(long sessionGeneration, long setRevision, IReadOnlyCollection<GhostKey> set)
        {
            if (set == null || sessionGeneration != generation || setRevision <= revision) return false;
            revision = setRevision;
            keys.Clear();
            foreach (var key in set) if (!key.IsNone) keys.Add(key);
            return true;
        }
    }
}
