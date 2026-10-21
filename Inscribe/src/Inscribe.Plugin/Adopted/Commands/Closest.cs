// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>"Did you mean" for unknown ids: the candidates with the smallest edit distance, prefix matches first.</summary>
    internal static class Closest
    {
        /// <summary>Up to <paramref name="max"/> candidates closest to <paramref name="typed"/>, comma separated.</summary>
        public static string To(string typed, IEnumerable<string> candidates, int max = 3)
        {
            List<KeyValuePair<int, string>> scored = new List<KeyValuePair<int, string>>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string candidate in candidates)
            {
                if (seen.Add(candidate))
                {
                    scored.Add(new KeyValuePair<int, string>(Score(typed, candidate), candidate));
                }
            }
            scored.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : string.CompareOrdinal(a.Value, b.Value));
            List<string> best = new List<string>();
            for (int i = 0; i < scored.Count && i < max; i++)
            {
                best.Add(scored[i].Value);
            }
            return best.Count == 0 ? "(none)" : string.Join(", ", best);
        }

        private static int Score(string typed, string candidate) =>
            candidate.StartsWith(typed, StringComparison.Ordinal) ? 0 : 1 + Distance(typed, candidate);

        /// <summary>Levenshtein distance, two rows.</summary>
        public static int Distance(string a, string b)
        {
            int[] previous = new int[b.Length + 1];
            int[] current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++)
            {
                previous[j] = j;
            }
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }
                int[] swap = previous;
                previous = current;
                current = swap;
            }
            return previous[b.Length];
        }
    }
}
