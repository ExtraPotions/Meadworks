// Shepherd - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the scalar subset of YAML the converter needs from EliteCreaturesReborn's rule file (mappings by
// indentation, inline [a, b] lists). The full rule-file parser is B02b; list items ("- match:") are skipped here.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Shepherd.Conversion
{
    public static class SimpleYaml
    {
        /// <summary>Every scalar under a mapping path, keyed by its dotted path (<c>loot.mode</c>); blocks and list items have no entry.</summary>
        public static Dictionary<string, string> Read(string? text)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var stack = new List<(int Indent, string Key)>();
            var skipBelow = -1;
            foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var line = StripComment(raw);
                if (line.Trim().Length == 0) continue;
                var indent = line.Length - line.TrimStart(' ').Length;
                var body = line.Trim();
                if (skipBelow >= 0)
                {
                    if (indent > skipBelow) continue;
                    skipBelow = -1;
                }
                while (stack.Count > 0 && stack[stack.Count - 1].Indent >= indent) stack.RemoveAt(stack.Count - 1);
                if (body.StartsWith("-", StringComparison.Ordinal))
                {
                    skipBelow = indent;
                    continue;
                }
                var colon = body.IndexOf(':');
                if (colon <= 0) continue;
                var key = body.Substring(0, colon).Trim();
                var value = Unquote(body.Substring(colon + 1).Trim());
                var path = string.Join(".", stack.Select(s => s.Key).Concat(new[] { key }));
                if (value.Length == 0) stack.Add((indent, key));
                else result[path] = value;
            }
            return result;
        }

        /// <summary>The items of an inline list <c>[a, b]</c>; a bare scalar is a one-item list.</summary>
        public static IReadOnlyList<string> List(string? value)
        {
            var v = (value ?? "").Trim();
            if (v.StartsWith("[", StringComparison.Ordinal) && v.EndsWith("]", StringComparison.Ordinal)) v = v.Substring(1, v.Length - 2);
            return v.Split(',').Select(p => Unquote(p.Trim())).Where(p => p.Length > 0).ToList();
        }

        private static string StripComment(string line)
        {
            var quote = '\0';
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                if (c == '\'' || c == '"') quote = c;
                else if (c == '#' && (i == 0 || line[i - 1] == ' ')) return line.Substring(0, i);
            }
            return line;
        }

        private static string Unquote(string v) =>
            v.Length >= 2 && (v[0] == '\'' || v[0] == '"') && v[v.Length - 1] == v[0] ? v.Substring(1, v.Length - 2) : v;
    }
}

