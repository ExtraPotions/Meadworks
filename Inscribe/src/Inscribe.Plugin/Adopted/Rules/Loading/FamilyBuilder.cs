// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; legacy runes in
// economy files read as their successors (2026-10-19).
using System;
using System.Collections.Generic;
using System.IO;
using ExtraPotions.Inscribe.Adopted.Core;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Turns a family's texts into one merged document: what mods registered through the API (<see cref="CodeLayer"/>,
    /// always at the bottom), the built-in defaults (unless the main file says <c>use_defaults: false</c>), then every
    /// file in the order given (main file first, then by name). Syntax errors are recorded per file; the caller parses
    /// the merged document and decides.
    /// </summary>
    internal static class FamilyBuilder
    {
        public const string UseDefaultsKey = "use_defaults";

        public static YamlMappingNode Merge(FamilySpec spec, IReadOnlyList<SourceText> files, RuleIssues issues) =>
            Merge(spec, files, issues, out _);

        /// <summary>
        /// <see cref="Merge(FamilySpec, IReadOnlyList{SourceText}, RuleIssues)"/>, also saying whether the built-in
        /// defaults were layered in; <paramref name="withCode"/> false leaves the registered layer out (the last resort
        /// when it breaks the defaults).
        /// </summary>
        public static YamlMappingNode Merge(FamilySpec spec, IReadOnlyList<SourceText> files, RuleIssues issues, out bool usedDefaults,
            bool withCode = true)
        {
            List<SourceLayer> layers = FileLayers(spec, files, issues, out bool useDefaults);
            if (useDefaults)
            {
                layers.Insert(0, Defaults(spec, issues));
            }
            SourceLayer? code = withCode ? CodeLayer.For(spec, issues) : null;
            if (code != null)
            {
                layers.Insert(0, code);
            }
            usedDefaults = useDefaults;
            YamlMappingNode merged = YamlMerge.Merge(layers, spec.IdLists, issues);
            if (spec == FamilySpec.Economy)
            {
                // Legacy drop weights fold after the merge, so an override naming only tempering adds to the Honing row below it.
                var notes = new List<string>();
                global::ExtraPotions.Inscribe.Conversion.EconomyRuneMigration.ApplyDropWeights(merged, notes);
                foreach (string note in notes) issues.Note("merged rules: " + note);
            }
            return merged;
        }

        // The files that load, in the order given; whether the main file leaves the built-in defaults in.
        private static List<SourceLayer> FileLayers(FamilySpec spec, IReadOnlyList<SourceText> files, RuleIssues issues, out bool useDefaults)
        {
            List<SourceLayer> layers = new List<SourceLayer>();
            useDefaults = true;
            foreach (SourceText file in files)
            {
                YamlMappingNode? root = Load(file, issues, spec);
                if (root == null)
                {
                    continue;
                }
                useDefaults &= ReadUseDefaults(spec, file, root, issues);
                layers.Add(new SourceLayer(file.Name, root, builtIn: false));
            }
            return layers;
        }

        /// <summary>The embedded default text of a family (also written as the main file on first run).</summary>
        public static string DefaultText(FamilySpec spec) =>
            Embedded.Text(spec.DefaultResource) ?? throw new InvalidOperationException($"missing resource {spec.DefaultResource}");

        private static SourceLayer Defaults(FamilySpec spec, RuleIssues issues)
        {
            SourceText text = new SourceText(FamilySpec.DefaultsOrigin, DefaultText(spec));
            YamlMappingNode root = Load(text, issues) ?? new YamlMappingNode();
            return new SourceLayer(text.Name, root, builtIn: true);
        }

        private static bool ReadUseDefaults(FamilySpec spec, SourceText file, YamlMappingNode root, RuleIssues issues)
        {
            YamlNode? node = YamlNodes.Child(root, UseDefaultsKey);
            if (node == null)
            {
                return true;
            }
            if (!string.Equals(file.Name, spec.MainFile, StringComparison.OrdinalIgnoreCase))
            {
                issues.Warn(UseDefaultsKey, node, $"only read from {spec.MainFile}; ignored here");
                return true;
            }
            if (YamlNodes.TryBool(node, out bool value))
            {
                return value;
            }
            issues.Error(UseDefaultsKey, node, "should be true or false");
            return true;
        }

        private static YamlMappingNode? Load(SourceText file, RuleIssues issues, FamilySpec? spec = null)
        {
            try
            {
                string text = file.Text;
                if (spec == FamilySpec.Affixes && RuleFormat.Of(text) == 3)
                {
                    var notes = new List<string>();
                    text = global::ExtraPotions.Inscribe.Conversion.InscriptionFormatMigration.Preview(file.Name, text,
                        Embedded.Text("Inscribe.migrations.inscriptions-v3.yml") ?? throw new InvalidOperationException("Missing legacy inscription baseline."), notes);
                    foreach (string note in notes) issues.Note(file.Name + ": " + note);
                }
                YamlStream stream = new YamlStream();
                stream.Load(new StringReader(text));
                if (stream.Documents.Count == 0)
                {
                    return new YamlMappingNode();
                }
                YamlMappingNode? root = AsRoot(file, stream.Documents[0].RootNode, issues);
                if (spec == FamilySpec.Economy && root != null)
                {
                    // Awakening and Tempering were folded into Ascension and Honing (2026-10-19); older files read as the new set.
                    var notes = new List<string>();
                    global::ExtraPotions.Inscribe.Conversion.EconomyRuneMigration.Apply(root, notes, dropWeights: false);
                    foreach (string note in notes) issues.Note(file.Name + ": " + note);
                }
                return root;
            }
            catch (YamlException e)
            {
                issues.Error("", null, $"{file.Name}:{e.Start.Line}: {e.Message}");
            }
            catch (Exception e)
            {
                issues.Error("", null, $"{file.Name}: could not be read: {e.Message}");
            }
            return null;
        }

        private static YamlMappingNode? AsRoot(SourceText file, YamlNode root, RuleIssues issues)
        {
            if (root is YamlMappingNode map)
            {
                issues.Register(map, file.Name);
                return map;
            }
            if (YamlNodes.IsNull(root))
            {
                return new YamlMappingNode();
            }
            issues.Error("", null, $"{file.Name}: the file should be a block of keyed values");
            return null;
        }
    }
}
