// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: YAML family file names (design 4.10). Parsing, validation and hot reload come with Y02b.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtraPotions.Saga.Config
{
    /// <summary>The six YAML families: kill requirements, advanced powers, wood and mine finds, forage table, fishing snags.</summary>
    public enum DataFamily { PowerLevels, Powers, Finds, MineFinds, Forage, Snags }

    public static class SagaDataFiles
    {
        public const string Extension = ".yml";

        /// <summary>The base file name of a family, for example <c>Saga.PowerLevels.yml</c>.</summary>
        public static string BaseName(DataFamily family) => Stem(family) + Extension;

        /// <summary>
        /// The family of a file named <c>Saga.&lt;Family&gt;*.yml</c> (case-insensitive, any directory); null for any other
        /// file. The longest matching stem wins, so <c>Saga.PowerLevels.yml</c> is never read as a <c>Saga.Powers</c> layer.
        /// </summary>
        public static DataFamily? Classify(string? path)
        {
            var name = Path.GetFileName(path ?? "");
            if (!name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) return null;
            DataFamily? best = null;
            foreach (DataFamily family in Enum.GetValues(typeof(DataFamily)))
            {
                var stem = Stem(family);
                if (!name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)) continue;
                var next = name.Length > stem.Length ? name[stem.Length] : '\0';
                if (next != '.') continue;
                if (best == null || stem.Length > Stem(best.Value).Length) best = family;
            }
            return best;
        }

        /// <summary>A family's files in load order: the base file first, then the other layers by ordinal file name.</summary>
        public static IReadOnlyList<string> Layers(DataFamily family, IEnumerable<string> paths)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            var mine = paths.Where(p => Classify(p) == family).Distinct(StringComparer.Ordinal).ToList();
            var baseName = BaseName(family);
            return mine.OrderBy(p => string.Equals(Path.GetFileName(p), baseName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToList();
        }

        /// <summary>The hidden synchronized entry that carries a family's server text (C01 push).</summary>
        public static SagaSetting<string> Carrier(DataFamily family)
        {
            switch (family)
            {
                case DataFamily.PowerLevels: return SagaSettings.Data.PowerLevels;
                case DataFamily.Powers: return SagaSettings.Data.Powers;
                case DataFamily.Finds: return SagaSettings.Data.Finds;
                case DataFamily.MineFinds: return SagaSettings.Data.MineFinds;
                case DataFamily.Forage: return SagaSettings.Data.Forage;
                case DataFamily.Snags: return SagaSettings.Data.Snags;
                default: throw new ArgumentException("Unknown family.", nameof(family));
            }
        }

        private static string Stem(DataFamily family) => "Saga." + family;
    }
}
