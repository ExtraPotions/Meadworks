// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/Regrow.cs Load and Save (the worlds_local/<world>.restockregen.txt format) and src/DungeonRegrow.cs (the spot package
// stored on a dungeon generator's ZDO)). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: parse and write are pure text and byte functions returning their errors instead of logging;
// the package is read with a BinaryReader in the ZPackage layout (little-endian int, three floats per vector), so the
// converter can carry RestockRegen's records over without the game.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Regrow
{
    /// <summary>A spot as RestockRegen records it: the whole prefab's stable hash, position and rotation.</summary>
    public readonly struct RecordSpot
    {
        public RecordSpot(int prefab, Vec3 position, Vec3 rotation)
        {
            Prefab = prefab;
            Position = position;
            Rotation = rotation;
        }

        public int Prefab { get; }
        public Vec3 Position { get; }
        public Vec3 Rotation { get; }
    }

    /// <summary>
    /// The world-object record file: <c>S &lt;prefab&gt; x y z rx ry rz</c> per spot and <c>V &lt;zone x&gt; &lt;zone z&gt; &lt;day&gt;</c>
    /// per zone last visited, invariant culture, round-trip floats, a comment header.
    /// </summary>
    public sealed class RecordFile
    {
        private RecordFile(IReadOnlyList<RecordSpot> spots, IReadOnlyDictionary<(int, int), int> visits, IReadOnlyList<string> errors)
        {
            Spots = spots;
            Visits = visits;
            Errors = errors;
        }

        public IReadOnlyList<RecordSpot> Spots { get; }
        public IReadOnlyDictionary<(int, int), int> Visits { get; }
        public IReadOnlyList<string> Errors { get; }

        public static RecordFile Parse(string? text)
        {
            var inv = CultureInfo.InvariantCulture;
            var spots = new List<RecordSpot>();
            var visits = new Dictionary<(int, int), int>();
            var errors = new List<string>();
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var f = line.Split(' ');
                try
                {
                    if (f[0] == "S" && f.Length == 8)
                        spots.Add(new RecordSpot(int.Parse(f[1], inv),
                            new Vec3(float.Parse(f[2], inv), float.Parse(f[3], inv), float.Parse(f[4], inv)),
                            new Vec3(float.Parse(f[5], inv), float.Parse(f[6], inv), float.Parse(f[7], inv))));
                    else if (f[0] == "V" && f.Length == 4)
                        visits[(int.Parse(f[1], inv), int.Parse(f[2], inv))] = int.Parse(f[3], inv);
                    else
                        errors.Add("line " + (i + 1) + ": skipped: " + line);
                }
                catch (FormatException)
                {
                    errors.Add("line " + (i + 1) + ": skipped: " + line);
                }
                catch (OverflowException)
                {
                    errors.Add("line " + (i + 1) + ": skipped: " + line);
                }
            }
            return new RecordFile(spots, visits, errors);
        }

        public static string Write(IEnumerable<RecordSpot> spots, IReadOnlyDictionary<(int, int), int> visits)
        {
            var inv = CultureInfo.InvariantCulture;
            var text = new StringBuilder("# Delve regrow records. S = spot (prefab x y z rx ry rz), V = zone last visited (zone x, zone y, day).\n");
            foreach (var s in spots)
                text.Append(string.Format(inv, "S {0} {1:R} {2:R} {3:R} {4:R} {5:R} {6:R}\n", s.Prefab, s.Position.X, s.Position.Y, s.Position.Z, s.Rotation.X, s.Rotation.Y, s.Rotation.Z));
            foreach (var v in visits.OrderBy(v => v.Key.Item1).ThenBy(v => v.Key.Item2))
                text.Append(string.Format(inv, "V {0} {1} {2}\n", v.Key.Item1, v.Key.Item2, v.Value));
            return text.ToString();
        }
    }

    /// <summary>The spot package on a dungeon generator's ZDO: int version 1, int count, then per spot int prefab, position, rotation.</summary>
    public static class DungeonSpots
    {
        public static byte[] Encode(IReadOnlyCollection<RecordSpot> spots)
        {
            if (spots == null) throw new ArgumentNullException(nameof(spots));
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(1);
                w.Write(spots.Count);
                foreach (var s in spots)
                {
                    w.Write(s.Prefab);
                    Write(w, s.Position);
                    Write(w, s.Rotation);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>The spots, or none for missing data, another version or a truncated package.</summary>
        public static IReadOnlyList<RecordSpot> Decode(byte[]? data)
        {
            var spots = new List<RecordSpot>();
            if (data == null || data.Length < 8) return spots;
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (r.ReadInt32() != 1) return spots;
                int n = r.ReadInt32();
                if (n < 0 || data.Length < 8 + (long)n * 28) return new List<RecordSpot>();
                for (int i = 0; i < n; i++)
                    spots.Add(new RecordSpot(r.ReadInt32(), Read(r), Read(r)));
            }
            return spots;
        }

        private static void Write(BinaryWriter w, Vec3 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
            w.Write(v.Z);
        }

        private static Vec3 Read(BinaryReader r) => new Vec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
