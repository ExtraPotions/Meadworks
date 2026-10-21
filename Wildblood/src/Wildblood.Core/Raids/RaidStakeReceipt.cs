// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;

namespace ExtraPotions.Wildblood.Raids
{
    public enum RaidStakeStage : byte { Debited = 1, EventCommitted = 2, Terminal = 3 }

    /// <summary>Root-scoped checkpoint facts; a codec cannot prove that a native debit occurred.</summary>
    public sealed class RaidStakeReceipt
    {
        const int EncodedLength = 104;
        public long WorldUid { get; }
        public string ChestToken { get; }
        public string EventRoot { get; }
        public string RequestId { get; }
        public string PlanHash { get; }
        public RaidStakeStage Stage { get; }
        public byte EndReason { get; }
        public long EndedAtMs { get; }

        public RaidStakeReceipt(long worldUid, string chestToken, string eventRoot, string requestId, string planHash,
            RaidStakeStage stage, byte endReason = 0, long endedAtMs = 0)
        {
            StakeWire.Require(worldUid != 0, "Missing receipt world.");
            StakeWire.Id(chestToken); StakeWire.Id(eventRoot); StakeWire.Id(requestId); StakeWire.Hex(planHash, 32, false);
            StakeWire.Require(stage >= RaidStakeStage.Debited && stage <= RaidStakeStage.Terminal, "Unknown receipt stage.");
            StakeWire.Require(stage == RaidStakeStage.Terminal ? endReason >= 1 && endReason <= 5 && endedAtMs >= 0
                : endReason == 0 && endedAtMs == 0, "Invalid receipt terminal facts.");
            WorldUid = worldUid; ChestToken = chestToken; EventRoot = eventRoot; RequestId = requestId; PlanHash = planHash;
            Stage = stage; EndReason = endReason; EndedAtMs = endedAtMs;
        }

        public byte[] Encode()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, StakeWire.Utf8, true))
            {
                writer.Write(0x5252574Du); writer.Write((ushort)1); writer.Write(WorldUid);
                writer.Write(StakeWire.Hex(ChestToken, 16, true)); writer.Write(StakeWire.Hex(EventRoot, 16, true)); writer.Write(StakeWire.Hex(RequestId, 16, true));
                writer.Write(StakeWire.Hex(PlanHash, 32, false)); writer.Write((byte)Stage); writer.Write(EndReason); writer.Write(EndedAtMs);
                return stream.ToArray();
            }
        }

        public static RaidStakeReceipt Decode(byte[] bytes)
        {
            StakeWire.Require(bytes != null && bytes.Length == EncodedLength, "Invalid raid receipt size.");
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, StakeWire.Utf8, true))
                {
                    StakeWire.Require(reader.ReadUInt32() == 0x5252574Du && reader.ReadUInt16() == 1, "Unknown raid receipt format.");
                    return new RaidStakeReceipt(reader.ReadInt64(), StakeWire.ReadHex(reader, 16), StakeWire.ReadHex(reader, 16),
                        StakeWire.ReadHex(reader, 16), StakeWire.ReadHex(reader, 32), (RaidStakeStage)reader.ReadByte(), reader.ReadByte(), reader.ReadInt64());
                }
            }
            catch (EndOfStreamException error) { throw new InvalidDataException("Truncated raid receipt.", error); }
        }

        public void Verify(RaidStakePlan plan, string expectedHash)
        {
            StakeWire.Hex(expectedHash, 32, false);
            StakeWire.Require(plan != null && WorldUid == plan.WorldUid && ChestToken == plan.ChestToken && EventRoot == plan.EventRoot
                && RequestId == plan.RequestId && PlanHash == expectedHash, "Raid receipt binding mismatch.");
            StakeWire.Require(Stage != RaidStakeStage.Terminal || EndedAtMs >= plan.StartedAtMs, "Receipt ends before raid started.");
        }

        public RaidStakeReceipt Merge(RaidStakeReceipt next, RaidStakePlan plan, string expectedHash)
        {
            Verify(plan, expectedHash); StakeWire.Require(next != null, "Missing receipt update."); next.Verify(plan, expectedHash);
            if (Stage == next.Stage)
            {
                StakeWire.Require(EndReason == next.EndReason && EndedAtMs == next.EndedAtMs, "Conflicting terminal facts.");
                return this;
            }
            StakeWire.Require((int)next.Stage == (int)Stage + 1, "Receipt cannot skip or reverse a checkpoint.");
            return next;
        }
    }
}
