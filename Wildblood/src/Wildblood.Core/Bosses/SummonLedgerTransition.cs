// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ExtraPotions.Wildblood.Transfers;

namespace ExtraPotions.Wildblood.Bosses
{
    /// <summary>Freezes one altar update for the existing flushed before/after-image participant.</summary>
    public sealed class SummonLedgerTransition
    {
        public string Id { get; }
        public string Source { get; }
        public string Fingerprint { get; }
        readonly byte[] before, after;
        public byte[] Before => (byte[])before.Clone();
        public byte[] After => (byte[])after.Clone();

        public SummonLedgerTransition(SummonLedgerSnapshot? previous, SummonLedgerSnapshot next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (previous != null && (previous.WorldUid != next.WorldUid || previous.AltarId != next.AltarId))
                throw new InvalidDataException("Summon transition changed its altar binding.");
            if (next.Revision == 0)
            {
                if (next.Stage != SummonStage.Pending || previous != null && (previous.BlocksSummon || previous.SummonId == next.SummonId))
                    throw new InvalidOperationException("Altar already has an unsettled summon.");
            }
            else if (previous == null || previous.SummonId != next.SummonId || previous.Context != next.Context || next.Revision != previous.Revision + 1
                || previous.Offerings.Count != next.Offerings.Count
                || previous.Offerings.Where((x, i) => x.Prefab != next.Offerings[i].Prefab || x.Amount != next.Offerings[i].Amount).Any())
                throw new InvalidDataException("Summon transition changed its recorded cost or history.");
            if (previous != null && next.Revision != 0)
            {
                bool allowed = previous.Stage == SummonStage.Pending && next.Stage == SummonStage.Alive
                    || previous.Stage == SummonStage.Alive && (next.Stage == SummonStage.Alive || next.Stage == SummonStage.Killed || next.Stage == SummonStage.RefundDecided)
                    || previous.Stage == SummonStage.RefundDecided && next.Stage == SummonStage.RefundDelivered;
                if (!allowed || previous.Stage != SummonStage.Pending && !previous.BossIds.SequenceEqual(next.BossIds)
                    || previous.KilledBossIds.Any(id => !next.KilledBossIds.Contains(id)) || !previous.RefundAllowed && next.RefundAllowed)
                    throw new InvalidDataException("Summon transition reversed its terminal facts.");
            }
            before = previous?.Encode() ?? Array.Empty<byte>(); after = next.Encode();
            Source = "boss-summon:" + next.WorldUid.ToString(CultureInfo.InvariantCulture) + ":" + next.AltarId;
            Id = "boss-summon:" + next.SummonId + ":" + next.Revision.ToString(CultureInfo.InvariantCulture);
            using (var sha = SHA256.Create())
            {
                var binding = System.Text.Encoding.UTF8.GetBytes(Source + "\n" + Id + "\n");
                using (var stream = new MemoryStream())
                {
                    stream.Write(binding, 0, binding.Length);
                    using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                    { writer.Write(before.Length); writer.Write(before); writer.Write(after.Length); writer.Write(after); }
                    Fingerprint = BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public InventoryDecision Commit(DurableInventoryParticipant participant)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            var prepared = participant.Prepare(Id, Source, Fingerprint, Before, After);
            return prepared == InventoryDecision.Prepared || prepared == InventoryDecision.Decided
                || prepared == InventoryDecision.Committed || prepared == InventoryDecision.RecoveryRequired
                ? participant.Commit(Id) : prepared;
        }
    }
}
