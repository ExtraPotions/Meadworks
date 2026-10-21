// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;

namespace ExtraPotions.Magpie.Merging
{
    /// <summary>
    /// The phase 1 intent on the target (<c>magpie.merge_in</c>). v1 is <c>v1;nonce;sourceToken;m;worldTime</c>.
    /// v2 adds <c>;0</c> or <c>;1</c>. 1 is written before a source is removed, and in the same step as a shrink.
    /// A baseline adds <c>;preMerge;d</c> (the source is removed) or <c>;preMerge;s</c> (the source stays).
    /// </summary>
    public sealed class MergeIntent
    {
        public const string Version = "v1";
        public const string VersionWithFlag = "v2";

        public MergeIntent(string nonce, string sourceToken, int moved, double worldTime)
            : this(nonce, sourceToken, moved, worldTime, true, false)
        {
        }

        private MergeIntent(string nonce, string sourceToken, int moved, double worldTime, bool tracksPhase2, bool phase2Saved,
            int? preMergeStack = null, bool? destroysSource = null)
        {
            if (!ValidField(nonce)) throw new ArgumentException("Nonce must be non-empty and contain no ';'.", nameof(nonce));
            if (!ValidField(sourceToken)) throw new ArgumentException("Source token must be non-empty and contain no ';'.", nameof(sourceToken));
            if (moved <= 0) throw new ArgumentException("Moved amount must be above zero.", nameof(moved));
            if (double.IsNaN(worldTime) || double.IsInfinity(worldTime)) throw new ArgumentException("World time must be finite.", nameof(worldTime));
            if (preMergeStack != null && preMergeStack.Value < 1)
                throw new ArgumentException("Pre-merge stack must be at least 1.", nameof(preMergeStack));
            if (preMergeStack == null && destroysSource != null)
                throw new ArgumentException("A removal bit requires a pre-merge stack.", nameof(destroysSource));
            Nonce = nonce;
            SourceToken = sourceToken;
            Moved = moved;
            WorldTime = worldTime;
            TracksPhase2 = tracksPhase2;
            Phase2Saved = phase2Saved;
            PreMergeStack = preMergeStack;
            DestroysSource = destroysSource;
        }

        public string Nonce { get; }
        public string SourceToken { get; }
        public int Moved { get; }
        public double WorldTime { get; }

        /// <summary>v2 records whether phase 2 has been saved. v1 does not.</summary>
        public bool TracksPhase2 { get; }

        /// <summary>True only after phase 2 has been saved. A removal saves this before the source is destroyed.</summary>
        public bool Phase2Saved { get; }

        /// <summary>Target stack before phase 1. Null on a record that did not store one.</summary>
        public int? PreMergeStack { get; }

        /// <summary>True when phase 2 removes the source. Null when the record does not say.</summary>
        public bool? DestroysSource { get; }

        public MergeIntent WithPhase2Saved() =>
            new MergeIntent(Nonce, SourceToken, Moved, WorldTime, true, true, PreMergeStack, DestroysSource);

        /// <summary>Records the target count before phase 1 and whether phase 2 removes the source.</summary>
        public MergeIntent WithBaseline(int preMergeStack, bool destroysSource) =>
            new MergeIntent(Nonce, SourceToken, Moved, WorldTime, true, Phase2Saved, preMergeStack, destroysSource);

        public string Format()
        {
            var text = string.Join(";", TracksPhase2 ? VersionWithFlag : Version, Nonce, SourceToken,
                Moved.ToString(CultureInfo.InvariantCulture), WorldTime.ToString("R", CultureInfo.InvariantCulture));
            if (!TracksPhase2)
                return text;
            text += ";" + (Phase2Saved ? "1" : "0");
            if (PreMergeStack == null)
                return text;
            return text + ";" + PreMergeStack.Value.ToString(CultureInfo.InvariantCulture) + ";" + (DestroysSource == true ? "d" : "s");
        }

        /// <summary>Parses a record written by <see cref="Format"/>; null for anything else (unknown version, bad fields).</summary>
        public static MergeIntent? Parse(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            var parts = text!.Split(';');
            if (parts.Length == 5 && parts[0] == Version)
                return From(parts, false, false, null, null);
            if (parts.Length == 6 && parts[0] == VersionWithFlag && Flag(parts[5]))
                return From(parts, true, parts[5] == "1", null, null);
            if (parts.Length == 8 && parts[0] == VersionWithFlag && Flag(parts[5]) && (parts[7] == "d" || parts[7] == "s")
                && int.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out var preMerge) && preMerge > 0)
                return From(parts, true, parts[5] == "1", preMerge, parts[7] == "d");
            return null;
        }

        private static bool Flag(string text) => text == "0" || text == "1";

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

        private static MergeIntent? From(string[] parts, bool tracksPhase2, bool phase2Saved, int? preMergeStack, bool? destroysSource)
        {
            if (!ValidField(parts[1]) || !ValidField(parts[2]))
                return null;
            if (!int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var moved) || moved <= 0)
                return null;
            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var time)
                || double.IsNaN(time) || double.IsInfinity(time))
                return null;
            return new MergeIntent(parts[1], parts[2], moved, time, tracksPhase2, phase2Saved, preMergeStack, destroysSource);
        }

        private static string[] Split(string? record) =>
            string.IsNullOrEmpty(record) ? Array.Empty<string>() : record!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        private static bool ValidField(string? value) => !string.IsNullOrEmpty(value) && value!.IndexOf(';') < 0;
    }
}
