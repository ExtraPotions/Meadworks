// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: choice values of design 4.8 and 4.10 (CreatureManager's names for the same options).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Wildblood.Config
{
    /// <summary>Design 4.8 Karma Mode (CreatureManager <c>Karma System Mode</c>).</summary>
    public enum KarmaMode { Off, KarmaLevelAndEnforcer, KarmaLevelOnly, EnforcerOnly }

    /// <summary>Design 4.10 Modifier Icon Layout (CreatureManager <c>Modifier HUD Icon Layout</c>).</summary>
    public enum IconLayout { FixedCategorySlots, RightPacked }

    /// <summary>A comma list of percentages 0 to 100 by star (design 4.6 Extra Roll Chance), invariant culture.</summary>
    public static class PercentList
    {
        public static IReadOnlyList<float> Parse(string? text)
        {
            var result = new List<float>();
            foreach (var part in (text ?? "").Split(','))
            {
                var t = part.Trim();
                if (t.Length == 0) continue;
                if (!float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || float.IsNaN(v) || float.IsInfinity(v))
                    return Array.Empty<float>();
                result.Add(Math.Min(100f, Math.Max(0f, v)));
            }
            return result;
        }

        public static string Format(IEnumerable<float> values) =>
            string.Join(", ", values.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));

        /// <summary>The canonical text; an empty or unreadable list gives <paramref name="fallback"/>.</summary>
        public static string Normalize(string? text, string fallback)
        {
            var values = Parse(text);
            return values.Count == 0 ? fallback : Format(values);
        }
    }

    /// <summary>A comma list of names (prefabs, global keys): trimmed, empty entries and repeats dropped, order kept.</summary>
    public static class NameList
    {
        public static IReadOnlyList<string> Parse(string? text) =>
            (text ?? "").Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal).ToList();

        public static string Normalize(string? text) => string.Join(", ", Parse(text));
    }
}
