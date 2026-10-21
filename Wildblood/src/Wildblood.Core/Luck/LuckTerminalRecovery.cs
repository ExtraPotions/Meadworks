// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExtraPotions.Wildblood.Transfers;

namespace ExtraPotions.Wildblood.Luck
{
    public sealed class LuckCounterPlan
    {
        public long World { get; }
        public long Player { get; }
        public long Revision { get; }
        public string FinalImage { get; }
        public string RecoveryOperationId { get; }
        public string RecoveryReceiptDigest { get; }
        public string ProofDigest { get; }
        internal string OwnerEpoch { get; }
        internal LuckRecoveryCheckpoint Recovery { get; }
        internal IReadOnlyList<LuckCounterAnchor> Anchors { get; }

        internal LuckCounterPlan(long world, long player, long revision, string image, string epoch,
            LuckRecoveryCheckpoint recovery, IEnumerable<LuckCounterAnchor> anchors, string digest)
        {
            World = world; Player = player; Revision = revision; FinalImage = image; OwnerEpoch = epoch;
            Recovery = recovery; Anchors = Array.AsReadOnly(anchors.ToArray()); ProofDigest = digest;
            var grant = LuckDecisionCodec.DecodeGrant(recovery.GrantBytes);
            var result = LuckDecisionCodec.DecodeResult(recovery.ResultBytes);
            RecoveryOperationId = grant.Request.Id;
            var receipt = LuckRecoveryCheckpoint.Create(world, player, checked(result.Revision + 1), result.FinalImage,
                recovery.RecoveryOwnerEpoch, recovery.GrantBytes, recovery.ResultBytes,
                new[] { new LuckCounterAnchor(grant.Revision, grant.BaseImage) });
            RecoveryReceiptDigest = LuckDecisionCodec.Hash(receipt.Encode());
        }
    }

    public sealed partial class LuckDecisionLedger
    {
        public bool HasRecoveryReceipts => checkpoint != null || entries.Values.Any(e => e.ReceiptRecord != null);

        static string ReceiptId(string id) => "luck.recovery/" + id;

        static bool OriginalMatches(LuckRecoveryCheckpoint proof, Entry entry) => entry.ResultRecord != null
            && proof.GrantBytes.SequenceEqual(entry.GrantRecord.Metadata) && proof.ResultBytes.SequenceEqual(entry.ResultRecord.After)
            && proof.World == entry.Grant.Request.World && proof.Player == entry.Grant.Request.Player;

        bool Recovered(Entry entry) => entry.GrantRecord.State == InventoryDecision.Decided
            && entry.ResultRecord?.State == InventoryDecision.Committed
            && (entry.ReceiptRecord?.State == InventoryDecision.Committed && entry.Receipt != null && OriginalMatches(entry.Receipt, entry)
                || checkpoint != null && OriginalMatches(checkpoint, entry));

        bool Terminal(Entry entry) => entry.ResultRecord?.State == InventoryDecision.Committed
            && (entry.GrantRecord.State == InventoryDecision.Committed || Recovered(entry));

        public bool RecoverTerminal(long world, long player, LuckRetainedOperation retained, string confirmedFinalImage)
        {
            if (!MatchesStore(world, player) || retained == null || retained.Result == null
                || retained.Grant.Request.World != world || retained.Grant.Request.Player != player
                || confirmedFinalImage != retained.Result.FinalImage || retained.ResultBytes == null) return false;
            if (!entries.TryGetValue(retained.Grant.Request.Id, out var entry))
                return checkpoint != null && checkpoint.World == world && checkpoint.Player == player
                    && checkpoint.GrantBytes.SequenceEqual(retained.GrantBytes) && checkpoint.ResultBytes.SequenceEqual(retained.ResultBytes);
            if (entry.GrantRecord.State != InventoryDecision.Decided || entry.ResultRecord?.State != InventoryDecision.Committed
                || entry.Result == null || confirmedFinalImage != entry.Result.FinalImage
                || retained.GrantDigest != LuckDecisionCodec.Hash(entry.GrantRecord.Metadata)
                || retained.ResultDigest != LuckDecisionCodec.Hash(entry.ResultRecord.After)
                || !retained.GrantBytes.SequenceEqual(entry.GrantRecord.Metadata) || !retained.ResultBytes.SequenceEqual(entry.ResultRecord.After)) return false;
            if (Recovered(entry)) return true;
            if (Revision(world, player) != checked(entry.Result.Revision + 1)) return false;
            var receipt = entry.Receipt ?? LuckRecoveryCheckpoint.Create(world, player, checked(entry.Result.Revision + 1),
                confirmedFinalImage, ownerEpoch, entry.GrantRecord.Metadata, entry.ResultRecord.After,
                new[] { new LuckCounterAnchor(entry.Grant.Revision, entry.Grant.BaseImage) });
            if (!OriginalMatches(receipt, entry) || receipt.HeadRevision != entry.Result.Revision + 1
                || receipt.HeadImage != confirmedFinalImage || receipt.Anchors.Count != 1
                || receipt.Anchors[0].Revision != entry.Grant.Revision || receipt.Anchors[0].Image != entry.Grant.BaseImage) return false;
            byte[] bytes = receipt.Encode();
            var record = entry.ReceiptRecord;
            if (record != null && (record.Id != ReceiptId(entry.Grant.Request.Id) || record.Source != ReceiptSource
                || record.Fingerprint != RequestHash(entry.Grant.Request) || record.Deadline != 0
                || record.Before.Length != 0 || record.Metadata.Length != 0 || !record.After.SequenceEqual(bytes)
                || record.State != InventoryDecision.Prepared && record.State != InventoryDecision.Decided)) return false;
            try
            {
                if (record == null)
                {
                    record = journal.Prepare(ReceiptId(entry.Grant.Request.Id), ReceiptSource, RequestHash(entry.Grant.Request),
                        0, Array.Empty<byte>(), bytes);
                    entry.Receipt = receipt; entry.ReceiptRecord = record;
                }
                if (record.State == InventoryDecision.Prepared) journal.Transition(record, InventoryDecision.Decided);
                journal.Transition(record, InventoryDecision.Committed);
                var readback = journal.Find(record.Id);
                if (!ReferenceEquals(readback, record) || record.State != InventoryDecision.Committed
                    || !record.After.SequenceEqual(bytes) || !LuckRecoveryCheckpoint.Decode(record.After).Encode().SequenceEqual(bytes))
                    throw new InvalidDataException("Recovery receipt readback failed.");
                if (entries.Count >= maximumOperations) Compact(entry);
                return true;
            }
            catch { unavailable = true; throw; }
        }

        public bool TryCounterPlan(long world, long player, string image, out LuckCounterPlan? plan)
        {
            plan = null;
            if (!MatchesStore(world, player) || !TryCounterProof(out var proof) || proof == null || !ContainsImage(proof, image)) return false;
            plan = proof; return true;
        }

        public bool ValidateCounterPlan(LuckCounterPlan plan, string image)
        {
            return plan != null && MatchesStore(plan.World, plan.Player) && plan.OwnerEpoch == ownerEpoch
                && TryCounterProof(out var current) && current != null && current.ProofDigest == plan.ProofDigest
                && current.RecoveryReceiptDigest == plan.RecoveryReceiptDigest && current.Revision == plan.Revision
                && current.FinalImage == plan.FinalImage && current.RecoveryOperationId == plan.RecoveryOperationId
                && ContainsImage(current, image);
        }

        static bool ContainsImage(LuckCounterPlan plan, string image) => image == plan.FinalImage || plan.Anchors.Any(a => a.Image == image);

        bool TryCounterProof(out LuckCounterPlan? plan) => TryCounterProof(storeWorld, storePlayer, true, out plan);

        bool ValidateArchiveProof(long world, long player) => path == null && world != 0 && player != 0
            && TryCounterProof(world, player, false, out _);

        bool TryCounterProof(long world, long player, bool requireStore, out LuckCounterPlan? plan)
        {
            plan = null;
            if (unavailable || world == 0 || player == 0 || requireStore && !MatchesStore(world, player)
                || !HasRecoveryReceipts || entries.Count > maximumOperations
                || entries.Values.Any(e => Key(e.Grant.Request) != (world, player) || !Terminal(e))) return false;
            LuckRecoveryCheckpoint? recovery = checkpoint;
            var points = new List<LuckCounterAnchor>();
            if (checkpoint != null)
            {
                if (checkpoint.World != world || checkpoint.Player != player) return false;
                points.AddRange(checkpoint.Anchors);
                points.Add(new LuckCounterAnchor(checkpoint.HeadRevision, checkpoint.HeadImage));
            }
            using var evidence = new MemoryStream(); using var writer = new BinaryWriter(evidence, Encoding.UTF8, true);
            writer.Write(world); writer.Write(player); writer.Write(ownerEpoch);
            byte[] checkpointBytes = checkpoint?.Encode() ?? Array.Empty<byte>();
            writer.Write(checkpointBytes.Length); writer.Write(checkpointBytes);
            foreach (var entry in entries.Values.OrderBy(e => e.Grant.Revision).ThenBy(e => e.Grant.Request.Id, StringComparer.Ordinal))
            {
                var grant = entry.Grant; var result = entry.Result!;
                if (entry.ResultRecord == null || !Matches(grant, result)) return false;
                byte[] grantBytes = entry.GrantRecord.Metadata, resultBytes = entry.ResultRecord.After;
                writer.Write(grantBytes.Length); writer.Write(grantBytes); writer.Write((int)entry.GrantRecord.State);
                writer.Write(resultBytes.Length); writer.Write(resultBytes); writer.Write((int)entry.ResultRecord.State);
                byte[] receiptBytes = entry.ReceiptRecord?.After ?? Array.Empty<byte>();
                writer.Write(receiptBytes.Length); writer.Write(receiptBytes);
                writer.Write((int)(entry.ReceiptRecord?.State ?? InventoryDecision.Rejected));
                if (entry.ReceiptRecord != null)
                {
                    if (entry.ReceiptRecord.State != InventoryDecision.Committed || entry.Receipt == null || !OriginalMatches(entry.Receipt, entry)) return false;
                    if (recovery == null || LuckDecisionCodec.DecodeResult(recovery.ResultBytes).Revision < result.Revision) recovery = entry.Receipt;
                }
                if (checkpoint != null && grant.Revision < checkpoint.HeadRevision)
                {
                    bool exactOriginal = OriginalMatches(checkpoint, entry);
                    var start = points.FirstOrDefault(p => p.Revision == grant.Revision);
                    var end = points.FirstOrDefault(p => p.Revision == grant.Revision + 1);
                    if (!exactOriginal && (start == null || end == null || start.Image != grant.BaseImage || end.Image != result.FinalImage)) return false;
                    continue;
                }
                if (points.Count == 0) points.Add(new LuckCounterAnchor(grant.Revision, grant.BaseImage));
                var head = points[points.Count - 1];
                if (grant.Revision != head.Revision || grant.BaseImage != head.Image) return false;
                points.Add(new LuckCounterAnchor(checked(result.Revision + 1), result.FinalImage));
            }
            if (recovery == null || points.Count < 2) return false;
            var final = points[points.Count - 1];
            if (final.Revision != Revision(world, player)) return false;
            var anchors = points.Take(points.Count - 1).Skip(Math.Max(0, points.Count - 1 - maximumOperations)).ToArray();
            writer.Write(final.Revision); writer.Write(final.Image);
            foreach (var anchor in anchors) { writer.Write(anchor.Revision); writer.Write(anchor.Image); }
            writer.Flush();
            plan = new LuckCounterPlan(world, player, final.Revision, final.Image, ownerEpoch, recovery, anchors,
                LuckDecisionCodec.Hash(evidence.ToArray()));
            return true;
        }
    }
}
