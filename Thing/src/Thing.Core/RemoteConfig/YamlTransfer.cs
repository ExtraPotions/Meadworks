// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Thing.RemoteConfig
{
    // Each frame fits the existing sixteen-line admin reply; offsets address bytes, including split UTF-8 characters.
    public static class YamlTransfer
    {
        public const int MaxBytes = 1024 * 1024;
        public const int ChunkBytes = 6144;
        public static IReadOnlyList<string> Pack(byte[] bytes) => ConfigWire.Pack(bytes);
        public static byte[] Unpack(IReadOnlyList<string> parts) => ConfigWire.UnpackBytes(parts, ChunkBytes);
        public static string Text(byte[] bytes) => ConfigWire.Utf8.GetString(bytes);
        public static byte[] Bytes(string text) => ConfigWire.Utf8.GetBytes(text);
        public static IReadOnlyList<string> Header(string token, string version, int total) => new[] { "THING_YAML\t" + token + "\t" + version + "\t" + total.ToString(CultureInfo.InvariantCulture) };
        public static bool TryHeader(IReadOnlyList<string> lines, out string token, out string version, out int total)
        {
            token = version = ""; total = 0;
            if (lines.Count != 1) return false;
            var parts = lines[0].Split('\t');
            if (parts.Length != 4 || parts[0] != "THING_YAML" || !Communication.MessageEditing.Version(parts[1]) || !Communication.MessageEditing.Version(parts[2])
                || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out total) || total < 0 || total > MaxBytes) return false;
            token = parts[1]; version = parts[2]; return true;
        }
        public static IReadOnlyList<string> Chunk(string token, int offset, byte[] bytes) => new[] { "THING_YAML_CHUNK\t" + token + "\t" + offset.ToString(CultureInfo.InvariantCulture) }.Concat(Pack(bytes)).ToArray();
        public static bool TryChunk(IReadOnlyList<string> lines, string token, int offset, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (lines.Count < 2 || lines.Count > 16 || lines[0] != "THING_YAML_CHUNK\t" + token + "\t" + offset.ToString(CultureInfo.InvariantCulture)) return false;
            try { bytes = Unpack(lines.Skip(1).ToArray()); return true; } catch { return false; }
        }
    }
    public sealed class YamlUpload
    {
        byte[]? bytes;
        readonly string hash;
        public int Received { get; private set; }
        public int Length { get; }
        public YamlUpload(int length, string hash)
        {
            if (length < 0 || length > YamlTransfer.MaxBytes || !Communication.MessageEditing.Version(hash)) throw new ArgumentException();
            bytes = new byte[length]; Length = length; this.hash = hash;
        }
        public bool Append(int offset, byte[] chunk)
        {
            if (bytes == null || chunk == null || chunk.Length == 0 || chunk.Length > YamlTransfer.ChunkBytes || offset != Received || chunk.Length > Length - Received) return false;
            Buffer.BlockCopy(chunk, 0, bytes, Received, chunk.Length); Received += chunk.Length; return true;
        }
        public bool Take(out string text)
        {
            text = ""; var completed = bytes; bytes = null;
            if (completed == null || Received != Length || !FileVersion.Matches(hash, completed)) return false;
            try { text = YamlTransfer.Text(completed); return true; } catch { return false; }
        }
    }
}
