// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Wildblood.Luck
{
    public static class NativeLuckCounters
    {
        public static int Bucket(int prefabHash, float chance) => unchecked(prefabHash + (int)(chance * 1000f));
        public static double MaximumReset(float chance) => Math.Min(int.MaxValue, Math.Truncate(1f / chance * 2f) + 1d);

        public static bool TryMaximumReset(float reportedChance, IEnumerable<float> sourceChances, out double maximum)
        {
            maximum = -1;
            bool exact = false;
            foreach (float chance in sourceChances)
            {
                if (!(chance > 0f && chance <= 0.3f) || Bucket(0, chance) != Bucket(0, reportedChance)) continue;
                exact |= chance == reportedChance;
                maximum = Math.Max(maximum, MaximumReset(chance));
            }
            return exact;
        }

        public static bool TryConvert(LuckTable table, Func<string, int> hash, out Dictionary<int, int> counters)
        {
            counters = new Dictionary<int, int>();
            foreach (var row in table.Entries)
            {
                if (!(row.Chance > 0f && row.Chance <= 0.3f) || row.Counter < 0) return false;
                int key = Bucket(hash(row.Prefab), row.Chance);
                if (counters.ContainsKey(key)) return false;
                counters.Add(key, row.Counter);
            }
            return true;
        }

        public static string Write(IDictionary<int, int> counters) => "1" + string.Concat(counters.OrderBy(p => p.Key)
            .Select(p => "|" + p.Key.ToString(CultureInfo.InvariantCulture) + ":" + p.Value.ToString(CultureInfo.InvariantCulture)));

        public static bool TryParse(string? text, out Dictionary<int, int> counters)
        {
            counters = new Dictionary<int, int>();
            string[] rows = (text ?? "").Split('|');
            if (rows[0] != "1") return false;
            for (int i = 1; i < rows.Length; i++)
            {
                string[] fields = rows[i].Split(':');
                if (fields.Length != 2 || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key) ||
                    !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ||
                    count < 0 || counters.ContainsKey(key)) return false;
                counters.Add(key, count);
            }
            return true;
        }
    }
}
