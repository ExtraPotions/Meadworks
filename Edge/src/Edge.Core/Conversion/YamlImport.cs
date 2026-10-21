// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the converter's YAML file mapping (design 10.3). The schema is unchanged, so files are copied under
// Edge's names; validation before Apply comes with the YAML normaliser (D03b).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ExtraPotions.Edge.Config;

namespace ExtraPotions.Edge.Conversion
{
    public sealed class YamlCopy
    {
        internal YamlCopy(string from, string to, RuleFamily family)
        {
            From = from;
            To = to;
            Family = family;
        }

        /// <summary>The source path as given.</summary>
        public string From { get; }

        /// <summary>The Edge file name (placed in Edge's config folder by the plugin).</summary>
        public string To { get; }

        public RuleFamily Family { get; }
    }

    public sealed class YamlPlan
    {
        internal YamlPlan(IReadOnlyList<YamlCopy> copies, IReadOnlyList<ImportNote> notes)
        {
            Copies = copies;
            Notes = notes;
        }

        public IReadOnlyList<YamlCopy> Copies { get; }
        public IReadOnlyList<ImportNote> Notes { get; }
    }

    public static class YamlImport
    {
        private static readonly Regex SourceName = new Regex(@"^SecondaryAttacks\.(Ranged|Melee|BloodMagic)(.*)\.yml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary><c>SecondaryAttacks.&lt;Family&gt;&lt;rest&gt;.yml</c> becomes <c>Edge.&lt;Family&gt;&lt;rest&gt;.yml</c>; other files are reported and skipped.</summary>
        public static YamlPlan Plan(IEnumerable<string> paths)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            var copies = new List<YamlCopy>();
            var notes = new List<ImportNote>();
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                var name = Path.GetFileName(path);
                var match = SourceName.Match(name);
                if (!match.Success)
                {
                    notes.Add(new ImportNote(ImportNoteKind.Skipped, path, name.IndexOf("AnimationReferences", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "Edge writes its own " + EdgeDataFiles.AnimationReferences
                        : "not a SecondaryAttacks rule file"));
                    continue;
                }
                var family = (RuleFamily)Enum.Parse(typeof(RuleFamily), match.Groups[1].Value, true);
                copies.Add(new YamlCopy(path, "Edge." + family + match.Groups[2].Value + EdgeDataFiles.Extension, family));
            }
            return new YamlPlan(copies, notes);
        }
    }
}
