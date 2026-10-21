// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Delve's world sidecar, worlds_local/<world>.delve.json (design: world state in delve.* ZDO keys plus
// this file). World objects have no object to keep their remembered spots on, so the spots, each zone's last visit and
// the one-shot regenerate-now kinds live here. The game ignores the file; a copy of worlds_local includes it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Delve.Data;
using ExtraPotions.Delve.Regrow;

namespace ExtraPotions.Delve.Engine
{
    public readonly struct ImportCount
    {
        public ImportCount(int spots, int visits)
        {
            Spots = spots;
            Visits = visits;
        }

        public int Spots { get; }
        public int Visits { get; }
    }

    public sealed class Sidecar
    {
        public const int Version = 1;

        /// <summary>World object spots (whole prefab hash, position, rotation).</summary>
        public List<RecordSpot> Spots { get; } = new List<RecordSpot>();

        /// <summary>Each 64 m zone with a spot in it, and when a player last visited it.</summary>
        public Dictionary<(int X, int Z), Stamp> Visits { get; } = new Dictionary<(int, int), Stamp>();

        /// <summary>Kinds an admin asked to regenerate now, kept until their next sweep runs.</summary>
        public HashSet<DelveKind> RegenerateNow { get; } = new HashSet<DelveKind>();

        /// <summary>RestockRegen's record file was already carried over (converter, copy only when absent).</summary>
        public bool ImportedRestockRegen { get; set; }

        public string Write()
        {
            var root = new Dictionary<string, object?>
            {
                ["version"] = Version,
                ["importedRestockRegen"] = ImportedRestockRegen,
                ["regenerateNow"] = RegenerateNow.OrderBy(k => k).Select(k => (object?)k.ToString()).ToList(),
                ["visits"] = Visits.OrderBy(v => v.Key.X).ThenBy(v => v.Key.Z)
                    .Select(v => (object?)new List<object?> { v.Key.X, v.Key.Z, StampValue(v.Value) }).ToList(),
                ["spots"] = Spots.Select(s => (object?)new List<object?>
                    { s.Prefab, s.Position.X, s.Position.Y, s.Position.Z, s.Rotation.X, s.Rotation.Y, s.Rotation.Z }).ToList(),
            };
            return MiniJson.Write(root);
        }

        /// <summary>The state in <paramref name="text"/>; empty for a missing file, and empty with a reason for one that cannot be read.</summary>
        public static Sidecar Parse(string? text, out List<string> errors)
        {
            errors = new List<string>();
            var car = new Sidecar();
            if (string.IsNullOrWhiteSpace(text)) return car;
            try
            {
                if (!(MiniJson.Parse(text!) is IDictionary<string, object?> root)) throw new FormatException("JSON root is not an object.");
                var version = root.TryGetValue("version", out var v) && v is double d ? (int)d : 0;
                if (version != Version)
                {
                    errors.Add("sidecar version " + version + " is not " + Version + "; starting empty and leaving the file alone");
                    return new Sidecar { readOnly = true };
                }
                car.ImportedRestockRegen = root.TryGetValue("importedRestockRegen", out var imported) && imported is bool b && b;
                foreach (var item in List(root, "regenerateNow"))
                    if (item is string name && Enum.TryParse<DelveKind>(name, out var kind)) car.RegenerateNow.Add(kind);
                foreach (var item in List(root, "visits"))
                {
                    if (item is IList<object?> f && f.Count == 3 && f[0] is double x && f[1] is double z && Read(f[2]) is Stamp s)
                        car.Visits[((int)x, (int)z)] = s;
                    else errors.Add("skipped a visit entry");
                }
                foreach (var item in List(root, "spots"))
                {
                    if (item is IList<object?> f && f.Count == 7 && f.All(n => n is double))
                    {
                        var n = f.Cast<double>().ToArray();
                        car.Spots.Add(new RecordSpot((int)n[0], new Vec3((float)n[1], (float)n[2], (float)n[3]), new Vec3((float)n[4], (float)n[5], (float)n[6])));
                    }
                    else errors.Add("skipped a spot entry");
                }
            }
            catch (FormatException e)
            {
                errors.Add("sidecar unreadable (" + e.Message + "); starting empty and leaving the file alone");
                return new Sidecar { readOnly = true };
            }
            return car;
        }

        private bool readOnly;

        /// <summary>The file on disk could not be read: never overwrite it (an admin can inspect or remove it).</summary>
        public bool ReadOnly => readOnly;

        /// <summary>
        /// Adds RestockRegen's spots and zone visits that Delve does not have yet; Delve's own records win. A spot within
        /// 20 cm of a known spot of the same prefab (6 m flat for drifting flametal) is the same spot.
        /// </summary>
        public ImportCount ImportRestockRegen(RecordFile restockRegen)
        {
            if (restockRegen == null) throw new ArgumentNullException(nameof(restockRegen));
            int spots = 0, visits = 0;
            foreach (var spot in restockRegen.Spots)
            {
                if (Spots.Any(s => Same(s, spot))) continue;
                Spots.Add(spot);
                spots++;
            }
            foreach (var visit in restockRegen.Visits)
            {
                if (Visits.ContainsKey(visit.Key)) continue;
                Visits[visit.Key] = Stamp.DayOnly(visit.Value);
                visits++;
            }
            ImportedRestockRegen = true;
            return new ImportCount(spots, visits);
        }

        private static readonly Lazy<HashSet<int>> drifting = new Lazy<HashSet<int>>(() => new HashSet<int>(
            KindCatalog.All.Where(k => k.Drifts).SelectMany(k => k.Whole).Select(StableHash.Of)));

        private static bool Same(RecordSpot a, RecordSpot b)
        {
            if (a.Prefab != b.Prefab) return false;
            return drifting.Value.Contains(a.Prefab)
                ? Vec3.Flat(a.Position, b.Position) < SpotIndex.DriftRadius
                : Vec3.SqrDistance(a.Position, b.Position) < 0.04f;
        }

        private static IEnumerable<object?> List(IDictionary<string, object?> root, string key) =>
            root.TryGetValue(key, out var value) && value is IList<object?> list ? list : Enumerable.Empty<object?>();

        private static object StampValue(Stamp s)
        {
            var map = new Dictionary<string, object?>();
            if (s.HasDay) map["day"] = s.Day;
            if (s.HasUtc) map["utc"] = s.Utc.ToString("o", CultureInfo.InvariantCulture);
            return map;
        }

        private static Stamp? Read(object? value)
        {
            if (!(value is IDictionary<string, object?> map)) return null;
            bool hasDay = map.TryGetValue("day", out var d) && d is double;
            DateTime utc = default;
            bool hasUtc = map.TryGetValue("utc", out var u) && u is string text
                && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out utc) && utc.Kind == DateTimeKind.Utc;
            if (hasDay && hasUtc) return Stamp.At((double)d!, utc);
            if (hasDay) return Stamp.DayOnly((double)d!);
            if (hasUtc) return Stamp.UtcOnly(utc);
            return null;
        }
    }
}
