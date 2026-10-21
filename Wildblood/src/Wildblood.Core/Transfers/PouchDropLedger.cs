// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Thieving pouch's death/despawn drop decisions. Reuses the InventoryDecisionJournal
// write-ahead journal (Timber 8b9c5ad / Burrow 14f92c8): one durable mark per pouch entry before the native world
// drop is created, the created identity after. A mark without an identity is UNKNOWN and is never created again.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtraPotions.Wildblood.Transfers
{
    /// <summary>What the owner may do with one pouch entry at a death or despawn.</summary>
    public enum PouchDropDecision
    {
        /// <summary>The mark is durable: create the native drop now, then record its identity.</summary>
        Create,
        /// <summary>A drop with a recorded identity exists already; never create another.</summary>
        Created,
        /// <summary>A decision exists without a recorded identity (crash, throw or another owner): retained, never recreated.</summary>
        Unknown,
        /// <summary>No durable mark could be written: nothing may be created as a receipted drop.</summary>
        Unavailable,
    }

    public enum PouchDropState { None, Marked, Created }

    public static class PouchDropKeys
    {
        /// <summary>One pouch entry's stable identity: the thief creature, the stealer and its counter, the request root
        /// (empty for a v1 entry) and the exact native item bytes.</summary>
        public static string Entry(long sourceUser, uint sourceId, long stealer, int counter, string root, byte[] item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            using var sha = SHA256.Create();
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write("wb.pouch.drop.v1");
                writer.Write(sourceUser); writer.Write(sourceId); writer.Write(stealer); writer.Write(counter);
                writer.Write(root ?? ""); writer.Write(item.Length); writer.Write(item);
            }
            return Hex(sha.ComputeHash(stream.ToArray()));
        }

        internal static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        public static bool IsKey(string? value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    }

    /// <summary>Owner-local durable drop decisions, in bounded journal segments that are all read back at open.</summary>
    public sealed class PouchDropLedger : IDisposable
    {
        const string Source = "wildblood.pouch.drop";
        const string CreatedSuffix = ".created";
        readonly string directory;
        readonly int segmentCapacity, maxSegments;
        readonly List<InventoryDecisionJournal> segments = new List<InventoryDecisionJournal>();
        readonly Dictionary<string, InventoryDecisionRecord> records = new Dictionary<string, InventoryDecisionRecord>(StringComparer.Ordinal);

        public PouchDropLedger(string directory, int segmentCapacity = 4096, int maxSegments = 64)
        {
            if (segmentCapacity < 2 || segmentCapacity > 4096) throw new ArgumentOutOfRangeException(nameof(segmentCapacity));
            if (maxSegments < 1 || maxSegments > 1024) throw new ArgumentOutOfRangeException(nameof(maxSegments));
            this.directory = directory; this.segmentCapacity = segmentCapacity; this.maxSegments = maxSegments;
            try
            {
                Directory.CreateDirectory(directory);
                for (int i = 0; i < maxSegments && File.Exists(SegmentPath(i)); i++) Open(i);
                if (segments.Count == 0) Open(0);
            }
            catch { Dispose(); throw; }
        }

        string SegmentPath(int index) => Path.Combine(directory, "drops-" + index.ToString("0000") + ".bin");

        void Open(int index)
        {
            var journal = new InventoryDecisionJournal(SegmentPath(index), segmentCapacity);
            segments.Add(journal);
            foreach (var record in journal.Snapshot())
            {
                if (records.ContainsKey(record.Id)) throw new InvalidDataException("Duplicate pouch drop decision.");
                records.Add(record.Id, record);
            }
        }

        public PouchDropState State(string key)
        {
            if (records.ContainsKey(key + CreatedSuffix)) return PouchDropState.Created;
            return records.ContainsKey(key) ? PouchDropState.Marked : PouchDropState.None;
        }

        /// <summary>The native item bytes retained with a mark (empty when none were given).</summary>
        public byte[]? MarkedItem(string key) => records.TryGetValue(key, out var record) ? record.Before : null;

        /// <summary>Marked entries without a recorded identity: the retained UNKNOWN drops.</summary>
        public IReadOnlyList<string> Unresolved =>
            records.Keys.Where(k => !k.EndsWith(CreatedSuffix, StringComparison.Ordinal) && !records.ContainsKey(k + CreatedSuffix)).ToArray();

        public string? CreatedIdentity(string key) =>
            records.TryGetValue(key + CreatedSuffix, out var record) ? Encoding.UTF8.GetString(record.Metadata) : null;

        /// <summary>Decides one entry. <paramref name="markedElsewhere"/>: the creature's replicated marker names this entry
        /// (another owner decided it). Only <see cref="PouchDropDecision.Create"/> permits a native creation.</summary>
        public PouchDropDecision Begin(string key, bool markedElsewhere, byte[]? item = null)
        {
            if (!PouchDropKeys.IsKey(key)) throw new ArgumentException("Pouch drop key.", nameof(key));
            switch (State(key))
            {
                case PouchDropState.Created: return PouchDropDecision.Created;
                case PouchDropState.Marked: return PouchDropDecision.Unknown;
            }
            if (markedElsewhere) return PouchDropDecision.Unknown;
            var journal = Writable();
            if (journal == null) return PouchDropDecision.Unavailable;
            // The exact native item bytes ride the mark, so an UNKNOWN entry stays recoverable after its creature is gone.
            var record = journal.Prepare(key, Source, key, 0, item ?? Array.Empty<byte>(), Array.Empty<byte>());
            records.Add(key, record);
            return PouchDropDecision.Create;
        }

        /// <summary>Records the native drop actually created for a marked entry. Repeating the same identity is a no-op;
        /// a different identity or a missing mark is refused.</summary>
        public bool Complete(string key, string identity)
        {
            if (string.IsNullOrWhiteSpace(identity) || identity.Length > 128) return false;
            var existing = CreatedIdentity(key);
            if (existing != null) return existing == identity;
            if (!records.ContainsKey(key)) return false;
            var journal = Writable();
            if (journal == null) return false;
            var record = journal.Prepare(key + CreatedSuffix, Source, key, 0, Array.Empty<byte>(), Array.Empty<byte>(), Encoding.UTF8.GetBytes(identity));
            records.Add(record.Id, record);
            return true;
        }

        InventoryDecisionJournal? Writable()
        {
            var last = segments[segments.Count - 1];
            if (last.CanAdmit) return last;
            if (segments.Count >= maxSegments) return null;
            Open(segments.Count);
            return segments[segments.Count - 1].CanAdmit ? segments[segments.Count - 1] : null;
        }

        public void Dispose()
        {
            foreach (var journal in segments) journal.Dispose();
            segments.Clear(); records.Clear();
        }
    }

    /// <summary>The creature's replicated list of pouch entries whose drop some owner has already decided.</summary>
    public static class PouchDropMarker
    {
        public const int MaxKeys = 64;

        public static byte[] Encode(IEnumerable<string> keys)
        {
            var list = keys.ToList();
            if (list.Count > MaxKeys || list.Any(k => !PouchDropKeys.IsKey(k)) || list.Distinct(StringComparer.Ordinal).Count() != list.Count)
                throw new InvalidDataException("Pouch drop marker.");
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(1); writer.Write(list.Count);
                foreach (var key in list) writer.Write(key);
            }
            return stream.ToArray();
        }

        public static IReadOnlyList<string> Decode(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return Array.Empty<string>();
            if (bytes.Length > 8 + MaxKeys * 65) throw new InvalidDataException("Pouch drop marker exceeds its bound.");
            using var reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.UTF8);
            try
            {
                if (reader.ReadInt32() != 1) throw new InvalidDataException("Pouch drop marker version.");
                int count = reader.ReadInt32();
                if (count < 0 || count > MaxKeys) throw new InvalidDataException("Pouch drop marker count.");
                var keys = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    if (!PouchDropKeys.IsKey(key) || keys.Contains(key)) throw new InvalidDataException("Pouch drop marker key.");
                    keys.Add(key);
                }
                if (reader.BaseStream.Position != bytes.Length) throw new InvalidDataException("Pouch drop marker trailing data.");
                return keys;
            }
            catch (EndOfStreamException) { throw new InvalidDataException("Pouch drop marker truncated."); }
        }
    }
}
