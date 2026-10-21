// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: strict big-endian wire forms for the Ghost request, answer and published set. Unity-free.
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Hex.Ghost
{
    /// <summary>A published confirmed set: the registry revision and its sorted, unique keys.</summary>
    public sealed class GhostSet
    {
        public GhostSet(long revision, IReadOnlyList<GhostKey> keys) { Revision = revision; Keys = keys; }
        public long Revision { get; }
        public IReadOnlyList<GhostKey> Keys { get; }
    }

    /// <summary>Encoders throw <see cref="ArgumentException"/> on invalid input; decoders throw <see cref="FormatException"/> on anything malformed.</summary>
    public static class GhostWire
    {
        public const byte Version = 1;
        public const int MaxEntries = GhostRegistry.MaxEntries;
        const int KeySize = 12, SetHeader = 11;

        public static byte[] EncodeRequest(GhostVerb verb, GhostKey key)
        {
            if (verb != GhostVerb.On && verb != GhostVerb.Off) throw new ArgumentException("Unknown verb.", nameof(verb));
            var bytes = new byte[2 + KeySize];
            bytes[0] = Version; bytes[1] = (byte)verb; WriteKey(bytes, 2, key);
            return bytes;
        }

        public static (GhostVerb Verb, GhostKey Key) DecodeRequest(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 2 + KeySize || bytes[0] != Version) throw new FormatException("Malformed Ghost request.");
            if (bytes[1] > (byte)GhostVerb.On) throw new FormatException("Unknown Ghost verb.");
            var key = ReadKey(bytes, 2);
            if (key.IsNone) throw new FormatException("Ghost request without a character.");
            return ((GhostVerb)bytes[1], key);
        }

        public static byte[] EncodeAnswer(GhostAnswer answer) => new[] { Version, (byte)answer.State, (byte)answer.Reason };

        public static GhostAnswer DecodeAnswer(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 3 || bytes[0] != Version) throw new FormatException("Malformed Ghost answer.");
            if (bytes[1] > (byte)GhostState.Refused || bytes[2] > (byte)GhostReason.Vanilla) throw new FormatException("Unknown Ghost answer.");
            return new GhostAnswer((GhostState)bytes[1], (GhostReason)bytes[2]);
        }

        public static byte[] EncodeSet(long revision, IReadOnlyCollection<GhostKey> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (revision < 0) throw new ArgumentException("Negative revision.", nameof(revision));
            var sorted = keys.Distinct().OrderBy(k => k).ToArray();
            if (sorted.Length > MaxEntries) throw new ArgumentException("Too many Ghost entries.", nameof(keys));
            if (sorted.Any(k => k.IsNone)) throw new ArgumentException("Ghost set with an empty character.", nameof(keys));
            var bytes = new byte[SetHeader + sorted.Length * KeySize];
            bytes[0] = Version;
            WriteLong(bytes, 1, revision);
            bytes[9] = (byte)(sorted.Length >> 8); bytes[10] = (byte)sorted.Length;
            for (var i = 0; i < sorted.Length; i++) WriteKey(bytes, SetHeader + i * KeySize, sorted[i]);
            return bytes;
        }

        public static GhostSet DecodeSet(byte[] bytes)
        {
            if (bytes == null || bytes.Length < SetHeader || bytes[0] != Version) throw new FormatException("Malformed Ghost set.");
            var revision = ReadLong(bytes, 1);
            if (revision < 0) throw new FormatException("Negative Ghost revision.");
            var count = bytes[9] << 8 | bytes[10];
            if (count > MaxEntries || bytes.Length != SetHeader + count * KeySize) throw new FormatException("Ghost set length mismatch.");
            var keys = new GhostKey[count];
            for (var i = 0; i < count; i++)
            {
                keys[i] = ReadKey(bytes, SetHeader + i * KeySize);
                if (keys[i].IsNone) throw new FormatException("Ghost set with an empty character.");
                if (i > 0 && keys[i - 1].CompareTo(keys[i]) >= 0) throw new FormatException("Ghost set is not strictly ordered.");
            }
            return new GhostSet(revision, keys);
        }

        public static byte[] EncodeAck() => new[] { Version, (byte)1 };

        public static bool DecodeAck(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 2 || bytes[0] != Version || bytes[1] != 1) throw new FormatException("Malformed Ghost acknowledgment.");
            return true;
        }

        static void WriteKey(byte[] bytes, int at, GhostKey key)
        {
            WriteLong(bytes, at, key.User);
            for (var i = 0; i < 4; i++) bytes[at + 8 + i] = (byte)(key.Id >> (24 - 8 * i));
        }

        static GhostKey ReadKey(byte[] bytes, int at)
        {
            uint id = 0;
            for (var i = 0; i < 4; i++) id = id << 8 | bytes[at + 8 + i];
            return new GhostKey(ReadLong(bytes, at), id);
        }

        static void WriteLong(byte[] bytes, int at, long value) { for (var i = 0; i < 8; i++) bytes[at + i] = (byte)(value >> (56 - 8 * i)); }

        static long ReadLong(byte[] bytes, int at) { long value = 0; for (var i = 0; i < 8; i++) value = value << 8 | bytes[at + i]; return value; }
    }
}
