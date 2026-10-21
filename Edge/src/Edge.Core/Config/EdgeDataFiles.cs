// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: YAML family file names (design 4.5). Parsing, validation and hot reload come with D02b.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtraPotions.Edge.Config
{
    /// <summary>The three YAML rule families (SecondaryAttacks' Ranged, Melee and BloodMagic file schemas).</summary>
    public enum RuleFamily { Ranged, Melee, BloodMagic }

    public static class EdgeDataFiles
    {
        /// <summary>Read-only reference for <c>animation</c> fields, written on first run.</summary>
        public const string AnimationReferences = "Edge.AnimationReferences.txt";

        public const string Extension = ".yml";

        /// <summary>The base file name of a family, for example <c>Edge.Ranged.yml</c>.</summary>
        public static string BaseName(RuleFamily family) => Stem(family) + Extension;

        /// <summary>
        /// The family of a file named <c>Edge.&lt;Family&gt;*.yml</c> (case-insensitive, any directory); null for any other file.
        /// </summary>
        public static RuleFamily? Classify(string? path)
        {
            var name = Path.GetFileName(path ?? "");
            if (!name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) return null;
            foreach (RuleFamily family in Enum.GetValues(typeof(RuleFamily)))
                if (name.StartsWith(Stem(family), StringComparison.OrdinalIgnoreCase)) return family;
            return null;
        }

        /// <summary>A family's files in load order: the base file first, then the other layers by ordinal file name.</summary>
        public static IReadOnlyList<string> Layers(RuleFamily family, IEnumerable<string> paths)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            var mine = paths.Where(p => Classify(p) == family).Distinct(StringComparer.Ordinal).ToList();
            var baseName = BaseName(family);
            return mine.OrderBy(p => string.Equals(Path.GetFileName(p), baseName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToList();
        }

        /// <summary>The hidden synchronized entry that carries a family's server text (C01 push).</summary>
        public static EdgeSetting<string> Carrier(RuleFamily family)
        {
            switch (family)
            {
                case RuleFamily.Ranged: return EdgeSettings.Data.RangedRules;
                case RuleFamily.Melee: return EdgeSettings.Data.MeleeRules;
                case RuleFamily.BloodMagic: return EdgeSettings.Data.BloodMagicRules;
                default: throw new ArgumentException("Unknown family.", nameof(family));
            }
        }

        private static string Stem(RuleFamily family) => "Edge." + family;
    }
}
