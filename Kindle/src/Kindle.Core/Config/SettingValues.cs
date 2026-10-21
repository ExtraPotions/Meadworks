// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Kindle.Config
{
    /// <summary>An HTML color (#RGB, #RRGGBB or #RRGGBBAA, any case). Names such as "red" are not accepted.</summary>
    public readonly struct HtmlColour
    {
        private readonly bool hasAlpha;

        private HtmlColour(byte r, byte g, byte b, byte a, bool hasAlpha)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            this.hasAlpha = hasAlpha;
        }

        public byte R { get; }

        public byte G { get; }

        public byte B { get; }

        public byte A { get; }

        public static bool TryParse(string? text, out HtmlColour colour)
        {
            colour = default;
            var t = text?.Trim();
            if (t == null || t.Length < 2 || t[0] != '#') return false;
            var hex = t.Substring(1);
            if (hex.Length == 3) hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
            if (hex.Length != 6 && hex.Length != 8) return false;
            if (!uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)) return false;
            if (hex.Length == 6)
                colour = new HtmlColour((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255, false);
            else
                colour = new HtmlColour((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value, true);
            return true;
        }

        /// <summary>Upper-case #RRGGBB, or #RRGGBBAA when the text carried an alpha channel.</summary>
        public override string ToString() =>
            "#" + R.ToString("X2", CultureInfo.InvariantCulture) + G.ToString("X2", CultureInfo.InvariantCulture) + B.ToString("X2", CultureInfo.InvariantCulture)
            + (hasAlpha ? A.ToString("X2", CultureInfo.InvariantCulture) : "");
    }

    /// <summary>Salvage's Mod Data Prefixes: a comma list, trimmed, case-sensitive, blanks and duplicates dropped.</summary>
    public static class ModDataPrefixes
    {
        public static IReadOnlyList<string> Parse(string? text)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            foreach (var part in (text ?? "").Split(','))
            {
                var prefix = part.Trim();
                if (prefix.Length > 0 && seen.Add(prefix)) result.Add(prefix);
            }
            return result;
        }

        public static string Normalize(string? text) => string.Join(",", Parse(text));

        /// <summary>Whether an item data key starts with any prefix (ordinal, case-sensitive).</summary>
        public static bool Matches(IEnumerable<string> prefixes, string? key) =>
            key != null && prefixes.Any(p => p.Length > 0 && key.StartsWith(p, StringComparison.Ordinal));
    }
}
