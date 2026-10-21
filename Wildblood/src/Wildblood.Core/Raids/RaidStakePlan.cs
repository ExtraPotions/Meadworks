// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExtraPotions.Wildblood.Transfers;

namespace ExtraPotions.Wildblood.Raids
{
    /// <summary>Immutable admission metadata. Encoding is not authority or proof of a native debit.</summary>
    public sealed class RaidStakePlan
    {
        public const int MaxImageBytes = 2080768, MaxMetadataBytes = 16384, MaxStake = int.MaxValue / 2;
        public long WorldUid { get; }
        public string ChestToken { get; }
        public string EventRoot { get; }
        public string RequestId { get; }
        public long AskerCharacterId { get; }
        public string EventName { get; }
        public string DisplayName { get; }
        public int Stake { get; }
        public float Heat { get; }
        public byte Band { get; }
        public byte Tier { get; }
        public int CoinBudget { get; }
        public float Drops { get; }
        public long StartedAtMs { get; }
        public long DeadlineMs { get; }
        public long CountdownUntilMs { get; }
        public long SeenAtMs { get; }
        public long CooldownUntilMs { get; }
        public ushort PresencePlayers { get; }
        public byte PresenceStrongestTier { get; }
        public bool LocalNear { get; }
        public long InitialRunnerPeer { get; }
        public long InitialServerSession { get; }
        public bool Alone => PresencePlayers == 0 || PresencePlayers == 1 && LocalNear;
        public string ChestKey => "raid-stake:" + WorldUid.ToString(CultureInfo.InvariantCulture) + ":" + ChestToken;
        public string Source => ChestKey + ":" + EventRoot;

        /// <summary>
        /// The metadata format: 1 for plans whose tiers stay within 0-6 (byte-identical to the format before the Deep North
        /// tier, so recorded fingerprints keep verifying), 2 when either tier is 7. A build that predates tier 7 refuses
        /// format 2 as unknown and keeps the record for recovery instead of misreading it.
        /// </summary>
        public ushort FormatVersion => (ushort)(Tier > RaidTiers.BeforeDeepNorth || PresenceStrongestTier > RaidTiers.BeforeDeepNorth ? 2 : 1);

        public RaidStakePlan(long worldUid, string chestToken, string eventRoot, string requestId, long askerCharacterId,
            string eventName, string displayName, int stake, float heat, byte band, byte tier, int coinBudget, float drops,
            long startedAtMs, long deadlineMs, long countdownUntilMs, long seenAtMs, long cooldownUntilMs,
            ushort presencePlayers, byte presenceStrongestTier, bool localNear, long initialRunnerPeer, long initialServerSession)
        {
            StakeWire.Require(worldUid != 0 && askerCharacterId != 0, "Missing world/asker identity.");
            StakeWire.Id(chestToken); StakeWire.Id(eventRoot); StakeWire.Id(requestId);
            StakeWire.Text(eventName, 256); StakeWire.Text(displayName, 512);
            StakeWire.Require(stake >= 1 && stake <= MaxStake && StakeWire.Finite(heat) && heat > 0 && heat <= MaxStake / 100f,
                "Stake/heat outside codec bounds.");
            StakeWire.Require(band <= 4 && tier <= RaidTiers.Top && coinBudget >= 0 && StakeWire.Finite(drops) && drops >= .1f && drops <= 5f,
                "Raid table fields outside codec bounds.");
            StakeWire.Require(startedAtMs >= 0 && startedAtMs <= long.MaxValue - 900000
                && deadlineMs == startedAtMs + 900000 && countdownUntilMs == startedAtMs + 20000
                && seenAtMs == startedAtMs && cooldownUntilMs >= startedAtMs, "Invalid initial raid clocks.");
            StakeWire.Require(presencePlayers <= 1024 && presenceStrongestTier <= RaidTiers.Top && (presencePlayers != 0 || !localNear)
                && initialRunnerPeer != 0 && initialServerSession != 0, "Invalid initial presence.");
            WorldUid = worldUid; ChestToken = chestToken; EventRoot = eventRoot; RequestId = requestId; AskerCharacterId = askerCharacterId;
            EventName = eventName; DisplayName = displayName; Stake = stake; Heat = heat; Band = band; Tier = tier; CoinBudget = coinBudget; Drops = drops;
            StartedAtMs = startedAtMs; DeadlineMs = deadlineMs; CountdownUntilMs = countdownUntilMs; SeenAtMs = seenAtMs; CooldownUntilMs = cooldownUntilMs;
            PresencePlayers = presencePlayers; PresenceStrongestTier = presenceStrongestTier; LocalNear = localNear;
            InitialRunnerPeer = initialRunnerPeer; InitialServerSession = initialServerSession;
        }

        public byte[] Encode()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, StakeWire.Utf8, true))
            {
                writer.Write(0x5352574Du); writer.Write(FormatVersion); writer.Write(WorldUid);
                writer.Write(StakeWire.Hex(ChestToken, 16, true)); writer.Write(StakeWire.Hex(EventRoot, 16, true)); writer.Write(StakeWire.Hex(RequestId, 16, true));
                writer.Write(AskerCharacterId); StakeWire.WriteText(writer, EventName, 256); StakeWire.WriteText(writer, DisplayName, 512);
                writer.Write(Stake); writer.Write(Heat); writer.Write(Band); writer.Write(Tier); writer.Write(CoinBudget); writer.Write(Drops);
                writer.Write(StartedAtMs); writer.Write(DeadlineMs); writer.Write(CountdownUntilMs); writer.Write(SeenAtMs); writer.Write(CooldownUntilMs);
                writer.Write(PresencePlayers); writer.Write(PresenceStrongestTier); writer.Write((byte)(LocalNear ? 1 : 0));
                writer.Write(InitialRunnerPeer); writer.Write(InitialServerSession);
                return stream.ToArray();
            }
        }

        public static RaidStakePlan Decode(byte[] bytes)
        {
            StakeWire.Require(bytes != null && bytes.Length <= MaxMetadataBytes, "Metadata exceeds bounds.");
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, StakeWire.Utf8, true))
                {
                    StakeWire.Require(reader.ReadUInt32() == 0x5352574Du, "Unknown raid metadata format.");
                    ushort format = reader.ReadUInt16();
                    StakeWire.Require(format == 1 || format == 2, "Unknown raid metadata format.");
                    long world = reader.ReadInt64(); string chest = StakeWire.ReadHex(reader, 16), root = StakeWire.ReadHex(reader, 16), request = StakeWire.ReadHex(reader, 16);
                    long asker = reader.ReadInt64(); string name = StakeWire.ReadText(reader, 256), display = StakeWire.ReadText(reader, 512);
                    int stake = reader.ReadInt32(); float heat = reader.ReadSingle(); byte band = reader.ReadByte(), tier = reader.ReadByte(); int budget = reader.ReadInt32(); float drops = reader.ReadSingle();
                    long started = reader.ReadInt64(), deadline = reader.ReadInt64(), countdown = reader.ReadInt64(), seen = reader.ReadInt64(), cooldown = reader.ReadInt64();
                    ushort players = reader.ReadUInt16(); byte strongest = reader.ReadByte(), local = reader.ReadByte();
                    StakeWire.Require(local <= 1, "Invalid presence boolean.");
                    var plan = new RaidStakePlan(world, chest, root, request, asker, name, display, stake, heat, band, tier, budget, drops,
                        started, deadline, countdown, seen, cooldown, players, strongest, local == 1, reader.ReadInt64(), reader.ReadInt64());
                    StakeWire.Require(stream.Position == stream.Length, "Trailing raid metadata bytes.");
                    StakeWire.Require(format == plan.FormatVersion, "Noncanonical raid metadata format.");
                    return plan;
                }
            }
            catch (EndOfStreamException error) { throw new InvalidDataException("Truncated raid metadata.", error); }
        }

        public string Fingerprint(byte[] before, byte[] after)
        {
            Images(before, after);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, StakeWire.Utf8, true))
            using (var sha = SHA256.Create())
            {
                foreach (var bytes in new[] { Encode(), before, after }) { writer.Write(bytes.Length); writer.Write(bytes); }
                writer.Flush(); return StakeWire.ToHex(sha.ComputeHash(stream.ToArray()));
            }
        }

        static void Images(byte[] before, byte[] after)
        {
            StakeWire.Require(before != null && after != null && before.Length >= 1 && before.Length <= MaxImageBytes
                && after.Length >= 1 && after.Length <= MaxImageBytes && !before.SequenceEqual(after), "Invalid raid debit images.");
        }

        public static RaidStakePlan Verify(InventoryDecisionRecord record, long expectedWorld, string expectedChest,
            string expectedRoot, string expectedRequest, string expectedHash)
        {
            StakeWire.Require(record != null, "Missing raid journal record."); StakeWire.Hex(expectedHash, 32, false);
            var plan = Decode(record.Metadata);
            StakeWire.Require(plan.WorldUid == expectedWorld && plan.ChestToken == expectedChest && plan.EventRoot == expectedRoot
                && plan.RequestId == expectedRequest && record.Id == expectedRoot && record.Source == plan.Source,
                "Raid journal binding mismatch.");
            string computed = plan.Fingerprint(record.Before, record.After);
            StakeWire.Require(record.Fingerprint == computed && expectedHash == computed, "Raid journal fingerprint mismatch.");
            return plan;
        }
    }

    internal static class StakeWire
    {
        internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal static void Require(bool condition, string error) { if (!condition) throw new InvalidDataException(error); }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static void Id(string value) => Hex(value, 16, true);
        internal static byte[] Hex(string value, int length, bool nonzero)
        {
            Require(value != null && value.Length == length * 2 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f')
                && (!nonzero || value.Any(c => c != '0')), "Noncanonical raid identity/hash.");
            var bytes = new byte[length]; for (int i = 0; i < length; i++) bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16); return bytes;
        }
        internal static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        internal static string ReadHex(BinaryReader reader, int length)
        {
            var bytes = reader.ReadBytes(length); Require(bytes.Length == length, "Truncated raid identity/hash."); return ToHex(bytes);
        }
        internal static byte[] Text(string value, int maximum)
        {
            Require(!string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl) && value.IndexOf('\uFEFF') < 0, "Invalid raid text.");
            try { var bytes = Utf8.GetBytes(value); Require(bytes.Length <= maximum, "Raid text exceeds byte limit."); return bytes; }
            catch (EncoderFallbackException error) { throw new InvalidDataException("Invalid raid UTF-8 text.", error); }
        }
        internal static void WriteText(BinaryWriter writer, string value, int maximum)
        { var bytes = Text(value, maximum); writer.Write(bytes.Length); writer.Write(bytes); }
        internal static string ReadText(BinaryReader reader, int maximum)
        {
            int length = reader.ReadInt32(); Require(length >= 1 && length <= maximum, "Raid text length exceeds bounds.");
            var bytes = reader.ReadBytes(length); Require(bytes.Length == length, "Truncated raid text.");
            try { string value = Utf8.GetString(bytes); Text(value, maximum); return value; }
            catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid raid UTF-8 bytes.", error); }
        }
    }
}
