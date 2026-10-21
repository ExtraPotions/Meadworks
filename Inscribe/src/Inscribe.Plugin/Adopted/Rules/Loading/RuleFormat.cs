// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the current
// format is per family.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>Checks each family's schema. Legacy format-3 inscription layers convert in memory before merging;
    /// older unsupported families keep the existing backup/skip behavior.</summary>
    internal static class RuleFormat
    {
        public const string Key = "format";

        private static readonly Regex Line = new Regex(@"^format[ \t]*:[ \t]*['""]?(\d+)['""]?[ \t]*(#[^\r\n]*)?\r?$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        /// <summary>The root <c>format:</c> value of a text, or 0 when it has none (format 1).</summary>
        public static int Of(string text)
        {
            Match match = Line.Match(text);
            return match.Success && Numbers.TryInt(match.Groups[1].Value, out int format) ? format : 0;
        }

        public static bool IsCurrent(string text, int current) => Of(text) == current;

        // Legacy inscriptions are converted in memory before merging; economy retains its own version.
        public static bool IsReadable(string text, int current) => IsCurrent(text, current) || current == 4 && Of(text) == 3;

        /// <summary>The texts in the current format; every other one is logged and left out.</summary>
        public static List<SourceText> KeepCurrent(IEnumerable<SourceText> files, string origin, int current)
        {
            List<SourceText> kept = new List<SourceText>();
            foreach (SourceText file in files)
            {
                if (IsReadable(file.Text, current))
                {
                    kept.Add(file);
                }
                else
                {
                    Log.Warn($"{file.Name} from {origin} is {Describe(file.Text, current)}: skipped (this version reads format {current})");
                }
            }
            return kept;
        }

        /// <summary>"format 1 (an older Inscribe)" or "format 4 (a newer Inscribe)".</summary>
        public static string Describe(string text, int current)
        {
            int format = Of(text);
            return format > current ? $"format {format} (a newer Inscribe)" : $"format {Math.Max(format, 1)} (an older Inscribe)";
        }

        /// <summary>
        /// Renames an old main file to <c>&lt;name&gt;.v&lt;format&gt;.bak</c> (<c>.v2.2.bak</c> and up when taken) and
        /// writes the default text in its place. The new text, or null when the file could not be moved (then skipped).
        /// </summary>
        public static string? ReplaceOldMain(string path, string oldText, string defaultText)
        {
            int old = Math.Max(Of(oldText), 1);
            try
            {
                string backup = FreeBackupPath(path, old);
                File.Move(path, backup);
                File.WriteAllText(path, defaultText, new System.Text.UTF8Encoding(false));
                Log.Warn($"{Path.GetFileName(path)} was written for format {old} by an older Inscribe: renamed to "
                    + $"{Path.GetFileName(backup)} and the new default written in its place (copy your changes over by hand)");
                return defaultText;
            }
            catch (Exception e)
            {
                Log.Error($"{Path.GetFileName(path)} is format {old} and could not be replaced ({e.Message}): skipped");
                return null;
            }
        }

        private static string FreeBackupPath(string path, int format)
        {
            string stem = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path));
            string candidate = $"{stem}.v{format}.bak";
            for (int n = 2; File.Exists(candidate); n++)
            {
                candidate = $"{stem}.v{format}.{n}.bak";
            }
            return candidate;
        }
    }
}
