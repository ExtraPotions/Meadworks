// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original bounded transport for native location data; no third-party implementation adopted.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExtraPotions.Thing.WorldAdmin
{
    public static class PoiData
    {
        public const string CapabilityId = "meadworks.thing.poi";
        public const string OperationId = "meadworks.thing.poi_locations";
        public static readonly Version Version = new Version(1, 0);
        public const int MaximumLocations = 512, MaximumBytes = 16384;
        static readonly HashSet<string> names = new HashSet<string>(StringComparer.Ordinal)
        {
            "Eikthyrnir", "GDKing", "Bonemass", "Dragonqueen", "GoblinKing",
            "Mistlands_DvergrBossEntrance1", "FaderLocation", "DN_Bossroom", "Vendor_BlackForest", "Hildir_camp", "BogWitch_Camp"
        };
        public static bool Known(string name) => name != null && names.Contains(name);
        public static bool Trader(string name) => name == "Vendor_BlackForest" || name == "Hildir_camp" || name == "BogWitch_Camp";
        static bool Coordinate(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= 20000;
        static bool Valid(Tuple<string, float, float, float, bool> row) => row != null && Known(row.Item1)
            && Coordinate(row.Item2) && Coordinate(row.Item3) && Coordinate(row.Item4);

        /// <summary>Retain native boss positions; suppress a trader's candidates when its placed location is known.</summary>
        public static IReadOnlyList<Tuple<string, float, float, float, bool>> Select(IEnumerable<Tuple<string, float, float, float, bool>> source)
        {
            var rows = source.Where(Valid).ToArray();
            var confirmed = new HashSet<string>(rows.Where(r => Trader(r.Item1) && r.Item5).Select(r => r.Item1), StringComparer.Ordinal);
            var selected = rows.Where(r => !Trader(r.Item1) || r.Item5 || !confirmed.Contains(r.Item1))
                .Distinct().OrderBy(r => r.Item1, StringComparer.Ordinal).ThenBy(r => r.Item2).ThenBy(r => r.Item4).ToArray();
            // Known prefab identities are ASCII and under 128 bytes: BinaryWriter uses one length byte.
            if (selected.Length > MaximumLocations || 5 + selected.Sum(r => 14 + Encoding.UTF8.GetByteCount(r.Item1)) > MaximumBytes)
                throw new InvalidOperationException("PoiLimit");
            return Array.AsReadOnly(selected);
        }

        public static byte[] Encode(IReadOnlyList<Tuple<string, float, float, float, bool>> rows)
        {
            if (rows == null || rows.Count > MaximumLocations || rows.Any(r => !Valid(r))) throw new ArgumentException("Invalid POI rows.");
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write((byte)1); writer.Write(rows.Count);
            foreach (var row in rows)
            { writer.Write(row.Item1); writer.Write(row.Item2); writer.Write(row.Item3); writer.Write(row.Item4); writer.Write(row.Item5); }
            if (stream.Length > MaximumBytes) throw new ArgumentException("POI reply too large.");
            return stream.ToArray();
        }

        public static IReadOnlyList<Tuple<string, float, float, float, bool>> Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 5 || bytes.Length > MaximumBytes) throw new FormatException("Invalid POI payload length.");
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream, new UTF8Encoding(false, true));
            if (reader.ReadByte() != 1) throw new FormatException("Unsupported POI payload.");
            var count = reader.ReadInt32();
            if (count < 0 || count > MaximumLocations) throw new FormatException("Invalid POI count.");
            var rows = new List<Tuple<string, float, float, float, bool>>(count);
            for (var i = 0; i < count; i++)
            {
                var name = reader.ReadString();
                var x = reader.ReadSingle(); var y = reader.ReadSingle(); var z = reader.ReadSingle();
                var placed = reader.ReadByte();
                var row = Tuple.Create(name, x, y, z, placed == 1);
                if (placed > 1 || !Valid(row)) throw new FormatException("Invalid POI row.");
                rows.Add(row);
            }
            if (stream.Position != stream.Length) throw new FormatException("Trailing POI payload.");
            return rows.AsReadOnly();
        }
    }
}
