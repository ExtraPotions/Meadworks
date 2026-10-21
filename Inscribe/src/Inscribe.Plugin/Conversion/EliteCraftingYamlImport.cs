// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// EliteCrafting YAML import: inscriptions 4, economy 3. Older sources are previewed without rewriting them.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;
namespace ExtraPotions.Inscribe.Conversion
{
    public sealed class YamlImportPlan
    {
        internal YamlImportPlan(int format, string text, IReadOnlyList<string> notes) { SourceFormat = format; Text = text; Notes = notes; }
        public int SourceFormat { get; }
        public string Text { get; }
        public IReadOnlyList<string> Notes { get; }
    }
    /// <summary>Previews per-family YAML conversion without writing sources. Economy stays at format 3;
    /// inscription formats 2/3 upgrade to 4 with custom-value conflict checks. Retired rune conversion and the
    /// user's 0.9.1 drop adjustment remain unchanged.</summary>
    public static class EliteCraftingYamlImport
    {
        private static readonly string[] Chances091 = { "35", "40", "45", "50", "55", "60", "65", "70" };
        private static readonly HashSet<string> RuneIds = new HashSet<string>(new[] { "awakening", "recasting", "ascension", "cleansing", "serpent", "dvergr_chisel",
            "gem_surtr", "gem_ymir", "gem_thor", "gem_nidhogg", "gem_hel", "gem_tyr", "gem_freyja", "gem_odin", "gem_skadi", "gem_heimdall", "gem_sleipnir" }, StringComparer.Ordinal);
        /// <summary>The removed runes and the rune their boss bonus rows became upstream (0.8.0: Eikthyr's Shaping to Recasting; 0.9.0: Consecrated to Ascension).</summary>
        private static readonly Dictionary<string, string> Successors = new Dictionary<string, string>(StringComparer.Ordinal) { ["shaping"] = "recasting", ["consecrated"] = "ascension" };
        public static YamlImportPlan Preview(string sourceName, string text, string pinnedEconomy, string? legacyInscriptions = null)
        {
            YamlMappingNode root = Read(text);
            var formatNode = Child(root, "format") as YamlScalarNode;
            bool inscriptions = sourceName.StartsWith("EliteCrafting_inscriptions", StringComparison.OrdinalIgnoreCase);
            if (!int.TryParse(formatNode?.Value, out var format) || (format != 2 && format != 3 && !(inscriptions && format == 4)))
                throw new FormatException("Supported formats: inscriptions 2, 3 or 4; economy 2 or 3.");
            var version = format == 4 ? "0.11.0" : format == 2 ? "0.7.0" : Version(sourceName, root);
            var notes = new List<string>();
            if (format == 2) notes.Add("Format 2 (EliteCrafting 0.7.0) normalized before per-family conversion.");
            root.Children[new YamlScalarNode("format")] = new YamlScalarNode(format == 4 ? "4" : "3");
            if (inscriptions)
            {
                var output = new StringWriter(); new YamlStream(new YamlDocument(root)).Save(output, false);
                string converted = format == 4 ? output.ToString() : InscriptionFormatMigration.Preview(sourceName, output.ToString(),
                    legacyInscriptions ?? throw new FormatException("The legacy inscription baseline is required for a safe conversion."), notes);
                return new YamlImportPlan(format, "# Imported from " + sourceName + " (EliteCrafting " + version + " format " + format + "); source unchanged.\n" + converted, notes);
            }
            if (Child(root, "runes") is YamlSequenceNode runes)
            {
                var pinned = Child(Read(pinnedEconomy), "runes") as YamlSequenceNode ?? throw new FormatException("The pinned rune catalog is missing.");
                for (int i = runes.Children.Count - 1; i >= 0; i--)
                {
                    if (!(runes.Children[i] is YamlMappingNode row)) throw new FormatException("A rune row must be a mapping.");
                    var id = Scalar(row, "id");
                    if (Successors.ContainsKey(id)) { runes.Children.RemoveAt(i); notes.Add("Rune '" + id + "' removed: EliteCrafting " + (id == "shaping" ? "0.8.0" : "0.9.0") + " retired it."); }
                    else if (!RuneIds.Contains(id)) { runes.Children.RemoveAt(i); notes.Add("Skipped unknown rune '" + id + "'."); }
                }
                // A complete main file keeps every built-in rune, socket stone and gem; partial override files stay partial.
                if (IsMainEconomy(sourceName))
                    foreach (var row in pinned.Children.OfType<YamlMappingNode>())
                        if (!runes.Children.OfType<YamlMappingNode>().Any(r => Scalar(r, "id") == Scalar(row, "id")))
                        { runes.Add(row); notes.Add("Restored default rune '" + Scalar(row, "id") + "' missing from the " + version + " main file."); }
            }
            if (Child(root, "drops") is YamlMappingNode drops)
            {
                if (Child(drops, "runes") is YamlMappingNode weights)
                {
                    foreach (var key in weights.Children.Keys.OfType<YamlScalarNode>().Where(k => !RuneIds.Contains(k.Value ?? "")).ToArray())
                    { weights.Children.Remove(key); notes.Add((Successors.ContainsKey(key.Value ?? "") ? "Drop weights of the removed rune '" : "Skipped unknown drop rune '") + key.Value + "' removed."); }
                    // A complete main file gets the default weights of the runes it no longer lists, so they still drop.
                    if (IsMainEconomy(sourceName) && Child(Read(pinnedEconomy), "drops") is YamlMappingNode pinnedDrops
                        && Child(pinnedDrops, "runes") is YamlMappingNode pinnedWeights)
                        foreach (var pair in pinnedWeights.Children.Where(p => p.Key is YamlScalarNode k && RuneIds.Contains(k.Value ?? "") && !weights.Children.ContainsKey(k)).ToArray())
                        { weights.Children.Add(pair.Key, pair.Value); notes.Add("Restored default drop weights for rune '" + ((YamlScalarNode)pair.Key).Value + "' missing from the " + version + " main file."); }
                }
                FilterBonus(drops, notes);
                // Decision 2026-10-09 (upstream updates item 7, refined by the user): 0.9.1 doubled rune drops, so its normal per-kill
                // and chest rune chances are halved on import; boss/creature bonus rows stay as configured (guaranteed stays 100).
                // Other versions are imported as configured.
                if (version == "0.9.1")
                {
                    var halved = HalveRuneChances(drops);
                    if (halved > 0)
                        notes.Add("EliteCrafting 0.9.1 rune drop chances halved on import (imported value x 0.5): " + halved
                            + " values in drops.chances.rune and drops.chests.rune_chance; boss/creature bonus rows unchanged; the source file is unchanged.");
                }
                else if (Child(drops, "chances") is YamlMappingNode chances && Child(chances, "rune") is YamlSequenceNode runeChances
                    && runeChances.Children.OfType<YamlScalarNode>().Select(n => n.Value).SequenceEqual(Chances091))
                    notes.Add("EliteCrafting 0.9.1 rune drop chances (35 to 70 % a kill) imported as configured; Inscribe's defaults are 15 to 40 %.");
            }
            var stream = new YamlStream(new YamlDocument(root));
            var writer = new StringWriter(); stream.Save(writer, false);
            return new YamlImportPlan(format, "# Imported from " + sourceName + " (EliteCrafting " + version + " format " + format + "); source unchanged.\n" + writer, notes);
        }
        /// <summary>Halves the per-kill rune chances and the chest rune chance under <c>drops</c>; bonus rows are left alone. Returns the count changed.</summary>
        private static int HalveRuneChances(YamlMappingNode drops)
        {
            int count = 0;
            if (Child(drops, "chances") is YamlMappingNode chances && Child(chances, "rune") is YamlSequenceNode perKill)
                foreach (var value in perKill.Children.OfType<YamlScalarNode>()) count += Halve(value);
            if (Child(drops, "chests") is YamlMappingNode chests && Child(chests, "rune_chance") is YamlScalarNode chest) count += Halve(chest);
            return count;
        }
        private static int Halve(YamlScalarNode node)
        {
            if (!double.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new FormatException("Rune drop chance '" + node.Value + "' is not a number.");
            node.Value = (value * 0.5).ToString("R", CultureInfo.InvariantCulture);
            return 1;
        }
        private static bool IsMainEconomy(string sourceName) => sourceName.Equals("EliteCrafting_economy.yml", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 0.8.0 still lists the Consecrated socket rune and gem drop weights; 0.9.0 removed both, so a complete main file
        /// without Consecrated is 0.9.1. Files that show neither (inscriptions, overrides) name the whole range.
        /// </summary>
        private static string Version(string sourceName, YamlMappingNode root)
        {
            var runes = (Child(root, "runes") as YamlSequenceNode)?.Children.OfType<YamlMappingNode>().ToList() ?? new List<YamlMappingNode>();
            var drops = Child(root, "drops") is YamlMappingNode d ? Child(d, "runes") as YamlMappingNode : null;
            if (runes.Any(r => Scalar(r, "verb") == "consecrate")
                || (drops != null && drops.Children.Keys.OfType<YamlScalarNode>().Any(k => (k.Value ?? "").StartsWith("gem_", StringComparison.Ordinal))))
                return "0.8.0";
            if (IsMainEconomy(sourceName) && runes.Count > 0 && !runes.Any(r => Scalar(r, "id") == "consecrated")) return "0.9.1";
            return "0.8.0 to 0.9.1";
        }

        private static void FilterBonus(YamlNode node, List<string> notes)
        {
            if (node is YamlMappingNode map)
            {
                if (Child(map, "bonus") is YamlSequenceNode rows)
                    for (int i = rows.Children.Count - 1; i >= 0; i--)
                    {
                        if (!(rows.Children[i] is YamlMappingNode row)) continue;
                        var rune = Scalar(row, "rune");
                        if (Successors.TryGetValue(rune, out var successor))
                        { row.Children[new YamlScalarNode("rune")] = new YamlScalarNode(successor); notes.Add("Bonus rune '" + rune + "' became '" + successor + "' (EliteCrafting " + (rune == "shaping" ? "0.8.0" : "0.9.0") + ")."); }
                        else if (!RuneIds.Contains(rune)) { notes.Add("Skipped unknown bonus rune '" + rune + "'."); rows.Children.RemoveAt(i); }
                    }
                foreach (var value in map.Children.Values) FilterBonus(value, notes);
            }
            else if (node is YamlSequenceNode sequence) foreach (var value in sequence.Children) FilterBonus(value, notes);
        }
        private static YamlMappingNode Read(string text)
        {
            try { var s = new YamlStream(); s.Load(new StringReader(text)); if (s.Documents.Count != 1 || !(s.Documents[0].RootNode is YamlMappingNode root)) throw new FormatException("The YAML must contain one mapping document."); return root; }
            catch (YamlDotNet.Core.YamlException e) { throw new FormatException("The source YAML is invalid: " + e.Message, e); }
        }
        private static YamlNode? Child(YamlMappingNode map, string key) => map.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;
        private static string Scalar(YamlMappingNode map, string key) => (Child(map, key) as YamlScalarNode)?.Value ?? "";
    }
}
