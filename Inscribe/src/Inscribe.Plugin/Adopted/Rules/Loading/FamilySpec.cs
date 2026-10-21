// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the YAML
// format per family (Inscribe's enhancement family was removed in 0.2.0).
namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>The fixed facts of one YAML family: file pattern, main file, embedded default, sync key, id lists.</summary>
    internal sealed class FamilySpec
    {
        public static readonly FamilySpec Affixes = new FamilySpec("Inscribe.inscriptions", "ecf_inscriptions", new[] { "inscriptions" }, 4);
        public static readonly FamilySpec Economy = new FamilySpec("Inscribe.economy", "ecf_economy", new[] { "rarities", "runes", "classes" }, 3);

        private FamilySpec(string prefix, string syncKey, string[] idLists, int format)
        {
            Format = format;
            Prefix = prefix;
            SyncKey = syncKey;
            IdLists = idLists;
        }

        /// <summary>The root <c>format:</c> this version reads: 4 for inscriptions, 3 for economy.</summary>
        public int Format { get; }

        /// <summary>File names match <c>&lt;Prefix&gt;*.yml</c>.</summary>
        public string Prefix { get; }

        /// <summary>The Charter article name.</summary>
        public string SyncKey { get; }

        /// <summary>Dotted paths of the lists that merge by <c>id</c>, field by field.</summary>
        public string[] IdLists { get; }

        public string MainFile => Prefix + ".yml";

        public string DefaultResource => "Inscribe.config." + MainFile;

        public const string DefaultsOrigin = "(built-in defaults)";
    }

    /// <summary>One YAML text and the name it is reported under (a file name, or the built-in defaults).</summary>
    internal sealed class SourceText
    {
        public SourceText(string name, string text)
        {
            Name = name;
            Text = text;
        }

        public string Name { get; }
        public string Text { get; }
    }
}
