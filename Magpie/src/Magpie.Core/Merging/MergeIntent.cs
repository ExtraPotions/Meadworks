// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;

namespace ExtraPotions.Magpie.Merging
{
    /// <summary>
    /// The phase 1 intent record a merge target carries in <c>magpie.merge_in</c>: <c>v1;nonce;sourceToken;m;worldTime</c>
    /// (design 3.3 step 3). Culture-invariant; nonce and token never contain the separator.
    /// </summary>
    public sealed class MergeIntent
    {
        public const string Version = "v1";

        public MergeIntent(string nonce, string sourceToken, int moved, double worldTime)
        {
            if (!ValidField(nonce)) throw new ArgumentException("Nonce must be non-empty and contain no ';'.", nameof(nonce));
            if (!ValidField(sourceToken)) throw new ArgumentException("Source token must be non-empty and contain no ';'.", nameof(sourceToken));
            if (moved <= 0) throw new ArgumentException("Moved amount must be above zero.", nameof(moved));
            if (double.IsNaN(worldTime) || double.IsInfinity(worldTime)) throw new ArgumentException("World time must be finite.", nameof(worldTime));
            Nonce = nonce;
            SourceToken = sourceToken;
            Moved = moved;
            WorldTime = worldTime;
        }

        public string Nonce { get; }
        public string SourceToken { get; }
        public int Moved { get; }
        public double WorldTime { get; }

        public string Format() => string.Join(";", Version, Nonce, SourceToken,
            Moved.ToString(CultureInfo.InvariantCulture), WorldTime.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>Parses a record written by <see cref="Format"/>; null for anything else (unknown version, bad fields).</summary>
        public static MergeIntent? Parse(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            var parts = text!.Split(';');
            if (parts.Length != 5 || parts[0] != Version || !ValidField(parts[1]) || !ValidField(parts[2]))
                return null;
            if (!int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var moved) || moved <= 0)
                return null;
            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var time)
                || double.IsNaN(time) || double.IsInfinity(time))
                return null;
            return new MergeIntent(parts[1], parts[2], moved, time);
        }

        /// <summary>Most nonces a source keeps in <c>magpie.merge_out</c>; far more than can be pending within the 10 s reconcile window.</summary>
        public const int MaxSourceNonces = 8;

        /// <summary>
        /// Phase 2 record on a partial source: a comma list of the nonces of merges it gave to, newest last. A source can give
        /// to a second target before the first target reconciles; keeping both nonces stops that reconcile from reading
        /// "found without nonce" and rolling a completed merge back (which would lose items).
        /// </summary>
        public static string AppendNonce(string? record, string nonce)
        {
            if (!ValidField(nonce) || nonce.IndexOf(',') >= 0) throw new ArgumentException("Nonce must be non-empty and contain no ';' or ','.", nameof(nonce));
            var list = new System.Collections.Generic.List<string>(Split(record));
            if (list.Contains(nonce)) return string.Join(",", list);
            list.Add(nonce);
            if (list.Count > MaxSourceNonces) list.RemoveRange(0, list.Count - MaxSourceNonces);
            return string.Join(",", list);
        }

        public static bool HasNonce(string? record, string nonce) => Array.IndexOf(Split(record), nonce) >= 0;

        private static string[] Split(string? record) =>
            string.IsNullOrEmpty(record) ? Array.Empty<string>() : record!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        private static bool ValidField(string? value) => !string.IsNullOrEmpty(value) && value!.IndexOf(';') < 0;
    }
}
