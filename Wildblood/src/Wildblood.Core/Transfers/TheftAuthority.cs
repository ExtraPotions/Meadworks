// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: retained authoritative theft outcomes. The v2 steal ask carries the requester's stable root and
// exact item fingerprint; the creature owner keeps a bounded outcome archive beside the pouch (content and outcome in one
// owner write, the Timber 8b9c5ad SiteDeliveryStore idiom), answers status only from that archive or an exact
// root-bound pouch entry, and never invents a refusal because a proof is missing.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExtraPotions.Wildblood.Transfers
{
    public enum TheftStatusAnswer : byte { Unknown = 0, Approved = 1, Denied = 2 }

    public sealed class TheftAsk
    {
        public TheftAsk(string root, int counter, string fingerprint, byte[] item, double sentAt) { Root = root; Counter = counter; Fingerprint = fingerprint; Item = item; SentAt = sentAt; }
        public string Root { get; }
        public int Counter { get; }
        /// <summary>The requester's synchronized world time (ZNet.GetTimeSeconds) when the debit was saved.</summary>
        public double SentAt { get; }
        public string Fingerprint { get; }
        public byte[] Item { get; }
    }

    public sealed class TheftStatusQuery
    {
        public TheftStatusQuery(string root, string fingerprint, TheftStatusAnswer answer) { Root = root; Fingerprint = fingerprint; Answer = answer; }
        public string Root { get; }
        public string Fingerprint { get; }
        public TheftStatusAnswer Answer { get; }
    }

    /// <summary>Bounded byte codecs for the v2 steal ask, status query and status answer.</summary>
    public static class TheftWire
    {
        public const int Version = 2;
        public const int MaxItem = 1024 * 1024;
        /// <summary>An ask older than this (synchronized world time) is never banked; the owner archives Denied instead.</summary>
        public const double AskWindowSeconds = 5;
        /// <summary>The requester waits this long for the live answer before it asks the current owner for status. It
        /// exceeds <see cref="AskWindowSeconds"/>, so a status fence and the requester's settlement always come after the
        /// last moment an ask could still bank: a late ask (lag, an ownership round trip) can never bank a settled theft.</summary>
        public const double ReplyDeadlineSeconds = 10;
        // Kind 1 (an ask without a send time) is retired: such an ask is refused unread and recovers through status.
        const byte Ask = 4, Status = 2, Answer = 3;

        /// <summary>True while an ask sent at <paramref name="sentAt"/> may still bank at <paramref name="now"/>.</summary>
        public static bool Fresh(double sentAt, double now) => Finite(sentAt) && Finite(now) && now - sentAt <= AskWindowSeconds && sentAt - now <= AskWindowSeconds;
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

        public static bool IsRoot(string? value) => Hex(value, 32);
        public static bool IsFingerprint(string? value) => Hex(value, 64);
        static bool Hex(string? value, int length) => value != null && value.Length == length && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');

        public static bool ItemMatches(string fingerprint, byte[] item) => item != null && item.Length > 0 && TheftMetadata.Hash(item) == fingerprint;

        public static byte[] EncodeAsk(string root, int counter, string fingerprint, byte[] item, double sentAt)
        {
            if (!IsRoot(root) || counter <= 0 || !IsFingerprint(fingerprint) || item == null || item.Length == 0 || item.Length > MaxItem || !Finite(sentAt))
                throw new InvalidDataException("Theft ask.");
            return Write(w => { w.Write(Ask); w.Write(root); w.Write(counter); w.Write(sentAt); w.Write(fingerprint); w.Write(item.Length); w.Write(item); });
        }

        public static TheftAsk DecodeAsk(byte[] bytes) => Read(bytes, Ask, MaxItem + 256, r =>
        {
            string root = r.ReadString(); int counter = r.ReadInt32(); double sentAt = r.ReadDouble(); string fp = r.ReadString(); int length = r.ReadInt32();
            if (!IsRoot(root) || counter <= 0 || !Finite(sentAt) || !IsFingerprint(fp) || length <= 0 || length > MaxItem) throw new InvalidDataException("Theft ask.");
            byte[] item = r.ReadBytes(length);
            if (item.Length != length) throw new InvalidDataException("Theft ask truncated.");
            return new TheftAsk(root, counter, fp, item, sentAt);
        });

        public static byte[] EncodeStatus(string root, string fingerprint) => Identity(Status, root, fingerprint, null);
        public static byte[] EncodeAnswer(string root, string fingerprint, TheftStatusAnswer answer) => Identity(Answer, root, fingerprint, answer);

        public static TheftStatusQuery DecodeStatus(byte[] bytes) => Read(bytes, Status, 256, r => ReadIdentity(r, false));
        public static TheftStatusQuery DecodeAnswer(byte[] bytes) => Read(bytes, Answer, 256, r => ReadIdentity(r, true));

        static byte[] Identity(byte kind, string root, string fingerprint, TheftStatusAnswer? answer)
        {
            if (!IsRoot(root) || !IsFingerprint(fingerprint) || answer.HasValue && !Enum.IsDefined(typeof(TheftStatusAnswer), answer.Value))
                throw new InvalidDataException("Theft status identity.");
            return Write(w => { w.Write(kind); w.Write(root); w.Write(fingerprint); if (answer.HasValue) w.Write((byte)answer.Value); });
        }

        static TheftStatusQuery ReadIdentity(BinaryReader r, bool answer)
        {
            string root = r.ReadString(), fp = r.ReadString();
            var value = answer ? (TheftStatusAnswer)r.ReadByte() : TheftStatusAnswer.Unknown;
            if (!IsRoot(root) || !IsFingerprint(fp) || !Enum.IsDefined(typeof(TheftStatusAnswer), value)) throw new InvalidDataException("Theft status identity.");
            return new TheftStatusQuery(root, fp, value);
        }

        static byte[] Write(Action<BinaryWriter> body)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) { writer.Write(Version); body(writer); }
            return stream.ToArray();
        }

        static T Read<T>(byte[] bytes, byte kind, int bound, Func<BinaryReader, T> body)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > bound) throw new InvalidDataException("Theft message bound.");
            using var reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.UTF8);
            try
            {
                if (reader.ReadInt32() != Version || reader.ReadByte() != kind) throw new InvalidDataException("Theft message kind.");
                T value = body(reader);
                if (reader.BaseStream.Position != bytes.Length) throw new InvalidDataException("Theft message trailing data.");
                return value;
            }
            catch (EndOfStreamException) { throw new InvalidDataException("Theft message truncated."); }
        }
    }

    /// <summary>The creature owner's retained outcomes. Bounded; a full archive refuses new outcomes rather than
    /// discarding an unacknowledged one. Only an exact acknowledgement releases a record.</summary>
    public sealed class TheftOutcomeArchive
    {
        public const int Capacity = 32;
        readonly List<(string Root, string Fingerprint, bool Approved)> entries = new List<(string, string, bool)>();

        public int Count => entries.Count;
        public bool CanRecord => entries.Count < Capacity;
        public IReadOnlyList<(string Root, string Fingerprint, bool Approved)> Entries => entries.AsReadOnly();

        public TheftStatusAnswer Find(string root, string fingerprint)
        {
            foreach (var entry in entries)
                if (entry.Root == root)
                    return entry.Fingerprint != fingerprint ? TheftStatusAnswer.Unknown : entry.Approved ? TheftStatusAnswer.Approved : TheftStatusAnswer.Denied;
            return TheftStatusAnswer.Unknown;
        }

        public bool Contains(string root) => entries.Any(e => e.Root == root);

        public bool Record(string root, string fingerprint, bool approved)
        {
            if (!TheftWire.IsRoot(root) || !TheftWire.IsFingerprint(fingerprint)) return false;
            foreach (var entry in entries)
                if (entry.Root == root) return entry.Fingerprint == fingerprint && entry.Approved == approved;
            if (!CanRecord) return false;
            entries.Add((root, fingerprint, approved));
            return true;
        }

        public bool Acknowledge(string root, string fingerprint)
        {
            int index = entries.FindIndex(e => e.Root == root && e.Fingerprint == fingerprint);
            if (index < 0) return false;
            entries.RemoveAt(index);
            return true;
        }

        public byte[] Encode()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(1); writer.Write(entries.Count);
                foreach (var entry in entries) { writer.Write(entry.Root); writer.Write(entry.Fingerprint); writer.Write(entry.Approved); }
            }
            return stream.ToArray();
        }

        public static TheftOutcomeArchive Decode(byte[]? bytes)
        {
            var archive = new TheftOutcomeArchive();
            if (bytes == null || bytes.Length == 0) return archive;
            if (bytes.Length > 8 + Capacity * 100) throw new InvalidDataException("Theft outcome archive exceeds its bound.");
            using var reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.UTF8);
            try
            {
                if (reader.ReadInt32() != 1) throw new InvalidDataException("Theft outcome archive version.");
                int count = reader.ReadInt32();
                if (count < 0 || count > Capacity) throw new InvalidDataException("Theft outcome archive count.");
                for (int i = 0; i < count; i++)
                {
                    string root = reader.ReadString(), fp = reader.ReadString(); bool approved = reader.ReadBoolean();
                    if (!TheftWire.IsRoot(root) || !TheftWire.IsFingerprint(fp) || archive.Contains(root)) throw new InvalidDataException("Theft outcome archive row.");
                    archive.entries.Add((root, fp, approved));
                }
                if (reader.BaseStream.Position != bytes.Length) throw new InvalidDataException("Theft outcome archive trailing data.");
                return archive;
            }
            catch (EndOfStreamException) { throw new InvalidDataException("Theft outcome archive truncated."); }
        }
    }

    public static class TheftOwnerStatus
    {
        /// <summary>The creature owner's answer: an archived outcome, else Approved only when the pouch holds the exact
        /// root and fingerprint, else Unknown. A missing proof is never answered as Denied.</summary>
        public static TheftStatusAnswer Answer(TheftOutcomeArchive archive, Func<string, string, bool> pouchHolds, string root, string fingerprint)
        {
            if (!TheftWire.IsRoot(root) || !TheftWire.IsFingerprint(fingerprint)) return TheftStatusAnswer.Unknown;
            if (archive.Contains(root)) return archive.Find(root, fingerprint);
            return pouchHolds(root, fingerprint) ? TheftStatusAnswer.Approved : TheftStatusAnswer.Unknown;
        }

        /// <summary>The current owner's status decision. A root with no archived outcome and no exact pouch entry was never
        /// banked by this ZDO's history (a lost ask, a rolled-back world, a dead owner's unsynced write), so the owner fences
        /// it: Denied is archived, and a late ask for that root replays Denied instead of banking. <paramref name="recorded"/>
        /// tells the caller to persist the archive. A full archive or a fingerprint conflict stays Unknown.</summary>
        public static TheftStatusAnswer Decide(TheftOutcomeArchive archive, Func<string, string, bool> pouchHolds, string root, string fingerprint, out bool recorded)
        {
            recorded = false;
            var answer = Answer(archive, pouchHolds, root, fingerprint);
            if (answer != TheftStatusAnswer.Unknown || !TheftWire.IsRoot(root) || !TheftWire.IsFingerprint(fingerprint) || archive.Contains(root)) return answer;
            if (!archive.Record(root, fingerprint, false)) return TheftStatusAnswer.Unknown;
            recorded = true;
            return TheftStatusAnswer.Denied;
        }
    }

    /// <summary>The world copy of retained outcomes, written by the thief's owner before the thief leaves the world (death,
    /// despawn, purge, raid exit). It rides the world's own global keys, so it is saved with the world, survives the
    /// owner's disconnect and any later host of that world, and is readable by the requester wherever it stands.
    /// Key: <c>wildblood_theft_&lt;root&gt;</c>, value: fingerprint plus 1 (Approved) or 0 (Denied); all lowercase, as the
    /// game stores global keys.</summary>
    public static class TheftWorldArchive
    {
        public const string Prefix = "wildblood_theft_";
        /// <summary>Soft bound on retained world rows; a requester's settlement removes its row.</summary>
        public const int Capacity = 256;

        public static string Key(string root) => Prefix + root;

        public static string Entry(string root, string fingerprint, bool approved)
        {
            if (!TheftWire.IsRoot(root) || !TheftWire.IsFingerprint(fingerprint)) throw new InvalidDataException("Theft world archive row.");
            return Key(root) + " " + Value(fingerprint, approved);
        }

        public static string Value(string fingerprint, bool approved) => fingerprint + (approved ? "1" : "0");

        /// <summary>Reads one world row; a missing, malformed or other-fingerprint row is Unknown.</summary>
        public static TheftStatusAnswer Read(string? value, string fingerprint)
        {
            if (value == null || value.Length != 65 || !TheftWire.IsFingerprint(fingerprint) || !value.StartsWith(fingerprint, StringComparison.Ordinal))
                return TheftStatusAnswer.Unknown;
            return value[64] == '1' ? TheftStatusAnswer.Approved : value[64] == '0' ? TheftStatusAnswer.Denied : TheftStatusAnswer.Unknown;
        }

        /// <summary>The rows a retiring thief must publish: every retained (unacknowledged) outcome, Approved or Denied.</summary>
        public static IReadOnlyList<string> Rows(TheftOutcomeArchive archive) => archive.Entries.Select(e => Entry(e.Root, e.Fingerprint, e.Approved)).ToArray();

        /// <summary>A non-final removal (despawn, raid exit) may only proceed when every row fits; a death proceeds regardless,
        /// because the thief is gone either way and a published row is the only remaining proof.</summary>
        public static bool Admit(int existing, int adding, bool lastChance) => adding == 0 || lastChance || existing + adding <= Capacity;
    }

    public enum TheftRecoveryStep { Retain, Record, Settled }

    /// <summary>Requester-side explicit recovery. Only an authoritative Approved/Denied becomes a receipt; Unknown keeps
    /// the debit retained. Recording reuses <see cref="TheftClientLedger.RecordOutcome"/>, which is idempotent and
    /// refuses a conflicting later answer.</summary>
    public static class TheftRecovery
    {
        public static bool Eligible(TheftRequest request, bool v2) => v2 && request != null && TheftWire.IsRoot(request.Root) && TheftWire.IsFingerprint(request.Fingerprint);

        public static TheftRecoveryStep Step(TheftStatusAnswer answer, bool outcomeKnown) =>
            outcomeKnown ? TheftRecoveryStep.Settled : answer == TheftStatusAnswer.Unknown ? TheftRecoveryStep.Retain : TheftRecoveryStep.Record;

        public static TheftReceipt? Receipt(TheftRequest request, TheftStatusAnswer answer) =>
            answer == TheftStatusAnswer.Unknown ? null : TheftReceipt.For(request, answer == TheftStatusAnswer.Approved);
    }
}
