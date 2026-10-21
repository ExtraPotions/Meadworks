// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExtraPotions.Almanac.Files
{
    /// <summary>One listed setting in the text editor: serialized value plus an optional comment shown above it.</summary>
    public sealed class CfgRow
    {
        public CfgRow(string section, string key, string value, string? note = null)
        { Section = section; Key = key; Value = value; Note = note; }
        public string Section { get; }
        public string Key { get; }
        public string Value { get; }
        public string? Note { get; }
    }

    public sealed class CfgParse
    {
        internal CfgParse(IReadOnlyList<CfgRow> values, IReadOnlyList<string> errors) { Values = values; Errors = errors; }
        public IReadOnlyList<CfgRow> Values { get; }
        public IReadOnlyList<string> Errors { get; }
    }

    public sealed class CfgChange
    {
        internal CfgChange(string section, string key, string before, string value) { Section = section; Key = key; Before = before; Value = value; }
        public string Section { get; }
        public string Key { get; }
        public string Before { get; }
        public string Value { get; }
    }

    public sealed class CfgPlan
    {
        internal CfgPlan(IReadOnlyList<CfgChange> changes, IReadOnlyList<string> errors) { Changes = changes; Errors = errors; }
        public IReadOnlyList<CfgChange> Changes { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool CanApply => Errors.Count == 0 && Changes.Count > 0;
    }

    /// <summary>
    /// BepInEx-style text view of the settings Almanac already shows for one file. The text is never written to disk:
    /// the plan's changes go through each entry's owner writer, so locks, hidden settings and validation still apply.
    /// </summary>
    public static class CfgText
    {
        public static string Render(IEnumerable<CfgRow> rows)
        {
            var builder = new StringBuilder();
            bool first = true;
            foreach (var section in rows.GroupBy(row => row.Section, StringComparer.Ordinal))
            {
                if (!first) builder.Append('\n');
                first = false;
                builder.Append('[').Append(section.Key).Append("]\n");
                foreach (var row in section)
                {
                    if (!string.IsNullOrEmpty(row.Note))
                        foreach (var line in row.Note!.Split('\n')) builder.Append("# ").Append(line.TrimEnd('\r')).Append('\n');
                    builder.Append(row.Key).Append(" = ").Append(row.Value).Append('\n');
                }
            }
            return builder.ToString();
        }

        public static CfgParse Parse(string text)
        {
            var values = new List<CfgRow>();
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string? section = null;
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                string at = "Line " + (i + 1) + ": ";
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                { section = line.Substring(1, line.Length - 2).Trim(); continue; }
                int split = line.IndexOf('=');
                if (split < 0) { errors.Add(at + "expected Key = Value."); continue; }
                string key = line.Substring(0, split).Trim(), value = line.Substring(split + 1).Trim();
                if (key.Length == 0) { errors.Add(at + "missing key."); continue; }
                if (section == null) { errors.Add(at + "setting outside a [Section]."); continue; }
                if (!seen.Add(section + "\n" + key)) { errors.Add(at + "duplicate setting " + section + " / " + key + "."); continue; }
                values.Add(new CfgRow(section, key, value));
            }
            return new CfgParse(values.AsReadOnly(), errors.AsReadOnly());
        }

        public static CfgPlan Plan(IEnumerable<CfgRow> original, CfgParse edited)
        {
            var known = new Dictionary<string, CfgRow>(StringComparer.Ordinal);
            foreach (var row in original) known[row.Section + "\n" + row.Key] = row;
            var errors = new List<string>(edited.Errors);
            var changes = new List<CfgChange>();
            foreach (var row in edited.Values)
            {
                if (!known.TryGetValue(row.Section + "\n" + row.Key, out var current))
                { errors.Add("Unknown setting " + row.Section + " / " + row.Key + "; only listed settings can be edited."); continue; }
                if (!string.Equals(current.Value, row.Value, StringComparison.Ordinal))
                    changes.Add(new CfgChange(row.Section, row.Key, current.Value, row.Value));
            }
            return new CfgPlan(changes.AsReadOnly(), errors.AsReadOnly());
        }
    }
}
