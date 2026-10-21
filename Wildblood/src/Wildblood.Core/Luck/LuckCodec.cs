// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT, adapted from SaveYourLuckBruh 1.0.2
// SaveYourLuckBruh/Core/StorageManager.cs (entry format, world key) at ece802da9f806f3ee9d6e50552beec479abae4ce.
// Changed by expDARE, 2026: Wildblood key and import of the source key; a portable ordered table instead of the game's
// static dictionary; the 4 KB published bound (design 3.4); malformed entries skipped as SaveYourLuckBruh does.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ExtraPotions.Wildblood.Luck
{
    /// <summary>
    /// Bad-luck counters per prefab and native chance bucket, in update order (oldest first).
    /// </summary>
    public sealed class LuckTable
    {
        private readonly List<(string Prefab, int Bucket)> order = new List<(string, int)>();
        private readonly Dictionary<(string Prefab, int Bucket), (float Chance, int Counter)> rows = new Dictionary<(string, int), (float, int)>();

        public int Count => rows.Count;

        /// <summary>Prefabs, oldest update first.</summary>
        public IReadOnlyList<string> Prefabs => order.Select(p => p.Prefab).Distinct(StringComparer.Ordinal).ToList();

        public IEnumerable<(string Prefab, float Chance, int Counter)> Entries =>
            order.Select(p => (p.Prefab, rows[p].Chance, rows[p].Counter));

        public (float Chance, int Counter) Get(string prefab) => rows[order.Last(p => p.Prefab == prefab)];

        public bool TryGet(string prefab, out (float Chance, int Counter) row)
        {
            for (int i = order.Count - 1; i >= 0; i--)
                if (order[i].Prefab == prefab) { row = rows[order[i]]; return true; }
            row = default;
            return false;
        }

        public bool TryGet(string prefab, float chance, out (float Chance, int Counter) row) =>
            rows.TryGetValue((prefab, NativeLuckCounters.Bucket(0, chance)), out row);

        /// <summary>Sets a row and moves it to the newest end.</summary>
        public void Set(string prefab, float chance, int counter)
        {
            if (string.IsNullOrEmpty(prefab)) throw new ArgumentException("Expected a prefab.", nameof(prefab));
            var key = (prefab, NativeLuckCounters.Bucket(0, chance));
            order.Remove(key);
            order.Add(key);
            rows[key] = (chance, counter);
        }

        public void Remove(string prefab)
        {
            foreach (var key in order.Where(p => p.Prefab == prefab).ToList())
            {
                order.Remove(key);
                rows.Remove(key);
            }
        }

        /// <summary>Applies an owner's evaluation (design 3.4 step 3) on the killer's client.</summary>
        public void Apply(LuckDelta delta)
        {
            if (delta.Weighted) throw new InvalidOperationException("Weighted pity deltas require the source catalog.");
            if (delta.NewCounter >= 0)
            {
                Set(delta.Prefab, delta.Chance, delta.NewCounter);
                return;
            }
            var current = TryGet(delta.Prefab, delta.Chance, out var row) ? row.Counter : 0;
            Set(delta.Prefab, delta.Chance, Math.Max(0, current - delta.Consumed));
        }
    }

    public static class LuckCodec
    {
        public const char EntryDelimiter = '|';
        public const char FieldDelimiter = ':';
        public const string Prefix = "wildblood.luck";
        public const string SourcePrefix = "vapok.mods.SaveYourLuckBruh";
        public const string ImportMarker = "wildblood.imported.luck";

        /// <summary>Most bytes of the counters published on the player's ZDO (design 3.4 step 1).</summary>
        public const int PublishBytes = 4096;

        public static string StorageKey(long worldUid) => Prefix + "." + worldUid.ToString(CultureInfo.InvariantCulture);

        public static string SourceKey(long worldUid) => SourcePrefix + "." + worldUid.ToString(CultureInfo.InvariantCulture);

        public static string MarkerKey(long worldUid) => ImportMarker + "." + worldUid.ToString(CultureInfo.InvariantCulture);

        public static string Write(LuckTable table) => string.Join(EntryDelimiter.ToString(), table.Entries.Select(p => Entry(p.Prefab, (p.Chance, p.Counter))));

        public static LuckTable Parse(string? text)
        {
            var table = new LuckTable();
            if (string.IsNullOrEmpty(text)) return table;
            foreach (var entry in text!.Split(EntryDelimiter))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                var fields = entry.Split(FieldDelimiter);
                if (fields.Length != 3 || fields[0].Length == 0) continue;
                if (float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var chance)
                    && int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var counter))
                    table.Set(fields[0], chance, counter);
            }
            return table;
        }

        public static bool CanPreserveFormat(string? text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            var seen = new HashSet<(string, int)>();
            foreach (string entry in text!.Split(EntryDelimiter))
            {
                if (entry.Length == 0) continue;
                var parsed = Parse(entry);
                if (parsed.Count != 1) return false;
                foreach (var row in parsed.Entries)
                    if (!(row.Chance > 0f && row.Chance <= 0.3f) || row.Counter < 0 ||
                        !seen.Add((row.Prefab, NativeLuckCounters.Bucket(0, row.Chance)))) return false;
            }
            return true;
        }

        /// <summary>The newest rows that fit in <paramref name="maxBytes"/> UTF-8 bytes, in table order.</summary>
        public static string Publish(LuckTable table, int maxBytes)
        {
            var kept = new List<string>();
            var bytes = 0;
            var rows = table.Entries.ToList();
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                var row = rows[i];
                var entry = Entry(row.Prefab, (row.Chance, row.Counter));
                var cost = Encoding.UTF8.GetByteCount(entry) + (kept.Count > 0 ? 1 : 0);
                if (bytes + cost > maxBytes) break;
                kept.Add(entry);
                bytes += cost;
            }
            kept.Reverse();
            return string.Join(EntryDelimiter.ToString(), kept);
        }

        /// <summary>
        /// Copies SaveYourLuckBruh's entry for the world into Wildblood's, once per world (marker key); the source entry is
        /// left in place. True when something was copied.
        /// </summary>
        public static bool ImportOnce(IDictionary<string, string> customData, long worldUid)
        {
            if (customData == null) throw new ArgumentNullException(nameof(customData));
            if (customData.ContainsKey(MarkerKey(worldUid))) return false;
            if (!customData.TryGetValue(SourceKey(worldUid), out var source) || string.IsNullOrEmpty(source)) return false;
            customData[MarkerKey(worldUid)] = "1";
            if (customData.TryGetValue(StorageKey(worldUid), out var existing) && !string.IsNullOrEmpty(existing)) return false;
            customData[StorageKey(worldUid)] = source;
            return true;
        }

        private static string Entry(string prefab, (float Chance, int Counter) row) =>
            prefab + FieldDelimiter + row.Chance.ToString("R", CultureInfo.InvariantCulture) + FieldDelimiter + row.Counter.ToString(CultureInfo.InvariantCulture);
    }
}
