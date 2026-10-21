// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ExtraPotions.Wildblood.Transfers;

namespace ExtraPotions.Wildblood.Luck
{
    public sealed partial class LuckDecisionLedger
    {
        void OpenStore()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path!)!);
            string marker = path + ".owner";
            bool existingMarker = File.Exists(marker);
            storeLock = new FileStream(marker, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (existingMarker && storeLock.Length != 0)
            {
                if (storeLock.Length != 21) throw new InvalidDataException("Invalid luck store marker.");
                using var reader = new BinaryReader(storeLock, Encoding.UTF8, true);
                if (reader.ReadInt32() != 1 || reader.ReadInt64() != storeWorld || reader.ReadInt64() != storePlayer || reader.ReadByte() > 1)
                    throw new InvalidDataException("Foreign or invalid luck store marker.");
                if (!File.Exists(path)) throw new InvalidDataException("Initialized luck journal is missing.");
            }
            journal = new InventoryDecisionJournal(path!);
        }

        void InitializeStoreMarker()
        {
            if (storeLock == null || storeLock.Length != 0) return;
            using var writer = new BinaryWriter(storeLock, Encoding.UTF8, true);
            writer.Write(1); writer.Write(storeWorld); writer.Write(storePlayer); writer.Write((byte)0); writer.Flush(); storeLock.Flush(true);
        }

        bool AdmittedStore()
        {
            if (storeLock == null || storeLock.Length == 0) return false;
            storeLock.Position = 20;
            return storeLock.ReadByte() == 1;
        }

        void MarkStoreAdmitted()
        {
            if (storeLock == null || AdmittedStore()) return;
            try { storeLock.Position = 20; storeLock.WriteByte(1); storeLock.Flush(true); }
            catch { unavailable = true; throw; }
        }

        void CompactRecovery(Entry last)
        {
            if (entries.Values.Any(e => !Terminal(e))) return;
            if (!TryCounterProof(out var proof) || proof == null)
            {
                unavailable = true;
                throw new InvalidDataException("Luck counter proof cannot be retired.");
            }
            string stage = path + ".checkpoint-" + Guid.NewGuid().ToString("N");
            string archive = path + ".retired-" + proof.Revision.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            try
            {
                var next = LuckRecoveryCheckpoint.Create(storeWorld, storePlayer, proof.Revision, proof.FinalImage,
                    proof.Recovery.RecoveryOwnerEpoch, proof.Recovery.GrantBytes, proof.Recovery.ResultBytes, proof.Anchors, maximumOperations);
                byte[] payload = next.Encode();
                using (var replacement = new InventoryDecisionJournal(stage))
                {
                    var c = replacement.Prepare(CheckpointId, CheckpointSource, LuckDecisionCodec.Hash(payload), 0, Array.Empty<byte>(), payload);
                    replacement.Transition(c, InventoryDecision.Decided); replacement.Transition(c, InventoryDecision.Committed);
                    CopyEntry(replacement, last);
                }
                byte[] staged = File.ReadAllBytes(stage);
                using (var replacement = new InventoryDecisionJournal(stage, requireExisting: true))
                using (var verified = new LuckDecisionLedger(replacement, ownerEpoch, maximumOperations))
                {
                    if (verified.checkpoint == null || !verified.checkpoint.Encode().SequenceEqual(payload)
                        || verified.entries.Count != 1 || verified.entries.Values.Any(e => !verified.Terminal(e))
                        || verified.Revision(storeWorld, storePlayer) != proof.Revision)
                        throw new InvalidDataException("Staged luck checkpoint verification failed.");
                }
                if (!staged.SequenceEqual(File.ReadAllBytes(stage))) throw new InvalidDataException("Staged luck journal changed during validation.");
                // The separate store lock stays held while the WAL handle is closed for atomic replacement.
                journal.Dispose();
                File.Replace(stage, path!, archive);
                journal = new InventoryDecisionJournal(path!, requireExisting: true);
                using (var readback = new FileStream(path!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var bytes = new MemoryStream())
                {
                    readback.CopyTo(bytes);
                    if (!staged.SequenceEqual(bytes.ToArray())) throw new InvalidDataException("Installed luck checkpoint differs from staged proof.");
                }
                entries.Clear(); revisions.Clear(); checkpoint = null; Load();
                if (checkpoint == null || !checkpoint.Encode().SequenceEqual(payload) || Revision(storeWorld, storePlayer) != proof.Revision
                    || !TryCounterProof(out var installed) || installed == null || installed.FinalImage != proof.FinalImage)
                    throw new InvalidDataException("Installed luck checkpoint verification failed.");
                PruneArchives();
            }
            catch { unavailable = true; throw; }
        }

        static void CopyEntry(InventoryDecisionJournal target, Entry entry)
        {
            var grant = entry.Grant; var result = entry.Result!;
            byte[] image = Encoding.UTF8.GetBytes(grant.BaseImage);
            var g = target.Prepare(GrantId(grant.Request.Id), GrantSource, RequestHash(grant.Request), 0, image, image, LuckDecisionCodec.EncodeGrant(grant));
            target.Transition(g, InventoryDecision.Decided);
            var r = target.Prepare(ResultId(grant.Request.Id), ResultSource, RequestHash(grant.Request), 0, image, LuckDecisionCodec.EncodeResult(result));
            target.Transition(r, InventoryDecision.Decided); target.Transition(r, InventoryDecision.Committed);
            if (entry.GrantRecord.State == InventoryDecision.Committed) target.Transition(g, InventoryDecision.Committed);
        }
    }
}
