// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the small YAML subset Delve's data files use (design 4: Delve.Rooms.yml, Delve.Dungeons.yml,
// Delve.Sites.yml, Delve.Bosses.yml). Delve.Core stays dependency-free, so no YAML library is bundled.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Delve.Data
{
    /// <summary>One entry under a top-level section: <c>name: value</c> or <c>name: {key: value, ...}</c>.</summary>
    public sealed class YamlEntry
    {
        internal YamlEntry(int line, string name, string? scalar, IReadOnlyDictionary<string, string>? map)
        {
            Line = line;
            Name = name;
            Scalar = scalar;
            Map = map;
        }

        public int Line { get; }
        public string Name { get; }

        /// <summary>The plain value; null for a flow map.</summary>
        public string? Scalar { get; }

        /// <summary>The flow map's keys and values; null for a plain value.</summary>
        public IReadOnlyDictionary<string, string>? Map { get; }
    }

    /// <summary>
    /// Reads <c>section:</c> lines at column 0 and indented <c>name: value</c> or <c>name: {key: value}</c> entries below
    /// them. <c>#</c> starts a comment outside quotes; blank lines are skipped. Anything else is reported with its line
    /// number and skipped, never thrown.
    /// </summary>
    public static class MiniYaml
    {
        public static IReadOnlyDictionary<string, IReadOnlyList<YamlEntry>> Read(string? text, List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            var sections = new Dictionary<string, IReadOnlyList<YamlEntry>>(StringComparer.Ordinal);
            List<YamlEntry>? current = null;
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int number = i + 1;
                var raw = StripComment(lines[i]);
                if (raw.Trim().Length == 0) continue;
                bool indented = raw[0] == ' ' || raw[0] == '\t';
                var line = raw.Trim();
                int colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    errors.Add("line " + number + ": expected 'name: value'");
                    continue;
                }
                var name = Unquote(line.Substring(0, colon).Trim());
                var value = line.Substring(colon + 1).Trim();
                if (!indented)
                {
                    if (value.Length > 0)
                    {
                        errors.Add("line " + number + ": a section '" + name + ":' takes no value");
                        current = null;
                        continue;
                    }
                    current = new List<YamlEntry>();
                    sections[name] = current;
                    continue;
                }
                if (current == null)
                {
                    errors.Add("line " + number + ": entry '" + name + "' outside a section");
                    continue;
                }
                if (value.StartsWith("{"))
                {
                    var map = FlowMap(value);
                    if (map == null) errors.Add("line " + number + ": '" + name + "' has a malformed {key: value} map");
                    else current.Add(new YamlEntry(number, name, null, map));
                    continue;
                }
                current.Add(new YamlEntry(number, name, Unquote(value), null));
            }
            return sections;
        }

        private static string StripComment(string line)
        {
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"' || line[i] == '\'') quoted = !quoted;
                else if (line[i] == '#' && !quoted && (i == 0 || char.IsWhiteSpace(line[i - 1]))) return line.Substring(0, i);
            }
            return line;
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[value.Length - 1] == value[0])
                return value.Substring(1, value.Length - 2);
            return value;
        }

        private static IReadOnlyDictionary<string, string>? FlowMap(string value)
        {
            if (!value.EndsWith("}")) return null;
            var inner = value.Substring(1, value.Length - 2).Trim();
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (inner.Length == 0) return map;
            foreach (var part in inner.Split(','))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0) return null;
                var key = Unquote(part.Substring(0, colon).Trim());
                var v = Unquote(part.Substring(colon + 1).Trim());
                if (key.Length == 0 || map.ContainsKey(key)) return null;
                map[key] = v;
            }
            return map;
        }
    }
}
