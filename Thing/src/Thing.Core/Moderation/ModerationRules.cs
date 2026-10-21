// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: durations and reasons for moderation verbs (design A2 to A8, 4.2) and the read side of the game's
// list files (A5, A10). The game itself writes bannedlist.txt, permittedlist.txt and adminlist.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ExtraPotions.Thing.Moderation
{
    public enum DurationUnit { Minutes, Hours }

    public static class Durations
    {
        /// <summary>
        /// Parses <c>90m</c>, <c>24h</c>, <c>3d</c> or a bare whole number in <paramref name="defaultUnit"/>. Zero, negative,
        /// fractional and unknown texts give null. Longer durations are capped at <paramref name="maximum"/> default units.
        /// </summary>
        public static TimeSpan? Parse(string? text, DurationUnit defaultUnit, int maximum)
        {
            var t = (text ?? "").Trim().ToLowerInvariant();
            if (t.Length == 0) return null;
            var unit = t[t.Length - 1];
            var number = char.IsDigit(unit) ? t : t.Substring(0, t.Length - 1);
            if (number.Length == 0 || !number.All(char.IsDigit) || !long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n <= 0) return null;
            double minutes;
            switch (char.IsDigit(unit) ? (defaultUnit == DurationUnit.Hours ? 'h' : 'm') : unit)
            {
                case 'm': minutes = n; break;
                case 'h': minutes = n * 60.0; break;
                case 'd': minutes = n * 1440.0; break;
                default: return null;
            }
            var cap = defaultUnit == DurationUnit.Hours ? maximum * 60.0 : maximum;
            return TimeSpan.FromMinutes(Math.Min(minutes, cap));
        }

        /// <summary>Short text such as <c>1d 2h</c> or <c>45m</c> (whole minutes).</summary>
        public static string Describe(TimeSpan span)
        {
            var total = (long)Math.Max(0, Math.Round(span.TotalMinutes));
            var parts = new List<string>();
            if (total / 1440 > 0) parts.Add((total / 1440).ToString(CultureInfo.InvariantCulture) + "d");
            if (total / 60 % 24 > 0) parts.Add((total / 60 % 24).ToString(CultureInfo.InvariantCulture) + "h");
            if (total % 60 > 0 || parts.Count == 0) parts.Add((total % 60).ToString(CultureInfo.InvariantCulture) + "m");
            return string.Join(" ", parts);
        }
    }

    public static class Reasons
    {
        public const int MaxLength = 200;

        /// <summary>One line: whitespace runs become one space, trimmed, at most 200 characters.</summary>
        public static string Normalize(string? text)
        {
            var b = new StringBuilder();
            var space = false;
            foreach (var c in (text ?? "").Trim())
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c)) { space = true; continue; }
                if (space && b.Length > 0) b.Append(' ');
                space = false;
                b.Append(c);
            }
            return b.Length > MaxLength ? b.ToString(0, MaxLength) : b.ToString();
        }

        /// <summary>Kick Reason Required (design 4.2): a required reason must not be blank.</summary>
        public static bool Acceptable(string? text, bool required) => !required || Normalize(text).Length > 0;
    }

    /// <summary>The game's list files (<c>adminlist.txt</c>, <c>bannedlist.txt</c>, <c>permittedlist.txt</c>): one ID per line, <c>//</c> comments.</summary>
    public static class GameListFile
    {
        public static IReadOnlyList<string> Parse(string? text)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
                if (seen.Add(line)) result.Add(line);
            }
            return result;
        }

        /// <summary>
        /// Whether a platform ID is listed. A bare ID and its platform form (<c>Steam_&lt;id&gt;</c>) are the same player,
        /// since older lists hold bare Steam IDs; two different platform prefixes never match, and names never do.
        /// </summary>
        public static bool Contains(IEnumerable<string> list, string? id)
        {
            var wanted = (id ?? "").Trim();
            if (wanted.Length == 0) return false;
            return list.Any(entry => Same(entry, wanted));
        }

        static bool Same(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal)) return true;
            var pa = a.IndexOf('_');
            var pb = b.IndexOf('_');
            if (pa >= 0 && pb >= 0) return false;
            var bareA = pa >= 0 ? a.Substring(pa + 1) : a;
            var bareB = pb >= 0 ? b.Substring(pb + 1) : b;
            return bareA.Length > 0 && bareA.All(char.IsDigit) && string.Equals(bareA, bareB, StringComparison.Ordinal);
        }
    }
}
