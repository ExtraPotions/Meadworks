// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: reads TimeWeatherHud's markers file in its documented line format
// "<world>/<character>|type:<PinType>" or "pin:..." (design 9.2). No TimeWeatherHud code is used.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Cairn.Hud;

namespace ExtraPotions.Cairn.Conversion
{
    /// <summary>The marker choices of one world (by name, as the file keys it) and character.</summary>
    public sealed class MarkerSet
    {
        internal MarkerSet(string world, string character, MarkerChoices choices)
        {
            World = world;
            Character = character;
            Choices = choices;
        }

        public string World { get; }
        public string Character { get; }
        public MarkerChoices Choices { get; }
    }

    public sealed class MarkerImport
    {
        private MarkerImport(IReadOnlyList<MarkerSet> sets, IReadOnlyList<ImportNote> notes)
        {
            Sets = sets;
            Notes = notes;
        }

        /// <summary>Sets in file order of first appearance.</summary>
        public IReadOnlyList<MarkerSet> Sets { get; }
        public IReadOnlyList<ImportNote> Notes { get; }

        /// <summary>The set for a world name and character name (exact match); the plugin matches names to world ids at load.</summary>
        public MarkerSet? For(string world, string character) =>
            Sets.FirstOrDefault(s => string.Equals(s.World, world, StringComparison.Ordinal) && string.Equals(s.Character, character, StringComparison.Ordinal));

        /// <summary>Accepted sets in the source format, retaining empty choices and sets for other characters.</summary>
        public string Serialize() => string.Join("\n", Sets.SelectMany(s => s.Choices.Entries.Count == 0
            ? new[] { s.World + "/" + s.Character + "|" }
            : s.Choices.Entries.Select(e => s.World + "/" + s.Character + "|" + e)));

        public string Without(string world, string character) => new MarkerImport(Sets.Where(s =>
            !string.Equals(s.World, world, StringComparison.Ordinal) || !string.Equals(s.Character, character, StringComparison.Ordinal)).ToArray(), Array.Empty<ImportNote>()).Serialize();

        /// <summary>Newly accepted choices replace the same pair; pending choices for other pairs stay queued.</summary>
        public MarkerImport Merge(MarkerImport incoming) => new MarkerImport(Sets.Where(s => incoming.For(s.World, s.Character) == null)
            .Concat(incoming.Sets).ToArray(), Notes.Concat(incoming.Notes).ToArray());

        /// <summary>
        /// Every world and character in the file. A line with no entry records that the character has choices, so a set with
        /// only that line imports as empty (the player turned tombstones off), not as the default.
        /// </summary>
        public static MarkerImport Parse(string? text)
        {
            var order = new List<(string World, string Character)>();
            var entries = new Dictionary<(string, string), MarkerChoices>();
            var notes = new List<ImportNote>();
            var lineNumber = 0;
            foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                lineNumber++;
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var bar = line.IndexOf('|');
                var slash = bar < 0 ? -1 : line.LastIndexOf('/', bar);
                if (bar < 0 || slash <= 0 || slash == bar - 1)
                {
                    notes.Add(new ImportNote(ImportNoteKind.Fallback, "line " + lineNumber, "not \"<world>/<character>|<entry>\": " + line));
                    continue;
                }
                var key = (line.Substring(0, slash), line.Substring(slash + 1, bar - slash - 1));
                if (!entries.ContainsKey(key))
                {
                    entries[key] = MarkerChoices.Empty;
                    order.Add(key);
                }
                var entry = line.Substring(bar + 1).Trim();
                if (entry.Length == 0) continue;
                if (MarkerChoices.IsEntry(entry)) entries[key] = entries[key].With(entry);
                else notes.Add(new ImportNote(ImportNoteKind.Fallback, "line " + lineNumber, "unknown marker entry skipped: " + entry));
            }
            return new MarkerImport(order.Select(k => new MarkerSet(k.World, k.Character, entries[k])).ToList(), notes);
        }
    }
}
