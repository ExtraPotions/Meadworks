// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExtraPotions.Wildblood.Luck
{
    public sealed class LuckCounterAnchor
    {
        public long Revision { get; }
        public string Image { get; }
        public LuckCounterAnchor(long revision, string image)
        {
            if (revision < 0 || revision == long.MaxValue) throw new InvalidDataException("Invalid counter anchor revision.");
            LuckDecisionCodec.Image(image); Revision = revision; Image = image;
        }
    }

    /// <summary>Local checkpoint payload. Its caller must prove the committed chain and validate the enclosing WAL record.</summary>
    public sealed class LuckRecoveryCheckpoint
    {
        const int Magic = 0x574C4350, Version = 1;
        public const int MaximumBytes = 1024 * 1024, MaximumAnchors = 256;
        public const string RecoveryReason = "RecipientRecovered";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        readonly byte[] grantBytes, resultBytes;
        public long World { get; }
        public long Player { get; }
        public long HeadRevision { get; }
        public string HeadImage { get; }
        public string RecoveryOwnerEpoch { get; }
        public string Reason => RecoveryReason;
        public byte[] GrantBytes => (byte[])grantBytes.Clone();
        public byte[] ResultBytes => (byte[])resultBytes.Clone();
        public IReadOnlyList<LuckCounterAnchor> Anchors { get; }

        LuckRecoveryCheckpoint(long world, long player, long headRevision, string headImage, string recoveryOwnerEpoch,
            byte[] grant, byte[] result, IReadOnlyList<LuckCounterAnchor> anchors)
        {
            if (world == 0 || player == 0 || headRevision <= 0 || headRevision == long.MaxValue) throw new InvalidDataException("Invalid checkpoint identity or revision.");
            LuckDecisionCodec.Image(headImage); LuckDecisionCodec.Text(recoveryOwnerEpoch, 128);
            if (grant == null || result == null || grant.Length > LuckDecisionCodec.MaximumPacketBytes || result.Length > LuckDecisionCodec.MaximumPacketBytes)
                throw new InvalidDataException("Invalid checkpoint receipt envelopes.");
            var g = LuckDecisionCodec.DecodeGrant(grant); var r = LuckDecisionCodec.DecodeResult(result);
            if (!grant.SequenceEqual(LuckDecisionCodec.EncodeGrant(g)) || !result.SequenceEqual(LuckDecisionCodec.EncodeResult(r))
                || g.Request.World != world || g.Request.Player != player || r.OwnerEpoch != g.OwnerEpoch || r.Revision != g.Revision
                || r.BaseHash != g.BaseHash || !LuckDecisionCodec.EncodeRequest(g.Request).SequenceEqual(LuckDecisionCodec.EncodeRequest(r.Request))
                || r.Revision >= headRevision) throw new InvalidDataException("Checkpoint receipt does not match its store or original grant.");
            if (anchors == null || anchors.Count < 1 || anchors.Count > MaximumAnchors) throw new InvalidDataException("Invalid checkpoint anchor count.");
            long previous = -1;
            foreach (var anchor in anchors)
            {
                if (anchor == null || anchor.Revision >= headRevision || previous >= 0 && anchor.Revision != previous + 1)
                    throw new InvalidDataException("Checkpoint anchors are not a contiguous trailing sequence.");
                if (anchor.Revision == g.Revision && anchor.Image != g.BaseImage
                    || anchor.Revision == g.Revision + 1 && anchor.Image != r.FinalImage)
                    throw new InvalidDataException("Checkpoint anchor conflicts with its receipt edge.");
                previous = anchor.Revision;
            }
            if (previous != headRevision - 1 || headRevision == r.Revision + 1 && headImage != r.FinalImage)
                throw new InvalidDataException("Checkpoint head does not match the trailing proof.");
            World = world; Player = player; HeadRevision = headRevision; HeadImage = headImage; RecoveryOwnerEpoch = recoveryOwnerEpoch;
            grantBytes = (byte[])grant.Clone(); resultBytes = (byte[])result.Clone(); Anchors = Array.AsReadOnly(anchors.ToArray());
        }

        /// <summary>Retains only a bounded trailing suffix of caller-proven anchors; it cannot prove native counter transitions itself.</summary>
        public static LuckRecoveryCheckpoint Create(long world, long player, long headRevision, string headImage, string recoveryOwnerEpoch,
            byte[] grantBytes, byte[] resultBytes, IEnumerable<LuckCounterAnchor> provenAnchors, int maximumAnchors = 128)
        {
            if (maximumAnchors < 1 || maximumAnchors > MaximumAnchors || provenAnchors == null) throw new InvalidDataException("Invalid checkpoint proof capacity.");
            var tail = new List<LuckCounterAnchor>(); long previous = -1;
            foreach (var anchor in provenAnchors)
            {
                if (anchor == null || anchor.Revision >= headRevision || previous >= 0 && anchor.Revision != previous + 1)
                    throw new InvalidDataException("Provided counter proof has a revision gap or conflict.");
                previous = anchor.Revision; tail.Add(anchor); if (tail.Count > maximumAnchors) tail.RemoveAt(0);
            }
            while (true)
            {
                var checkpoint = new LuckRecoveryCheckpoint(world, player, headRevision, headImage, recoveryOwnerEpoch, grantBytes, resultBytes, tail);
                if (checkpoint.EncodeCore().Length <= MaximumBytes) return checkpoint;
                if (tail.Count == 1) throw new InvalidDataException("Checkpoint exceeds storage bounds.");
                tail.RemoveAt(0);
            }
        }

        public byte[] Encode()
        {
            byte[] bytes = EncodeCore(); if (bytes.Length > MaximumBytes) throw new InvalidDataException("Checkpoint exceeds storage bounds."); return bytes;
        }
        byte[] EncodeCore()
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
            writer.Write(Magic); writer.Write(Version); Write(writer, Utf8.GetBytes(RecoveryReason));
            writer.Write(World); writer.Write(Player); writer.Write(HeadRevision); Write(writer, Utf8.GetBytes(HeadImage)); Write(writer, Utf8.GetBytes(RecoveryOwnerEpoch));
            Write(writer, grantBytes); Write(writer, resultBytes); writer.Write(Anchors.Count);
            foreach (var anchor in Anchors) { writer.Write(anchor.Revision); Write(writer, Utf8.GetBytes(anchor.Image)); }
            writer.Flush(); return stream.ToArray();
        }
        public static LuckRecoveryCheckpoint Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length > MaximumBytes) throw new InvalidDataException("Invalid checkpoint payload size.");
            try
            {
                using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Utf8, true);
                if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version || Utf8.GetString(Read(reader, 128)) != RecoveryReason)
                    throw new InvalidDataException("Unsupported checkpoint version or terminal reason.");
                long world = reader.ReadInt64(), player = reader.ReadInt64(), head = reader.ReadInt64();
                string image = Utf8.GetString(Read(reader, LuckCodec.PublishBytes)), epoch = Utf8.GetString(Read(reader, 128));
                byte[] grant = Read(reader, LuckDecisionCodec.MaximumPacketBytes), result = Read(reader, LuckDecisionCodec.MaximumPacketBytes);
                int count = reader.ReadInt32(); if (count < 1 || count > MaximumAnchors) throw new InvalidDataException("Invalid checkpoint anchor count.");
                var anchors = new LuckCounterAnchor[count];
                for (int i = 0; i < count; i++) anchors[i] = new LuckCounterAnchor(reader.ReadInt64(), Utf8.GetString(Read(reader, LuckCodec.PublishBytes)));
                if (stream.Position != stream.Length) throw new InvalidDataException("Checkpoint has trailing bytes.");
                return new LuckRecoveryCheckpoint(world, player, head, image, epoch, grant, result, anchors);
            }
            catch (EndOfStreamException ex) { throw new InvalidDataException("Truncated checkpoint payload.", ex); }
            catch (DecoderFallbackException ex) { throw new InvalidDataException("Malformed checkpoint text.", ex); }
        }
        static void Write(BinaryWriter writer, byte[] value) { writer.Write(value.Length); writer.Write(value); }
        static byte[] Read(BinaryReader reader, int maximum)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum || count > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Checkpoint field exceeds bounds.");
            return reader.ReadBytes(count);
        }
    }
}
