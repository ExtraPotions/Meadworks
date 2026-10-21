// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Larder.Cooking
{
    public sealed class FermenterCredits
    {
        public const int Capacity = 4096;
        const int MaxKeyLength = 160;
        readonly Dictionary<string, long> starts = new Dictionary<string, long>(StringComparer.Ordinal);

        public int Count => starts.Count;

        public bool CanCredit(long world, string barrel, long start)
        {
            if (!TryKey(world, barrel, out string key) || start <= 0) return false;
            return starts.TryGetValue(key, out long credited) ? start > credited : starts.Count < Capacity;
        }

        public bool TryCredit(long world, string barrel, long start)
        {
            if (!CanCredit(world, barrel, start)) return false;
            TryKey(world, barrel, out string key);
            // Newer starts supersede older batches without making stale ready-state retries eligible again.
            starts[key] = start;
            return true;
        }

        public string Save()
        {
            var text = new StringBuilder("1");
            foreach (var entry in starts)
                text.Append('\n').Append(entry.Key).Append('\t').Append(entry.Value.ToString(CultureInfo.InvariantCulture));
            return text.ToString();
        }

        public static bool TryRestore(string text, out FermenterCredits history)
        {
            history = null;
            var restored = new FermenterCredits();
            if (string.IsNullOrEmpty(text)) { history = restored; return true; }
            if (text.Length > (MaxKeyLength + 22) * Capacity + 1) return false;
            var lines = text.Split('\n');
            if (lines[0] != "1" || lines.Length > Capacity + 1) return false;
            for (int i = 1; i < lines.Length; i++)
            {
                int separator = lines[i].IndexOf('\t');
                if (separator <= 0) return false;
                string key = lines[i].Substring(0, separator);
                int colon = key.IndexOf(':');
                if (colon <= 0 || !long.TryParse(key.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out long world)
                    || !TryKey(world, key.Substring(colon + 1), out string normalized) || key != normalized
                    || !long.TryParse(lines[i].Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long start)
                    || start <= 0 || restored.starts.ContainsKey(key)) return false;
                restored.starts.Add(key, start);
            }
            history = restored;
            return true;
        }

        static bool TryKey(long world, string barrel, out string key)
        {
            key = null;
            if (world == 0 || string.IsNullOrEmpty(barrel) || barrel.Length > 128
                || barrel.IndexOf('\n') >= 0 || barrel.IndexOf('\r') >= 0 || barrel.IndexOf('\t') >= 0) return false;
            key = world.ToString(CultureInfo.InvariantCulture) + ":" + barrel;
            return key.Length <= MaxKeyLength;
        }
    }
}
