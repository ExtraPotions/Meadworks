// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Delve.Dungeons.yml, the per dungeon type mode (design 4.3).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Data
{
    /// <summary><c>modes:</c> per generator prefab, <c>Off</c>, <c>Resources</c> or <c>Full</c>; unlisted types keep <see cref="DungeonModes.Default"/>.</summary>
    public sealed class DungeonsFile
    {
        public const string FileName = "Delve.Dungeons.yml";

        private DungeonsFile(IReadOnlyDictionary<string, DungeonMode> modes, IReadOnlyList<string> errors)
        {
            Modes = modes;
            Errors = errors;
        }

        public static DungeonsFile Empty { get; } = new DungeonsFile(new Dictionary<string, DungeonMode>(), Array.Empty<string>());

        /// <summary>The modes written in the file.</summary>
        public IReadOnlyDictionary<string, DungeonMode> Modes { get; }

        public IReadOnlyList<string> Errors { get; }

        public DungeonMode ModeOf(string generator) =>
            generator != null && Modes.TryGetValue(generator, out var mode) ? mode : DungeonModes.Default(generator!);

        public static DungeonsFile Parse(string? text)
        {
            var errors = new List<string>();
            var modes = new Dictionary<string, DungeonMode>(StringComparer.Ordinal);
            var sections = MiniYaml.Read(text, errors);
            if (sections.TryGetValue("modes", out var entries))
                foreach (var e in entries)
                {
                    if (e.Scalar != null && Enum.TryParse<DungeonMode>(e.Scalar, true, out var mode) && Enum.IsDefined(typeof(DungeonMode), mode)
                        && !int.TryParse(e.Scalar, out _))
                        modes[e.Name] = mode;
                    else
                        errors.Add("line " + e.Line + ": '" + e.Name + "' needs Off, Resources or Full; the default stays");
                }
            return new DungeonsFile(modes, errors);
        }

        /// <summary>
        /// The written modes plus every generator type found in the game that the file does not list yet, at Delve's default
        /// (MoreDungeonResets behavior: the file lists every dungeon, modded ones included). Null when nothing is missing.
        /// </summary>
        public static Dictionary<string, DungeonMode>? WithMissing(IReadOnlyDictionary<string, DungeonMode> written, IEnumerable<string> generators)
        {
            var all = written.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            int before = all.Count;
            foreach (var g in generators)
                if (!string.IsNullOrEmpty(g) && !all.ContainsKey(g)) all[g] = DungeonModes.Default(g);
            return all.Count == before ? null : all;
        }

        /// <summary>The file text for these modes, sorted by generator, with a header comment.</summary>
        public static string Write(IReadOnlyDictionary<string, DungeonMode> modes)
        {
            var text = new StringBuilder();
            text.Append("# Delve dungeon modes: Off, Resources (partial regrow and boss return) or Full (wipe and regenerate).\n");
            text.Append("# Types not listed keep Delve's default (Resources where something regrows, Off otherwise).\n");
            text.Append("modes:\n");
            foreach (var pair in modes.OrderBy(p => p.Key, StringComparer.Ordinal))
                text.Append("  ").Append(pair.Key).Append(": ").Append(pair.Value).Append('\n');
            return text.ToString();
        }
    }
}
