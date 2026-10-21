// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtraPotions.Thing.RemoteConfig
{
    public sealed class ConfigListDocument
    {
        public ConfigEntryDocument Element = new ConfigEntryDocument();
        public IReadOnlyList<string> Values = Array.Empty<string>();
        public const int MaxItems = 128, MaxValueBytes = 1024, MaxValuesBytes = 4096;
        public static IReadOnlyList<string> PackValues(IReadOnlyList<string> values) => ConfigWire.Pack(ValueBytes(values));
        static byte[] ValueBytes(IReadOnlyList<string> values)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, ConfigWire.Utf8, true)) WriteValues(writer, values);
                if (stream.Length > MaxValuesBytes) throw new FormatException(); return stream.ToArray();
            }
        }
        static void WriteValues(BinaryWriter writer, IReadOnlyList<string> values)
        {
            if (values.Count > MaxItems) throw new FormatException(); writer.Write(values.Count);
            foreach (var value in values) { if (value == null || ConfigWire.Utf8.GetByteCount(value) > MaxValueBytes) throw new FormatException(); writer.Write(value); }
        }
        static IReadOnlyList<string> ReadValues(BinaryReader reader)
        {
            var count = reader.ReadInt32(); if (count < 0 || count > MaxItems) throw new FormatException();
            var values = new string[count]; for (var i = 0; i < count; i++) { values[i] = reader.ReadString(); if (ConfigWire.Utf8.GetByteCount(values[i]) > MaxValueBytes) throw new FormatException(); }
            if (ValueBytes(values).Length > MaxValuesBytes) throw new FormatException(); return values;
        }
        public static IReadOnlyList<string> UnpackValues(IReadOnlyList<string> chunks)
        {
            using (var stream = new MemoryStream(ConfigWire.UnpackBytes(chunks, MaxValuesBytes)))
            using (var reader = new BinaryReader(stream, ConfigWire.Utf8))
            { var values = ReadValues(reader); if (stream.Position != stream.Length) throw new FormatException(); return values; }
        }
        public IReadOnlyList<string> Encode()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, ConfigWire.Utf8, true))
                {
                    writer.Write((byte)1); var metadata = Element.Encode(); writer.Write(metadata.Count); foreach (var part in metadata) writer.Write(part);
                    WriteValues(writer, Values);
                }
                if (stream.Length > ConfigWire.MaxDocumentBytes) throw new FormatException();
                return new[] { "THING_CONFIG_LIST" }.Concat(ConfigWire.Pack(stream.ToArray())).ToArray();
            }
        }
        public static bool TryDecode(IReadOnlyList<string> lines, out ConfigListDocument document)
        {
            document = null!;
            if (lines.Count < 2 || lines.Count > 16 || lines[0] != "THING_CONFIG_LIST") return false;
            try
            {
                using (var stream = new MemoryStream(ConfigWire.UnpackBytes(lines.Skip(1).ToArray(), ConfigWire.MaxDocumentBytes)))
                using (var reader = new BinaryReader(stream, ConfigWire.Utf8))
                {
                    if (reader.ReadByte() != 1) return false; var count = reader.ReadInt32(); if (count < 2 || count > 16) return false;
                    var metadata = new string[count]; for (var i = 0; i < count; i++) metadata[i] = reader.ReadString();
                    if (!ConfigEntryDocument.TryDecode(metadata, out var element)) return false;
                    var values = ReadValues(reader); if (stream.Position != stream.Length) return false;
                    document = new ConfigListDocument { Element = element, Values = values }; return true;
                }
            }
            catch { return false; }
        }
    }
}
