// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtraPotions.Wildblood.Luck
{
    public enum LuckDecisionMessageKind : byte { Request = 1, Status = 2, Grant = 3, Result = 4, Acknowledge = 5, Applied = 6, Busy = 7, Refused = 8, Acknowledged = 9, Stale = 10 }
    public static class LuckDecisionCodec
    {
        public const int MaximumPacketBytes = 8192;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static LuckDecisionMessageKind PeekKind(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 2 || bytes.Length > MaximumPacketBytes || bytes[0] != 1 || !Enum.IsDefined(typeof(LuckDecisionMessageKind), bytes[1]))
                throw new InvalidDataException("Unsupported luck decision packet.");
            return (LuckDecisionMessageKind)bytes[1];
        }
        public static byte[] EncodeRequest(LuckDecisionRequest request, LuckDecisionMessageKind kind = LuckDecisionMessageKind.Request)
        {
            RequestKind(kind);
            return Encode(kind, writer => WriteRequest(writer, request));
        }
        public static LuckDecisionRequest DecodeRequest(byte[] bytes) => Decode(bytes, reader => { RequestKind((LuckDecisionMessageKind)bytes[1]); return ReadRequest(reader); });
        public static byte[] EncodeGrant(LuckDecisionGrant grant, LuckDecisionMessageKind kind = LuckDecisionMessageKind.Grant)
        {
            GrantKind(kind);
            return Encode(kind, writer => { WriteRequest(writer, grant.Request); WriteText(writer, grant.OwnerEpoch); writer.Write(grant.Revision); WriteText(writer, grant.BaseImage); });
        }
        public static LuckDecisionGrant DecodeGrant(byte[] bytes) => Decode(bytes, reader =>
        { GrantKind((LuckDecisionMessageKind)bytes[1]); return new LuckDecisionGrant(ReadRequest(reader), ReadText(reader, 128), reader.ReadInt64(), ReadText(reader, LuckCodec.PublishBytes)); });
        public static byte[] EncodeResult(LuckDecisionResult result) => Encode(LuckDecisionMessageKind.Result, writer =>
        {
            WriteRequest(writer, result.Request); WriteText(writer, result.OwnerEpoch); writer.Write(result.Revision); WriteText(writer, result.BaseHash);
            writer.Write(result.Decisions.Count); foreach (var row in result.Decisions) WriteText(writer, row); WriteText(writer, result.FinalImage);
        });
        public static LuckDecisionResult DecodeResult(byte[] bytes) => Decode(bytes, reader =>
        {
            if ((LuckDecisionMessageKind)bytes[1] != LuckDecisionMessageKind.Result) throw new InvalidDataException("Expected luck result.");
            var request = ReadRequest(reader); string epoch = ReadText(reader, 128); long revision = reader.ReadInt64(); string hash = ReadText(reader, 64);
            int count = reader.ReadInt32(); if (count < 0 || count > 128) throw new InvalidDataException("Too many luck decisions.");
            var rows = new string[count]; for (int i = 0; i < count; i++) rows[i] = ReadText(reader, 256);
            return new LuckDecisionResult(request, epoch, revision, hash, rows, ReadText(reader, LuckCodec.PublishBytes));
        });
        static void RequestKind(LuckDecisionMessageKind kind)
        { if (kind != LuckDecisionMessageKind.Request && kind != LuckDecisionMessageKind.Status && kind != LuckDecisionMessageKind.Busy && kind != LuckDecisionMessageKind.Refused && kind != LuckDecisionMessageKind.Stale) throw new InvalidDataException("Expected luck request envelope."); }
        static void GrantKind(LuckDecisionMessageKind kind)
        { if (kind != LuckDecisionMessageKind.Grant && kind != LuckDecisionMessageKind.Acknowledge && kind != LuckDecisionMessageKind.Applied && kind != LuckDecisionMessageKind.Acknowledged) throw new InvalidDataException("Expected luck grant envelope."); }
        static byte[] Encode(LuckDecisionMessageKind kind, Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, true);
            writer.Write((byte)1); writer.Write((byte)kind); write(writer); writer.Flush();
            if (stream.Length > MaximumPacketBytes) throw new InvalidDataException("Luck decision packet exceeds bounds.");
            return stream.ToArray();
        }
        static T Decode<T>(byte[] bytes, Func<BinaryReader, T> read)
        {
            PeekKind(bytes);
            try
            {
                using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Utf8, true); stream.Position = 2;
                var result = read(reader); if (stream.Position != stream.Length) throw new InvalidDataException("Luck decision packet has trailing data."); return result;
            }
            catch (EndOfStreamException error) { throw new InvalidDataException("Truncated luck decision packet.", error); }
            catch (DecoderFallbackException error) { throw new InvalidDataException("Malformed luck decision text.", error); }
        }
        static void WriteRequest(BinaryWriter writer, LuckDecisionRequest request)
        {
            WriteText(writer, request.Id); writer.Write(request.World); WriteText(writer, request.RequesterEpoch); WriteText(writer, request.Source); writer.Write(request.SourceOwner);
            WriteText(writer, request.Attacker); writer.Write(request.Player); WriteText(writer, request.Recipient); writer.Write(request.RecipientOwner); writer.Write(request.Level); WriteText(writer, request.Fingerprint);
            WriteText(writer, request.ExpectedOwnerEpoch); writer.Write(request.ExpectedRevision);
        }
        static LuckDecisionRequest ReadRequest(BinaryReader reader) => new LuckDecisionRequest(ReadText(reader, 128), reader.ReadInt64(), ReadText(reader, 128), ReadText(reader, 128), reader.ReadInt64(),
            ReadText(reader, 128), reader.ReadInt64(), ReadText(reader, 128), reader.ReadInt64(), reader.ReadInt32(), ReadText(reader, 64), ReadText(reader, 128), reader.ReadInt64());
        static void WriteText(BinaryWriter writer, string text) { byte[] bytes = Utf8.GetBytes(text); writer.Write(bytes.Length); writer.Write(bytes); }
        static string ReadText(BinaryReader reader, int maximum)
        {
            int count = reader.ReadInt32(); if (count < 0 || count > maximum || count > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Luck text exceeds bounds.");
            return Utf8.GetString(reader.ReadBytes(count));
        }
        internal static void Text(string value, int maximum)
        { if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 || Utf8.GetByteCount(value) > maximum) throw new InvalidDataException("Invalid luck decision text."); }
        internal static void Digest(string value)
        { if (value == null || value.Length != 64 || value.Any(c => !(c >= '0' && c <= '9' || c >= 'A' && c <= 'F' || c >= 'a' && c <= 'f'))) throw new InvalidDataException("Invalid luck decision digest."); }
        internal static void Image(string value)
        { if (value == null || Utf8.GetByteCount(value) > LuckCodec.PublishBytes || !LuckCodec.CanPreserveFormat(value)) throw new InvalidDataException("Invalid luck counter image."); }
        internal static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    }
}
