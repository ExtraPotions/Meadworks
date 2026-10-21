// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using ExtraPotions.Wildblood.Transfers;

namespace ExtraPotions.Wildblood.Luck
{
    public sealed class LuckDecisionRequest
    {
        public string Id { get; }
        public long World { get; }
        public string RequesterEpoch { get; }
        public string Source { get; }
        public long SourceOwner { get; }
        public string Attacker { get; }
        public long Player { get; }
        public string Recipient { get; }
        public long RecipientOwner { get; }
        public int Level { get; }
        public string Fingerprint { get; }
        public string ExpectedOwnerEpoch { get; }
        public long ExpectedRevision { get; }
        public LuckDecisionRequest(string id, long world, string requesterEpoch, string source, long sourceOwner,
            string attacker, long player, string recipient, long recipientOwner, int level, string fingerprint, string expectedOwnerEpoch, long expectedRevision)
        {
            LuckDecisionCodec.Text(expectedOwnerEpoch, 128);
            string[] parts = (id ?? "").Split('.');
            if (expectedRevision < 0 || expectedRevision == long.MaxValue || parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out _) ||
                id != CreateId(expectedOwnerEpoch, expectedRevision, parts[0]) || world == 0 || sourceOwner == 0 || player == 0 || recipientOwner == 0 || level < 1 || level > WeightedPity.MaximumStars + 1)
                throw new InvalidDataException("Invalid luck decision identity.");
            LuckDecisionCodec.Text(requesterEpoch, 128); LuckDecisionCodec.Text(source, 128);
            LuckDecisionCodec.Text(attacker, 128); LuckDecisionCodec.Text(recipient, 128); LuckDecisionCodec.Digest(fingerprint);
            Id = id; World = world; RequesterEpoch = requesterEpoch; Source = source; SourceOwner = sourceOwner;
            Attacker = attacker; Player = player; Recipient = recipient; RecipientOwner = recipientOwner; Level = level; Fingerprint = fingerprint;
            ExpectedOwnerEpoch = expectedOwnerEpoch; ExpectedRevision = expectedRevision;
        }
        public static string CreateId(string ownerEpoch, long revision) => CreateId(ownerEpoch, revision, Guid.NewGuid().ToString("N"));
        public static string CreateId(string ownerEpoch, long revision, string nonce)
        {
            LuckDecisionCodec.Text(ownerEpoch, 128);
            if (revision < 0 || revision == long.MaxValue || !Guid.TryParseExact(nonce, "N", out _)) throw new InvalidDataException("Invalid luck operation fence.");
            return nonce + "." + revision.ToString("X16", CultureInfo.InvariantCulture) + "." + LuckDecisionCodec.Hash(Encoding.UTF8.GetBytes(ownerEpoch));
        }
    }

    public sealed class LuckDecisionGrant
    {
        public LuckDecisionRequest Request { get; }
        public string OwnerEpoch { get; }
        public long Revision { get; }
        public string BaseImage { get; }
        public string BaseHash { get; }
        public LuckDecisionGrant(LuckDecisionRequest request, string ownerEpoch, long revision, string baseImage)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            LuckDecisionCodec.Text(ownerEpoch, 128); LuckDecisionCodec.Image(baseImage);
            if (revision < 0 || revision == long.MaxValue || revision != request.ExpectedRevision || ownerEpoch != request.ExpectedOwnerEpoch) throw new InvalidDataException("Invalid luck decision revision or owner epoch.");
            OwnerEpoch = ownerEpoch; Revision = revision; BaseImage = baseImage; BaseHash = LuckDecisionCodec.Hash(Encoding.UTF8.GetBytes(baseImage));
        }
    }

    public sealed class LuckDecisionResult
    {
        public LuckDecisionRequest Request { get; }
        public string OwnerEpoch { get; }
        public long Revision { get; }
        public string BaseHash { get; }
        public IReadOnlyList<string> Decisions { get; }
        public string FinalImage { get; }
        public LuckDecisionResult(LuckDecisionGrant grant, IReadOnlyList<string> decisions, string finalImage)
            : this(grant.Request, grant.OwnerEpoch, grant.Revision, grant.BaseHash, decisions, finalImage) { }
        internal LuckDecisionResult(LuckDecisionRequest request, string ownerEpoch, long revision, string baseHash, IReadOnlyList<string> decisions, string finalImage)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            LuckDecisionCodec.Text(ownerEpoch, 128); LuckDecisionCodec.Digest(baseHash); LuckDecisionCodec.Image(finalImage);
            if (revision < 0 || revision == long.MaxValue || revision != request.ExpectedRevision || ownerEpoch != request.ExpectedOwnerEpoch || decisions == null || decisions.Count > 128) throw new InvalidDataException("Invalid luck result bounds.");
            var copy = decisions.ToArray();
            foreach (var decision in copy)
            {
                LuckDecisionCodec.Text(decision, 256);
                if (!LuckDelta.TryParse(decision, out var delta) || delta.Consumed < 0 || delta.NewCounter < -1 ||
                    !(delta.Chance > 0f && delta.Chance <= .3f)) throw new InvalidDataException("Invalid luck decision row.");
            }
            OwnerEpoch = ownerEpoch; Revision = revision; BaseHash = baseHash; Decisions = Array.AsReadOnly(copy); FinalImage = finalImage;
        }
    }

    public enum LuckDecisionState { Granted, Busy, Refused, Prepared, Applied, Acknowledged, Stale }
    public sealed class LuckDecisionReply
    {
        public LuckDecisionState State { get; }
        public LuckDecisionGrant? Grant { get; }
        public LuckDecisionResult? Result { get; }
        internal LuckDecisionReply(LuckDecisionState state, LuckDecisionGrant? grant = null, LuckDecisionResult? result = null)
        { State = state; Grant = grant; Result = result; }
    }

    public enum LuckRecoveryState { UnresolvedNoResult, DurableResultPendingApply, AppliedAwaitingReceipt, Prepared, Acknowledged, Unsupported, RecoveredTerminal }

    public sealed class LuckRetainedOperation
    {
        readonly byte[] grantBytes;
        readonly byte[]? resultBytes;
        public LuckDecisionGrant Grant { get; }
        public LuckDecisionResult? Result { get; }
        public InventoryDecision GrantState { get; }
        public InventoryDecision? ResultState { get; }
        public LuckRecoveryState State { get; }
        public string GrantDigest { get; }
        public string? ResultDigest { get; }
        public byte[] GrantBytes => (byte[])grantBytes.Clone();
        public byte[]? ResultBytes => resultBytes == null ? null : (byte[])resultBytes.Clone();

        internal LuckRetainedOperation(LuckDecisionGrant grant, LuckDecisionResult? result, InventoryDecisionRecord grantRecord, InventoryDecisionRecord? resultRecord, bool recoveredTerminal = false)
        {
            Grant = grant; Result = result; GrantState = grantRecord.State; ResultState = resultRecord?.State;
            grantBytes = grantRecord.Metadata; resultBytes = resultRecord?.After;
            GrantDigest = LuckDecisionCodec.Hash(grantBytes);
            ResultDigest = resultBytes == null ? null : LuckDecisionCodec.Hash(resultBytes);
            State = recoveredTerminal && GrantState == InventoryDecision.Decided && ResultState == InventoryDecision.Committed ? LuckRecoveryState.RecoveredTerminal
                : GrantState == InventoryDecision.Committed && ResultState == InventoryDecision.Committed ? LuckRecoveryState.Acknowledged
                : GrantState == InventoryDecision.Prepared || ResultState == InventoryDecision.Prepared ? LuckRecoveryState.Prepared
                : GrantState != InventoryDecision.Decided ? LuckRecoveryState.Unsupported
                : result == null && resultRecord == null ? LuckRecoveryState.UnresolvedNoResult
                : result != null && ResultState == InventoryDecision.Decided ? LuckRecoveryState.DurableResultPendingApply
                : result != null && ResultState == InventoryDecision.Committed ? LuckRecoveryState.AppliedAwaitingReceipt : LuckRecoveryState.Unsupported;
        }
    }

    /// <summary>Durable recipient exclusion, not an atomic transaction with native item creation. Runtime authenticates and replays every result before Submit.</summary>
    public sealed partial class LuckDecisionLedger : IDisposable
    {
        const string GrantSource = "wildblood.luck.grant.v1", ResultSource = "wildblood.luck.result.v1";
        const string ReceiptSource = "wildblood.luck.recovery-receipt.v1", CheckpointSource = "wildblood.luck.checkpoint.v2";
        const string CheckpointId = "luck.checkpoint/v2";
        InventoryDecisionJournal journal;
        readonly string ownerEpoch;
        readonly int maximumOperations;
        readonly string? path;
        readonly long storeWorld, storePlayer;
        FileStream? storeLock;
        LuckRecoveryCheckpoint? checkpoint;
        bool unavailable;
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly Dictionary<(long World, long Player), long> revisions = new Dictionary<(long, long), long>();
        sealed class Entry
        {
            internal readonly LuckDecisionGrant Grant;
            internal readonly InventoryDecisionRecord GrantRecord;
            internal LuckDecisionResult? Result;
            internal InventoryDecisionRecord? ResultRecord;
            internal InventoryDecisionRecord? ReceiptRecord;
            internal LuckRecoveryCheckpoint? Receipt;
            internal Entry(LuckDecisionGrant grant, InventoryDecisionRecord record) { Grant = grant; GrantRecord = record; }
        }
        public LuckDecisionLedger(InventoryDecisionJournal journal, string ownerEpoch, int maximumOperations = 128)
        {
            this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
            LuckDecisionCodec.Text(ownerEpoch, 128);
            if (maximumOperations < 1 || maximumOperations > 256) throw new ArgumentOutOfRangeException(nameof(maximumOperations));
            this.ownerEpoch = ownerEpoch; this.maximumOperations = maximumOperations;
            Load();
        }

        void Load()
        {
            try
            {
            if (journal.Snapshot().Any(record => record.Source != GrantSource && record.Source != ResultSource && record.Source != ReceiptSource && record.Source != CheckpointSource)) throw new InvalidDataException("Unsupported luck journal record.");
            foreach (var record in journal.Snapshot().Where(r => r.Source == CheckpointSource))
            {
                if (checkpoint != null || record.Id != CheckpointId || record.State != InventoryDecision.Committed || record.Deadline != 0
                    || record.Before.Length != 0 || record.Metadata.Length != 0 || record.Fingerprint != LuckDecisionCodec.Hash(record.After))
                    throw new InvalidDataException("Invalid luck checkpoint record.");
                checkpoint = LuckRecoveryCheckpoint.Decode(record.After);
                revisions[(checkpoint.World, checkpoint.Player)] = checkpoint.HeadRevision;
            }
            foreach (var record in journal.Snapshot().Where(r => r.Source == GrantSource))
            {
                var grant = LuckDecisionCodec.DecodeGrant(record.Metadata);
                if (record.Id != GrantId(grant.Request.Id) || record.Fingerprint != RequestHash(grant.Request) ||
                    !record.Before.SequenceEqual(Encoding.UTF8.GetBytes(grant.BaseImage)) || !record.Before.SequenceEqual(record.After) ||
                    record.State == InventoryDecision.Aborted || entries.ContainsKey(grant.Request.Id)) throw new InvalidDataException("Conflicting retained luck grant.");
                entries.Add(grant.Request.Id, new Entry(grant, record));
            }
            if (entries.Values.GroupBy(e => (e.Grant.Request.World, e.Grant.Request.Player, e.Grant.Revision)).Any(group => group.Count() != 1))
                throw new InvalidDataException("Conflicting luck operations share a recipient revision.");
            foreach (var record in journal.Snapshot().Where(r => r.Source == ResultSource))
            {
                var result = LuckDecisionCodec.DecodeResult(record.After);
                if (!entries.TryGetValue(result.Request.Id, out var entry) || record.Id != ResultId(result.Request.Id) ||
                    !Matches(entry.Grant, result) || record.Fingerprint != RequestHash(result.Request) ||
                    !record.Before.SequenceEqual(Encoding.UTF8.GetBytes(entry.Grant.BaseImage)) || entry.Result != null ||
                    record.State == InventoryDecision.Aborted) throw new InvalidDataException("Conflicting retained luck result.");
                entry.Result = result; entry.ResultRecord = record;
                if (record.State == InventoryDecision.Committed)
                {
                    var key = Key(result.Request); revisions.TryGetValue(key, out long revision);
                    revisions[key] = Math.Max(revision, checked(result.Revision + 1));
                }
            }
            foreach (var entry in entries.Values)
                if (entry.GrantRecord.State == InventoryDecision.Committed && entry.ResultRecord?.State != InventoryDecision.Committed)
                    throw new InvalidDataException("Acknowledged luck grant has no applied result.");
            foreach (var record in journal.Snapshot().Where(r => r.Source == ReceiptSource))
            {
                var receipt = LuckRecoveryCheckpoint.Decode(record.After);
                var grant = LuckDecisionCodec.DecodeGrant(receipt.GrantBytes);
                var result = LuckDecisionCodec.DecodeResult(receipt.ResultBytes);
                if (!entries.TryGetValue(grant.Request.Id, out var entry) || entry.ReceiptRecord != null
                    || record.Id != ReceiptId(grant.Request.Id) || record.Fingerprint != RequestHash(grant.Request)
                    || record.Before.Length != 0 || record.Metadata.Length != 0 || record.Deadline != 0 || record.State == InventoryDecision.Aborted
                    || receipt.HeadRevision != result.Revision + 1 || receipt.HeadImage != result.FinalImage
                    || receipt.Anchors.Count != 1 || receipt.Anchors[0].Revision != grant.Revision || receipt.Anchors[0].Image != grant.BaseImage
                    || entry.GrantRecord.State != InventoryDecision.Decided || entry.ResultRecord?.State != InventoryDecision.Committed
                    || !receipt.GrantBytes.SequenceEqual(entry.GrantRecord.Metadata) || !receipt.ResultBytes.SequenceEqual(entry.ResultRecord.After))
                    throw new InvalidDataException("Invalid retained recovery receipt.");
                entry.Receipt = receipt; entry.ReceiptRecord = record;
            }
            }
            catch { journal.Dispose(); throw; }
        }

        public LuckDecisionLedger(string path, string ownerEpoch, long world, long player, int maximumOperations = 128)
        {
            LuckDecisionCodec.Text(ownerEpoch, 128);
            if (world == 0 || player == 0 || maximumOperations < 2 || maximumOperations > 256) throw new InvalidDataException("Invalid luck store identity or segment capacity.");
            this.path = Path.GetFullPath(path); storeWorld = world; storePlayer = player;
            this.ownerEpoch = ownerEpoch; this.maximumOperations = maximumOperations;
            journal = null!;
            try
            {
                OpenStore();
                Load();
                if (entries.Values.Any(e => Key(e.Grant.Request) != (world, player))) throw new InvalidDataException("Foreign luck store identity.");
                if (checkpoint != null && (checkpoint.World != world || checkpoint.Player != player)) throw new InvalidDataException("Foreign luck checkpoint identity.");
                long floor = ReadFloor(); revisions.TryGetValue((world, player), out long revision); revisions[(world, player)] = Math.Max(floor, revision);
                if (checkpoint != null && floor > checkpoint.HeadRevision) throw new InvalidDataException("Luck floor exceeds recovery checkpoint.");
                if (entries.Count == 0 && checkpoint == null && AdmittedStore()) throw new InvalidDataException("Admitted luck journal is empty.");
                InitializeStoreMarker();
                if (entries.Count != 0 || checkpoint != null) MarkStoreAdmitted();
                if (entries.Count >= maximumOperations && entries.Values.All(Terminal))
                    Compact(entries.Values.OrderByDescending(e => e.Grant.Revision).First());
            }
            catch { journal?.Dispose(); storeLock?.Dispose(); throw; }
        }

        public string OwnerEpoch => ownerEpoch;
        public bool MatchesStore(long world, long player) => !unavailable && path != null && storeWorld == world && storePlayer == player;
        public long Revision(long world, long player) => revisions.TryGetValue((world, player), out long revision) ? revision : 0;

        public bool Held(long world, long player) => entries.Values.Any(e => Key(e.Grant.Request) == (world, player) && !Terminal(e));

        public IReadOnlyList<LuckRetainedOperation> RetainedOperations()
        {
            if (entries.Count > maximumOperations) throw new InvalidDataException("Retained luck operations exceed configured capacity.");
            return Array.AsReadOnly(entries.Values.OrderBy(e => e.Grant.Revision).ThenBy(e => e.Grant.Request.Id, StringComparer.Ordinal)
                .Select(e => new LuckRetainedOperation(e.Grant, e.Result, e.GrantRecord, e.ResultRecord, e.GrantRecord.State != InventoryDecision.Committed && Terminal(e))).ToArray());
        }

        // Runtime must reconcile and read back the exact final player image before committing this existing result.
        public bool RecoverApplied(long world, long player, LuckRetainedOperation retained, string confirmedFinalImage)
        {
            if (unavailable || path == null || storeWorld != world || storePlayer != player || retained == null
                || (retained.State != LuckRecoveryState.DurableResultPendingApply && retained.State != LuckRecoveryState.AppliedAwaitingReceipt)
                || Key(retained.Grant.Request) != (world, player) || retained.Result == null
                || !entries.TryGetValue(retained.Grant.Request.Id, out var entry) || entry.Result == null || entry.ResultRecord == null
                || entry.GrantRecord.State != InventoryDecision.Decided
                || (entry.ResultRecord.State != InventoryDecision.Decided && entry.ResultRecord.State != InventoryDecision.Committed)
                || confirmedFinalImage != entry.Result.FinalImage
                || retained.GrantDigest != LuckDecisionCodec.Hash(entry.GrantRecord.Metadata)
                || retained.ResultDigest != LuckDecisionCodec.Hash(entry.ResultRecord.After)
                || !retained.GrantBytes.SequenceEqual(entry.GrantRecord.Metadata)
                || retained.ResultBytes == null || !retained.ResultBytes.SequenceEqual(entry.ResultRecord.After)) return false;
            long next = checked(entry.Result.Revision + 1);
            if (entry.ResultRecord.State == InventoryDecision.Committed) return Revision(world, player) == next;
            if (Revision(world, player) != entry.Result.Revision) return false;
            try { journal.Transition(entry.ResultRecord, InventoryDecision.Committed); }
            catch { unavailable = true; throw; }
            revisions[(world, player)] = next;
            return true;
        }

        public LuckDecisionReply Ask(LuckDecisionRequest request, string currentAuthorityImage)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (unavailable || path != null && Key(request) != (storeWorld, storePlayer)) return Refused();
            if (entries.TryGetValue(request.Id, out var retained))
                return Same(retained.Grant.Request, request) && (retained.Grant.OwnerEpoch == ownerEpoch || retained.GrantRecord.State == InventoryDecision.Committed) ? Reply(retained) : Refused();
            if (Held(request.World, request.Player)) return new LuckDecisionReply(LuckDecisionState.Busy);
            if (request.ExpectedOwnerEpoch != ownerEpoch || request.ExpectedRevision != Revision(request.World, request.Player))
                return new LuckDecisionReply(LuckDecisionState.Stale);
            if (entries.Count >= maximumOperations || !journal.CanAdmit) return Refused();
            LuckDecisionCodec.Image(currentAuthorityImage);
            if (HasRecoveryReceipts && (!TryCounterPlan(request.World, request.Player, currentAuthorityImage, out var plan) || plan == null || plan.FinalImage != currentAuthorityImage)) return Refused();
            revisions.TryGetValue(Key(request), out long revision);
            var grant = new LuckDecisionGrant(request, ownerEpoch, revision, currentAuthorityImage);
            byte[] metadata = LuckDecisionCodec.EncodeGrant(grant), image = Encoding.UTF8.GetBytes(currentAuthorityImage);
            MarkStoreAdmitted();
            var record = journal.Prepare(GrantId(request.Id), GrantSource, RequestHash(request), 0, image, image, metadata);
            // Retain even a failed decision flush; no later request may bypass a possibly durable grant.
            var entry = new Entry(grant, record); entries.Add(request.Id, entry);
            journal.Transition(record, InventoryDecision.Decided);
            return Reply(entry);
        }

        public LuckDecisionReply Submit(LuckDecisionResult result, string validatedFinalImage)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (unavailable || !entries.TryGetValue(result.Request.Id, out var entry) || Recovered(entry) || !Matches(entry.Grant, result) || result.FinalImage != validatedFinalImage) return Refused();
            byte[] encoded = LuckDecisionCodec.EncodeResult(result);
            if (entry.Result != null)
                return encoded.SequenceEqual(LuckDecisionCodec.EncodeResult(entry.Result)) ? Reply(entry) : Refused();
            if (entry.Grant.OwnerEpoch != ownerEpoch || entry.GrantRecord.State != InventoryDecision.Decided) return Refused();
            var record = journal.Prepare(ResultId(result.Request.Id), ResultSource, RequestHash(result.Request), 0,
                Encoding.UTF8.GetBytes(entry.Grant.BaseImage), encoded);
            entry.Result = result; entry.ResultRecord = record;
            journal.Transition(record, InventoryDecision.Decided);
            return Reply(entry);
        }

        public LuckDecisionReply MarkApplied(LuckDecisionResult result)
        {
            if (unavailable || !entries.TryGetValue(result.Request.Id, out var entry) || Recovered(entry) || entry.Result == null || entry.ResultRecord == null ||
                entry.Grant.OwnerEpoch != ownerEpoch || !LuckDecisionCodec.EncodeResult(result).SequenceEqual(LuckDecisionCodec.EncodeResult(entry.Result))) return Refused();
            if (entry.ResultRecord.State == InventoryDecision.Prepared) return Refused();
            if (entry.ResultRecord.State != InventoryDecision.Committed)
            {
                journal.Transition(entry.ResultRecord, InventoryDecision.Committed);
                revisions[Key(result.Request)] = checked(result.Revision + 1);
            }
            return Reply(entry);
        }

        public LuckDecisionReply Acknowledge(LuckDecisionGrant grant)
        {
            if (unavailable || !entries.TryGetValue(grant.Request.Id, out var entry) || Recovered(entry) || !LuckDecisionCodec.EncodeGrant(grant).SequenceEqual(LuckDecisionCodec.EncodeGrant(entry.Grant)) ||
                entry.Grant.OwnerEpoch != ownerEpoch || entry.ResultRecord?.State != InventoryDecision.Committed) return Refused();
            bool alreadyAcknowledged = entry.GrantRecord.State == InventoryDecision.Committed;
            journal.Transition(entry.GrantRecord, InventoryDecision.Committed);
            var reply = Reply(entry);
            if (!alreadyAcknowledged && path != null && entries.Count >= maximumOperations) Compact(entry);
            return reply;
        }

        long ReadFloor()
        {
            string file = path + ".floor";
            if (Directory.Exists(file)) throw new InvalidDataException("Luck retirement floor is not a file.");
            if (!File.Exists(file))
            {
                if (checkpoint == null && Directory.GetFiles(Path.GetDirectoryName(path!)!, Path.GetFileName(path) + ".retired-*").Length != 0)
                    throw new InvalidDataException("Luck retirement floor is missing.");
                return 0;
            }
            if (new FileInfo(file).Length != 60) throw new InvalidDataException("Invalid luck retirement floor.");
            byte[] bytes = File.ReadAllBytes(file);
            if (bytes.Length != 60) throw new InvalidDataException("Luck retirement floor changed during read.");
            byte[] body = bytes.Take(28).ToArray();
            if (!bytes.Skip(28).SequenceEqual(HexBytes(LuckDecisionCodec.Hash(body)))) throw new InvalidDataException("Luck retirement checksum mismatch.");
            using var stream = new MemoryStream(body); using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != 1 || reader.ReadInt64() != storeWorld || reader.ReadInt64() != storePlayer) throw new InvalidDataException("Foreign luck retirement floor.");
            long floor = reader.ReadInt64(); if (floor < 0 || floor == long.MaxValue) throw new InvalidDataException("Invalid luck retirement revision."); return floor;
        }
        static byte[] HexBytes(string text) => Enumerable.Range(0, text.Length / 2).Select(i => Convert.ToByte(text.Substring(i * 2, 2), 16)).ToArray();
        void Compact(Entry last)
        {
            if (HasRecoveryReceipts) { CompactRecovery(last); return; }
            if (entries.Values.Any(e => e.GrantRecord.State != InventoryDecision.Committed)) return;
            try
            {
                using var body = new MemoryStream(); using (var writer = new BinaryWriter(body, Encoding.UTF8, true))
                { writer.Write(1); writer.Write(storeWorld); writer.Write(storePlayer); writer.Write(Revision(storeWorld, storePlayer)); }
                byte[] floor = body.ToArray(); string temporary = path + ".floor-" + Guid.NewGuid().ToString("N");
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(floor, 0, floor.Length); byte[] hash = HexBytes(LuckDecisionCodec.Hash(floor)); stream.Write(hash, 0, hash.Length); stream.Flush(true); }
                if (File.Exists(path + ".floor")) File.Replace(temporary, path + ".floor", path + ".floor.bak");
                else File.Move(temporary, path + ".floor");
                journal.Dispose();
                File.Move(path!, path + ".retired-" + Revision(storeWorld, storePlayer).ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
                journal = new InventoryDecisionJournal(path!);
                entries.Clear();
                // Keep the latest acknowledgment available for a lost confirmation; older IDs are fenced by their revision.
                var grant = last.Grant; var result = last.Result!; byte[] image = Encoding.UTF8.GetBytes(grant.BaseImage);
                var g = journal.Prepare(GrantId(grant.Request.Id), GrantSource, RequestHash(grant.Request), 0, image, image, LuckDecisionCodec.EncodeGrant(grant));
                journal.Transition(g, InventoryDecision.Decided);
                var r = journal.Prepare(ResultId(grant.Request.Id), ResultSource, RequestHash(grant.Request), 0, image, LuckDecisionCodec.EncodeResult(result));
                journal.Transition(r, InventoryDecision.Decided); journal.Transition(r, InventoryDecision.Committed); journal.Transition(g, InventoryDecision.Committed);
                entries.Add(grant.Request.Id, new Entry(grant, g) { Result = result, ResultRecord = r });
                if (ReadFloor() != Revision(storeWorld, storePlayer) || journal.Snapshot().Any(record => record.State != InventoryDecision.Committed))
                    throw new InvalidDataException("Luck retirement checkpoint verification failed.");
                PruneArchives();
            }
            catch { unavailable = true; throw; }
        }
        void PruneArchives()
        {
            string prefix = Path.GetFileName(path) + ".retired-";
            long retirementHead = checkpoint?.HeadRevision ?? ReadFloor();
            var known = new List<(string Path, long Revision, string Hash)>();
            foreach (string archive in Directory.GetFiles(Path.GetDirectoryName(path!)!, prefix + "*"))
            {
                string suffix = Path.GetFileName(archive).Substring(prefix.Length); int split = suffix.IndexOf('-');
                if (split < 1 || !long.TryParse(suffix.Substring(0, split), NumberStyles.None, CultureInfo.InvariantCulture, out long revision) ||
                    !Guid.TryParseExact(suffix.Substring(split + 1), "N", out _) || revision > retirementHead) continue;
                string inspection = path + ".inspect-" + Guid.NewGuid().ToString("N");
                try
                {
                    var info = new FileInfo(archive);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length > 268435456) continue;
                    byte[] before = File.ReadAllBytes(archive); File.WriteAllBytes(inspection, before);
                    using (var check = new InventoryDecisionJournal(inspection))
                    {
                        var records = check.Snapshot();
                        if (records.Count == 0) continue;
                        using var loaded = new LuckDecisionLedger(check, ownerEpoch, 256);
                        if (loaded.entries.Values.Any(e => Key(e.Grant.Request) != (storeWorld, storePlayer) || !loaded.Terminal(e))
                            || loaded.Revision(storeWorld, storePlayer) != revision
                            || loaded.checkpoint != null && (loaded.checkpoint.World != storeWorld || loaded.checkpoint.Player != storePlayer)) continue;
                        if (loaded.HasRecoveryReceipts && !loaded.ValidateArchiveProof(storeWorld, storePlayer)) continue;
                    }
                    if (!before.SequenceEqual(File.ReadAllBytes(inspection))) continue;
                    known.Add((archive, revision, LuckDecisionCodec.Hash(before)));
                }
                catch (InvalidDataException) { }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                finally { if (File.Exists(inspection)) File.Delete(inspection); }
            }
            foreach (var old in known.OrderByDescending(a => a.Revision).ThenByDescending(a => a.Path, StringComparer.Ordinal).Skip(3))
                if (LuckDecisionCodec.Hash(File.ReadAllBytes(old.Path)) == old.Hash) File.Delete(old.Path);
        }
        public void Dispose() { unavailable = true; if (path != null) journal.Dispose(); storeLock?.Dispose(); }

        LuckDecisionReply Reply(Entry entry)
        {
            if (Recovered(entry)) return Refused();
            var state = entry.GrantRecord.State == InventoryDecision.Committed ? LuckDecisionState.Acknowledged
                : entry.ResultRecord?.State == InventoryDecision.Committed ? LuckDecisionState.Applied
                : entry.ResultRecord?.State == InventoryDecision.Decided ? LuckDecisionState.Prepared
                : entry.GrantRecord.State == InventoryDecision.Decided && entry.ResultRecord == null ? LuckDecisionState.Granted : LuckDecisionState.Refused;
            return new LuckDecisionReply(state, entry.Grant, entry.Result);
        }
        static LuckDecisionReply Refused() => new LuckDecisionReply(LuckDecisionState.Refused);
        static (long World, long Player) Key(LuckDecisionRequest request) => (request.World, request.Player);
        static string GrantId(string id) => "luck.grant/" + id;
        static string ResultId(string id) => "luck.result/" + id;
        static string RequestHash(LuckDecisionRequest request) => LuckDecisionCodec.Hash(LuckDecisionCodec.EncodeRequest(request));
        static bool Same(LuckDecisionRequest left, LuckDecisionRequest right) => LuckDecisionCodec.EncodeRequest(left).SequenceEqual(LuckDecisionCodec.EncodeRequest(right));
        static bool Matches(LuckDecisionGrant grant, LuckDecisionResult result) => Same(grant.Request, result.Request) && grant.OwnerEpoch == result.OwnerEpoch && grant.Revision == result.Revision && grant.BaseHash == result.BaseHash;
    }
}
