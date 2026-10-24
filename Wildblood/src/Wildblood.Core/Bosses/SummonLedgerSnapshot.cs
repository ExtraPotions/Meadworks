// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExtraPotions.Wildblood.Bosses
{
    public enum SummonStage : byte { Pending = 1, Alive = 2, Killed = 3, RefundDecided = 4, RefundDelivered = 5 }

    public sealed class SummonOffering
    {
        public string Prefab { get; }
        public int Amount { get; }
        public SummonOffering(string prefab, int amount)
        {
            SummonLedgerSnapshot.RequireText(prefab);
            if (amount <= 0) throw new InvalidDataException("Invalid summoned-boss offering amount.");
            Prefab = prefab; Amount = amount;
        }
    }

    /// <summary>Authority facts only. Refund delivery requires a native output receipt, never an absent item.</summary>
    public sealed class SummonLedgerSnapshot
    {
        public long WorldUid { get; }
        public string AltarId { get; }
        public string SummonId { get; }
        public int Revision { get; }
        public SummonStage Stage { get; }
        public IReadOnlyList<string> BossIds { get; }
        public IReadOnlyList<string> KilledBossIds { get; }
        public IReadOnlyList<SummonOffering> Offerings { get; }
        public double AbsentSince { get; }
        public bool RefundAllowed { get; }
        public string Context { get; }
        public bool BlocksSummon => Stage == SummonStage.Pending || Stage == SummonStage.Alive || Stage == SummonStage.RefundDecided;
        public string RefundId => SummonId + ":refund";

        SummonLedgerSnapshot(long worldUid, string altarId, string summonId, int revision, SummonStage stage,
            IEnumerable<string> bossIds, IEnumerable<string> killedBossIds, IEnumerable<SummonOffering> offerings,
            double absentSince, bool refundAllowed, string context = "")
        {
            RequireText(altarId); RequireText(summonId);
            if (context == null || context.Length > 2048) throw new InvalidDataException("Summon context exceeds bounds.");
            var bosses = bossIds.ToArray(); var killed = killedBossIds.ToArray(); var costs = offerings.ToArray();
            if (worldUid == 0 || revision < 0 || stage < SummonStage.Pending || stage > SummonStage.RefundDelivered
                || !Finite(absentSince) || absentSince < -1 || (absentSince < 0 && absentSince != -1)
                || bosses.Length > 16 || killed.Length > 16 || costs.Length > 32
                || bosses.Distinct(StringComparer.Ordinal).Count() != bosses.Length
                || killed.Distinct(StringComparer.Ordinal).Count() != killed.Length
                || killed.Any(id => !bosses.Contains(id, StringComparer.Ordinal)) || costs.Any(cost => cost == null))
                throw new InvalidDataException("Invalid summoned-boss ledger facts.");
            foreach (var id in bosses) RequireText(id);
            if (stage == SummonStage.Pending ? bosses.Length != 0 || killed.Length != 0 : bosses.Length == 0)
                throw new InvalidDataException("Invalid summoned-boss membership.");
            if (stage == SummonStage.Killed ? killed.Length != bosses.Length || refundAllowed
                : stage != SummonStage.Pending && killed.Length == bosses.Length)
                throw new InvalidDataException("Invalid summoned-boss kill state.");
            if (killed.Length != 0 && refundAllowed || (stage == SummonStage.RefundDecided || stage == SummonStage.RefundDelivered) && !refundAllowed
                || stage != SummonStage.Alive && absentSince != -1)
                throw new InvalidDataException("Invalid summoned-boss refund state.");
            WorldUid = worldUid; AltarId = altarId; SummonId = summonId; Revision = revision; Stage = stage;
            BossIds = Array.AsReadOnly(bosses); KilledBossIds = Array.AsReadOnly(killed); Offerings = Array.AsReadOnly(costs);
            AbsentSince = absentSince; RefundAllowed = refundAllowed;
            Context = context;
        }

        public static SummonLedgerSnapshot Start(long worldUid, string altarId, string summonId, IEnumerable<SummonOffering> offerings, string context = "")
            => new SummonLedgerSnapshot(worldUid, altarId, summonId, 0, SummonStage.Pending, Array.Empty<string>(),
                Array.Empty<string>(), offerings ?? throw new ArgumentNullException(nameof(offerings)), -1, true, context);

        SummonLedgerSnapshot Next(SummonStage stage, IEnumerable<string>? bosses = null, IEnumerable<string>? killed = null,
            double absentSince = -1, bool? refundAllowed = null)
            => new SummonLedgerSnapshot(WorldUid, AltarId, SummonId, checked(Revision + 1), stage,
                bosses ?? BossIds, killed ?? KilledBossIds, Offerings, absentSince, refundAllowed ?? RefundAllowed, Context);

        public SummonLedgerSnapshot AttachBosses(IEnumerable<string> bossIds)
        {
            if (Stage != SummonStage.Pending) throw new InvalidOperationException("Summon membership is already frozen.");
            return Next(SummonStage.Alive, bossIds ?? throw new ArgumentNullException(nameof(bossIds)));
        }

        public SummonLedgerSnapshot MarkKilled(string bossId)
        {
            if (!BossIds.Contains(bossId, StringComparer.Ordinal)) throw new InvalidDataException("Kill does not belong to this summon.");
            if (KilledBossIds.Contains(bossId, StringComparer.Ordinal)) return this;
            if (Stage != SummonStage.Alive) throw new InvalidOperationException("Summon already has a terminal decision.");
            var killed = KilledBossIds.Concat(new[] { bossId }).ToArray();
            return Next(killed.Length == BossIds.Count ? SummonStage.Killed : SummonStage.Alive, killed: killed, refundAllowed: false);
        }

        public SummonLedgerSnapshot Observe(bool enabled, bool anyPlayerNear, bool allFullHealth, bool allAlive, double now, double timeoutSeconds)
        {
            if (!Finite(now) || now < 0 || !Finite(timeoutSeconds) || timeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(now), "World clock and abandonment timeout must be finite and positive.");
            if (Stage != SummonStage.Alive) return this;
            if (!enabled || anyPlayerNear || !allFullHealth || !allAlive || !RefundAllowed)
                return AbsentSince < 0 ? this : Next(Stage);
            // A restored world may predate the durable timer. Start again instead of counting future time.
            if (AbsentSince < 0 || now < AbsentSince) return Next(Stage, absentSince: now);
            return now - AbsentSince >= timeoutSeconds ? Next(SummonStage.RefundDecided) : this;
        }

        public SummonLedgerSnapshot DeliverRefund()
        {
            if (Stage == SummonStage.RefundDelivered) return this;
            if (Stage != SummonStage.RefundDecided) throw new InvalidOperationException("Refund has not been decided.");
            return Next(SummonStage.RefundDelivered);
        }

        public byte[] Encode()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(0x5342574D); writer.Write((byte)2); writer.Write(WorldUid); writer.Write(AltarId); writer.Write(SummonId);
                writer.Write(Revision); writer.Write((byte)Stage); writer.Write(AbsentSince); writer.Write(RefundAllowed);
                writer.Write((byte)BossIds.Count); foreach (var id in BossIds) writer.Write(id);
                writer.Write((byte)KilledBossIds.Count); foreach (var id in KilledBossIds) writer.Write(id);
                writer.Write((byte)Offerings.Count); foreach (var item in Offerings) { writer.Write(item.Prefab); writer.Write(item.Amount); }
                writer.Write(Context);
                var result = stream.ToArray();
                if (result.Length > 16384) throw new InvalidDataException("Summon ledger snapshot exceeds bounds.");
                return result;
            }
        }

        public static SummonLedgerSnapshot Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length > 16384) throw new InvalidDataException("Summon ledger snapshot exceeds bounds.");
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (reader.ReadInt32() != 0x5342574D) throw new InvalidDataException("Unknown summon ledger format.");
                    byte version = reader.ReadByte();
                    if (version != 1 && version != 2) throw new InvalidDataException("Unknown summon ledger format.");
                    long world = reader.ReadInt64(); string altar = reader.ReadString(), summon = reader.ReadString();
                    int revision = reader.ReadInt32(); var stage = (SummonStage)reader.ReadByte(); double absent = reader.ReadDouble();
                    byte refundFlag = reader.ReadByte(); if (refundFlag > 1) throw new InvalidDataException("Invalid refund flag.");
                    var bosses = ReadIds(reader); var killed = ReadIds(reader); int costCount = reader.ReadByte();
                    if (costCount > 32) throw new InvalidDataException("Too many offering kinds.");
                    var costs = new SummonOffering[costCount]; for (int i = 0; i < costs.Length; i++) costs[i] = new SummonOffering(reader.ReadString(), reader.ReadInt32());
                    string context = version == 2 ? reader.ReadString() : "";
                    if (stream.Position != stream.Length) throw new InvalidDataException("Trailing summon ledger bytes.");
                    return new SummonLedgerSnapshot(world, altar, summon, revision, stage, bosses, killed, costs, absent, refundFlag == 1, context);
                }
            }
            catch (EndOfStreamException error) { throw new InvalidDataException("Truncated summon ledger snapshot.", error); }
        }

        static string[] ReadIds(BinaryReader reader)
        {
            int count = reader.ReadByte(); if (count > 16) throw new InvalidDataException("Too many summoned bosses.");
            var ids = new string[count]; for (int i = 0; i < count; i++) ids[i] = reader.ReadString(); return ids;
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static void RequireText(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0)
                throw new InvalidDataException("Invalid summoned-boss identity.");
        }
    }
}
