// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a main economy file written before Inscribe 0.2.0 holds the older defaults, and its rows replace
// the built-in ones field by field. The Dvergr Chisel and the gems listed only normal, magic and rare, so they refused
// Epic and Legendary items; Cleansing and the Serpent Rune did the same. This rewrites, once, only the values that still
// equal those older defaults (and the entries kept only for removed systems), keeping every value the owner changed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExtraPotions.Inscribe.Runes;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Conversion
{
    /// <summary>
    /// The on-disk update of a pre-0.2.0 <c>Inscribe.economy.yml</c> (the main file only; extra files are the owner's
    /// own overrides). A value equal to the old shipped default moves to the new default; a changed value stays. The
    /// text is edited in place, so comments, layout and every other line are kept. A marker comment makes it run once.
    /// </summary>
    internal static class EconomyDefaultsMigration
    {
        public const string MarkerPrefix = "# inscribe-defaults: 0.2.0";

        private static readonly Regex MarkerLine = new Regex(@"^# inscribe-defaults: 0\.2\.0\b", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        private static readonly Regex FormatLine = new Regex(@"^format[ \t]*:[^\r\n]*\r?\n", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        private static readonly string[] Gems =
        {
            "gem_surtr", "gem_ymir", "gem_thor", "gem_nidhogg", "gem_hel", "gem_tyr", "gem_freyja", "gem_odin", "gem_skadi",
            "gem_heimdall", "gem_sleipnir",
        };

        /// <summary>Rune rows whose <c>applies_to</c> moves from the old default to the new one (Inscribe 0.1.x to 0.2.0).</summary>
        private static readonly Dictionary<string, (string[] Old, string[] New)> AppliesTo = BuildAppliesTo();

        private static readonly double[] OldAwakeningWeights = { 37, 26, 21, 18, 15, 14, 12, 10 };
        private static readonly double[] OldAscensionWeights = { 0, 10, 12, 13, 13, 12, 12, 11 };
        private static readonly double[] NewAscensionWeights = { 37, 36, 33, 31, 28, 26, 24, 21 };
        private static readonly double[] OldRecastingWeights = { 50, 50, 50, 50, 50, 50, 50, 50 };
        private static readonly double[] NewRecastingWeights = { 48, 45, 43, 42, 40, 40, 38, 38 };

        private static Dictionary<string, (string[], string[])> BuildAppliesTo()
        {
            var socketOld = new[] { "normal", "magic", "rare" };
            var socketNew = new[] { "normal", "magic", "rare", "mw_epic", "mw_legendary" };
            var map = new Dictionary<string, (string[], string[])>(StringComparer.Ordinal)
            {
                ["dvergr_chisel"] = (socketOld, socketNew),
                ["cleansing"] = (new[] { "magic", "rare" }, new[] { "magic", "rare", "mw_epic", "mw_legendary" }),
                ["serpent"] = (new[] { "magic", "rare" }, new[] { "magic", "rare", "mw_epic", "mw_legendary" }),
            };
            foreach (var gem in Gems) map[gem] = (socketOld, socketNew);
            return map;
        }

        /// <summary>What <see cref="UpdateFile"/> did.</summary>
        internal sealed class FileResult
        {
            public FileResult(string text, string? backup, IReadOnlyList<string> notes)
            {
                Text = text;
                Backup = backup;
                Notes = notes;
            }

            /// <summary>The text to load: the updated one, or the original when nothing changed or the write failed.</summary>
            public string Text { get; }

            /// <summary>The copy of the original, or null when the file was not rewritten.</summary>
            public string? Backup { get; }

            public IReadOnlyList<string> Notes { get; }

            public string? Error { get; internal set; }

            public bool Rewritten => Backup != null && Error == null;
        }

        /// <summary>True when the text carries the marker of a finished update.</summary>
        public static bool IsMarked(string text) => MarkerLine.IsMatch(text ?? "");

        /// <summary>
        /// The updated text of a main economy file, or null when there is nothing to do (already marked, not written by
        /// a pre-0.2.0 Inscribe, unreadable, or no value still at an old default). Each change adds a note.
        /// </summary>
        public static string? Update(string text, ICollection<string> notes)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (notes == null) throw new ArgumentNullException(nameof(notes));
            if (IsMarked(text)) return null;
            YamlMappingNode? root;
            try
            {
                var stream = new YamlStream();
                stream.Load(new StringReader(text));
                root = stream.Documents.Count > 0 ? stream.Documents[0].RootNode as YamlMappingNode : null;
            }
            catch (Exception)
            {
                return null; // the loader reports the syntax error; nothing is rewritten
            }
            if (root == null || !WrittenBefore020(root)) return null;

            var edits = new List<Edit>();
            var local = new List<string>();
            RuneRows(text, root, edits, local);
            if (Child(root, "drops") is YamlMappingNode drops)
            {
                DropWeights(text, drops, edits, local);
                Bonuses(drops, "bosses", edits, local);
                Bonuses(drops, "creatures", edits, local);
                if (Child(drops, "scrolls") != null && RemovePair(text, drops, "scrolls", edits, extendOverComments: "scroll"))
                    local.Add("drops.scrolls (enhancement scroll drops, removed in 0.2.0) was removed");
            }
            if (edits.Count == 0) return null;
            if (Overlap(edits)) return null;

            var sb = new StringBuilder(text);
            foreach (var edit in edits.OrderByDescending(e => e.Start))
            {
                sb.Remove(edit.Start, edit.End - edit.Start);
                sb.Insert(edit.Start, edit.Text);
            }
            var updated = AddMarker(sb.ToString());
            foreach (var note in local) notes.Add(note);
            return updated;
        }

        /// <summary>
        /// Updates the main economy file at <paramref name="path"/> whose current text is <paramref name="text"/>: copies
        /// the original next to it (<c>Inscribe.economy.pre-0.2.0.bak</c>, numbered when taken), then writes the update.
        /// Nothing is written when there is nothing to do; when the copy or the write fails, the original text loads.
        /// </summary>
        public static FileResult UpdateFile(string path, string text)
        {
            var notes = new List<string>();
            var updated = Update(text, notes);
            if (updated == null) return new FileResult(text, null, notes);
            string backup = FreeBackupPath(path);
            try
            {
                File.Copy(path, backup, overwrite: false);
            }
            catch (Exception e)
            {
                return new FileResult(text, null, notes) { Error = "could not copy the original (" + e.Message + ")" };
            }
            try
            {
                string temp = path + ".tmp";
                File.WriteAllText(temp, updated, new UTF8Encoding(false));
                File.Copy(temp, path, overwrite: true);
                File.Delete(temp);
            }
            catch (Exception e)
            {
                return new FileResult(text, backup, notes) { Error = "could not write the update (" + e.Message + "); the original is unchanged" };
            }
            return new FileResult(updated, backup, notes);
        }

        public static string FreeBackupPath(string path)
        {
            string stem = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path));
            string candidate = stem + ".pre-0.2.0.bak";
            for (int n = 2; File.Exists(candidate); n++) candidate = stem + ".pre-0.2.0." + n.ToString(CultureInfo.InvariantCulture) + ".bak";
            return candidate;
        }

        // Only a file that still names something 0.2.0 removed: a legacy rune row or drop weight, or the scroll drops.
        private static bool WrittenBefore020(YamlMappingNode root)
        {
            if (Child(root, "runes") is YamlSequenceNode runes
                && runes.Children.OfType<YamlMappingNode>().Any(r => LegacyRunes.IsLegacy(Scalar(r, "id"))))
                return true;
            if (!(Child(root, "drops") is YamlMappingNode drops)) return false;
            if (Child(drops, "scrolls") != null) return true;
            return Child(drops, "runes") is YamlMappingNode weights && LegacyRunes.Ids.Any(id => Child(weights, id) != null);
        }

        private static void RuneRows(string text, YamlMappingNode root, List<Edit> edits, List<string> notes)
        {
            if (!(Child(root, "runes") is YamlSequenceNode runes)) return;
            var rows = runes.Children.OfType<YamlMappingNode>().ToList();
            var awakening = rows.FirstOrDefault(r => Scalar(r, "id") == "awakening");
            foreach (var row in rows)
            {
                var id = Scalar(row, "id");
                if (id != null && AppliesTo.TryGetValue(id, out var change))
                    ReplaceList(text, row, change.Old, change.New, "runes." + id, edits, notes);
            }
            // Awakening's job moved into Ascension: the old default pair becomes the new Ascension row.
            if (awakening == null || !IsOldAwakening(awakening)) return;
            var ascension = rows.FirstOrDefault(r => Scalar(r, "id") == "ascension");
            if (ascension != null && !ReplaceList(text, ascension, new[] { "magic" }, new[] { "normal", "magic", "rare" }, "runes.ascension", edits, notes)
                && !SameList(Child(ascension, "applies_to"), new[] { "normal", "magic", "rare" }))
                return; // an Ascension the owner changed: the loader still reads the Awakening row as Ascension
            if (RemoveLines(text, awakening, edits))
                notes.Add("runes.awakening (folded into Ascension) was removed");
        }

        private static bool IsOldAwakening(YamlMappingNode row)
        {
            var keys = row.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value).ToArray();
            return keys.Length == 3 && keys.Contains("id") && Scalar(row, "verb") == "promote" && SameList(Child(row, "applies_to"), new[] { "normal" });
        }

        private static bool ReplaceList(string text, YamlMappingNode row, string[] old, string[] next, string path, List<Edit> edits, List<string> notes)
        {
            var node = Child(row, "applies_to") as YamlSequenceNode;
            if (node == null || !SameList(node, old)) return false;
            int start = Index(node.Start);
            int end = ContentEnd(text, node);
            if (start < 0 || end <= start || end > text.Length) return false;
            edits.Add(new Edit(start, end, "[" + string.Join(", ", next) + "]"));
            notes.Add(path + ".applies_to [" + string.Join(", ", old) + "] (the old default) is now [" + string.Join(", ", next) + "]");
            return true;
        }

        private static void DropWeights(string text, YamlMappingNode drops, List<Edit> edits, List<string> notes)
        {
            if (!(Child(drops, "runes") is YamlMappingNode weights)) return;
            if (Child(weights, "awakening") is YamlSequenceNode awakening && Child(weights, "ascension") is YamlSequenceNode ascension
                && SameNumbers(awakening, OldAwakeningWeights) && SameNumbers(ascension, OldAscensionWeights))
            {
                if (RemovePair(text, weights, "awakening", edits))
                {
                    edits.Add(new Edit(Index(ascension.Start), ContentEnd(text, ascension), Numbers(NewAscensionWeights)));
                    notes.Add("drops.runes.awakening and drops.runes.ascension (the old defaults) are now the merged Ascension weights");
                }
            }
            if (Child(weights, "recasting") is YamlSequenceNode recasting && Child(weights, "honing") == null && SameNumbers(recasting, OldRecastingWeights))
            {
                edits.Add(new Edit(Index(recasting.Start), ContentEnd(text, recasting), Numbers(NewRecastingWeights)));
                notes.Add("drops.runes.recasting (the old default) is now the new default, which leaves room for Honing");
            }
        }

        // A boss or creature bonus that pays a folded rune pays its successor, with the owner's chance and amount.
        private static void Bonuses(YamlMappingNode drops, string key, List<Edit> edits, List<string> notes)
        {
            if (!(Child(drops, key) is YamlMappingNode owners)) return;
            foreach (var owner in owners.Children)
            {
                if (!(owner.Value is YamlMappingNode map) || !(Child(map, "bonus") is YamlSequenceNode rows)) continue;
                foreach (var row in rows.Children.OfType<YamlMappingNode>())
                {
                    if (!(Child(row, "rune") is YamlScalarNode rune)) continue;
                    var successor = LegacyRunes.SuccessorOf(rune.Value);
                    if (successor == null || rune.Style != YamlDotNet.Core.ScalarStyle.Plain) continue;
                    edits.Add(new Edit(Index(rune.Start), Index(rune.End), successor));
                    notes.Add("drops." + key + "." + ((owner.Key as YamlScalarNode)?.Value ?? "?") + " bonus '" + rune.Value + "' now pays '" + successor + "'");
                }
            }
        }

        // Removes "key: value" from a block mapping, whole lines only.
        private static bool RemovePair(string text, YamlMappingNode map, string key, List<Edit> edits, string? extendOverComments = null)
        {
            if (map.Style == YamlDotNet.Core.Events.MappingStyle.Flow) return false;
            foreach (var pair in map.Children)
            {
                if (!(pair.Key is YamlScalarNode k) || k.Value != key) continue;
                int start = LineStart(text, Index(k.Start));
                if (text.Substring(start, Index(k.Start) - start).Trim().Length != 0) return false;
                int end = LineEnd(text, Math.Max(ContentEnd(text, pair.Value), Index(k.End)));
                if (extendOverComments != null) start = CommentsAbove(text, start, extendOverComments);
                edits.Add(new Edit(start, end, ""));
                return true;
            }
            return false;
        }

        // Removes one block sequence item ("- ..."), whole lines only.
        private static bool RemoveLines(string text, YamlMappingNode row, List<Edit> edits)
        {
            int at = Index(row.Start);
            int start = LineStart(text, at);
            string before = text.Substring(start, at - start).Trim();
            if (before != "-") return false;
            int end = LineEnd(text, ContentEnd(text, row));
            edits.Add(new Edit(start, end, ""));
            return true;
        }

        // Comment lines directly above that mention the word, so the removed section leaves no orphaned heading.
        private static int CommentsAbove(string text, int start, string word)
        {
            while (start > 0)
            {
                int prev = LineStart(text, start - 1);
                string line = text.Substring(prev, start - prev).Trim();
                if (!line.StartsWith("#", StringComparison.Ordinal) || line.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0) break;
                start = prev;
            }
            return start;
        }

        private static string AddMarker(string text)
        {
            string line = MarkerPrefix + " (values still at the pre-0.2.0 defaults were updated; the original is the .pre-0.2.0.bak file)";
            var match = FormatLine.Match(text);
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            return match.Success
                ? text.Substring(0, match.Index + match.Length) + line + newline + text.Substring(match.Index + match.Length)
                : line + newline + text;
        }

        private static bool Overlap(List<Edit> edits)
        {
            var sorted = edits.OrderBy(e => e.Start).ToList();
            for (int i = 1; i < sorted.Count; i++)
                if (sorted[i].Start < sorted[i - 1].End) return true;
            return false;
        }

        // The end of what a node spells out. YamlDotNet 13 gives a collection node no end mark (its End equals its
        // Start), so a flow collection ends at its matching bracket and a block collection at its last child.
        private static int ContentEnd(string text, YamlNode node)
        {
            switch (node)
            {
                case YamlSequenceNode seq when seq.Style == YamlDotNet.Core.Events.SequenceStyle.Flow:
                    return FlowEnd(text, Index(seq.Start));
                case YamlMappingNode map when map.Style == YamlDotNet.Core.Events.MappingStyle.Flow:
                    return FlowEnd(text, Index(map.Start));
                case YamlSequenceNode seq when seq.Children.Count > 0:
                    return seq.Children.Max(c => ContentEnd(text, c));
                case YamlMappingNode map when map.Children.Count > 0:
                    return map.Children.Max(p => Math.Max(ContentEnd(text, p.Key), ContentEnd(text, p.Value)));
                default:
                    return Index(node.End);
            }
        }

        // Just past the bracket that closes the flow collection opening at start; -1 when it is not found.
        private static int FlowEnd(string text, int start)
        {
            if (start < 0 || start >= text.Length || (text[start] != '[' && text[start] != '{')) return -1;
            int depth = 0;
            char quote = '\0';
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (quote != '\0')
                {
                    if (c == quote && !(quote == '"' && text[i - 1] == '\\')) quote = '\0';
                    continue;
                }
                if (c == '"' || c == '\'') quote = c;
                else if (c == '[' || c == '{') depth++;
                else if ((c == ']' || c == '}') && --depth == 0) return i + 1;
            }
            return -1;
        }

        private static int LineStart(string text, int index)
        {
            int i = Math.Min(index, text.Length);
            while (i > 0 && text[i - 1] != '\n') i--;
            return i;
        }

        private static int LineEnd(string text, int index)
        {
            int i = Math.Max(0, Math.Min(index, text.Length));
            while (i < text.Length && text[i] != '\n') i++;
            return i < text.Length ? i + 1 : i;
        }

        private static int Index(YamlDotNet.Core.Mark mark) => (int)mark.Index;

        private static string Numbers(double[] values) =>
            "[" + string.Join(", ", values.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";

        private static bool SameList(YamlNode? node, string[] expected) =>
            node is YamlSequenceNode seq && seq.Children.Count == expected.Length
            && seq.Children.Select(n => (n as YamlScalarNode)?.Value).SequenceEqual(expected);

        private static bool SameNumbers(YamlSequenceNode seq, double[] expected)
        {
            if (seq.Children.Count != expected.Length) return false;
            for (int i = 0; i < expected.Length; i++)
            {
                if (!(seq.Children[i] is YamlScalarNode s) || !double.TryParse(s.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v != expected[i])
                    return false;
            }
            return true;
        }

        private static YamlNode? Child(YamlMappingNode map, string key) =>
            map.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : null;

        private static string? Scalar(YamlMappingNode map, string key) => (Child(map, key) as YamlScalarNode)?.Value;

        private readonly struct Edit
        {
            public Edit(int start, int end, string text)
            {
                Start = start;
                End = end;
                Text = text;
            }

            public int Start { get; }
            public int End { get; }
            public string Text { get; }
        }
    }
}
