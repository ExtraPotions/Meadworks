// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the small YAML subset of the kill requirement and power definition files (nested maps by
// indentation, scalars, inline maps in braces, comments). Anything else is reported, never guessed.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Saga.Conversion
{
    /// <summary>A YAML node: a scalar, or a map whose entries keep file order.</summary>
    public sealed class YamlNode
    {
        private YamlNode(string? scalar, List<YamlEntry>? entries)
        {
            Scalar = scalar;
            Entries = entries ?? new List<YamlEntry>();
        }

        public static YamlNode Text(string value) => new YamlNode(value, null);
        public static YamlNode Map() => new YamlNode(null, new List<YamlEntry>());

        /// <summary>The text of a scalar; null for a map.</summary>
        public string? Scalar { get; }
        public bool IsMap => Scalar == null;
        public List<YamlEntry> Entries { get; }
    }

    public sealed class YamlEntry
    {
        public YamlEntry(string key, YamlNode value, int line)
        {
            Key = key;
            Value = value;
            Line = line;
        }

        public string Key { get; }
        public YamlNode Value { get; }

        /// <summary>1-based line number, for messages.</summary>
        public int Line { get; }
    }

    public static class MiniYaml
    {
        public static string Merge(IEnumerable<string> layers,out List<string> errors) {
            errors=new List<string>();var result=YamlNode.Map();foreach(var layer in layers)MergeInto(result,Parse(layer,errors));
            var output=new System.Text.StringBuilder();Write(result,0,output);return output.ToString();
        }
        static void MergeInto(YamlNode target,YamlNode source){foreach(var entry in source.Entries){var prior=target.Entries.FirstOrDefault(e=>e.Key==entry.Key);if(prior!=null&&prior.Value.IsMap&&entry.Value.IsMap)MergeInto(prior.Value,entry.Value);else {target.Entries.RemoveAll(e=>e.Key==entry.Key);target.Entries.Add(entry);}}}
        static void Write(YamlNode node,int indent,System.Text.StringBuilder text){foreach(var entry in node.Entries){text.Append(' ',indent).Append(entry.Key).Append(':');if(entry.Value.IsMap){text.Append('\n');Write(entry.Value,indent+2,text);}else text.Append(' ').Append(entry.Value.Scalar).Append('\n');}}
        /// <summary>Parses the subset; problems are added to <paramref name="errors"/> with their line and the line is skipped.</summary>
        public static YamlNode Parse(string? text, List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            var root = YamlNode.Map();
            var stack = new List<(int Indent, YamlNode Map)> { (-1, root) };
            var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            YamlEntry? open = null;
            for (var i = 0; i < lines.Length; i++)
            {
                var raw = StripComment(lines[i]);
                if (raw.Trim().Length == 0) continue;
                if (raw.Contains('\t')) { errors.Add("line " + (i + 1) + ": tabs are not allowed"); continue; }
                var indent = raw.Length - raw.TrimStart(' ').Length;
                var line = raw.Trim();
                if (open != null && indent > stack[stack.Count - 1].Indent)
                {
                    stack.Add((indent, open.Value));
                    open = null;
                }
                else
                {
                    open = null;
                    while (stack.Count > 1 && indent < stack[stack.Count - 1].Indent) stack.RemoveAt(stack.Count - 1);
                    if (stack.Count > 1 && indent != stack[stack.Count - 1].Indent)
                    {
                        errors.Add("line " + (i + 1) + ": unexpected indentation");
                        continue;
                    }
                }
                var colon = line.IndexOf(':');
                if (colon <= 0 || line.StartsWith("-", StringComparison.Ordinal))
                {
                    errors.Add("line " + (i + 1) + ": expected 'key: value'");
                    continue;
                }
                var key = line.Substring(0, colon).Trim();
                var rest = line.Substring(colon + 1).Trim();
                YamlNode value;
                if (rest.Length == 0) value = YamlNode.Map();
                else if (rest.StartsWith("{", StringComparison.Ordinal))
                {
                    if (!TryInline(rest, out value))
                    {
                        errors.Add("line " + (i + 1) + ": unreadable inline map");
                        continue;
                    }
                }
                else value = YamlNode.Text(Unquote(rest));
                var entry = new YamlEntry(key, value, i + 1);
                stack[stack.Count - 1].Map.Entries.Add(entry);
                if (rest.Length == 0) open = entry;
            }
            return root;
        }

        private static string StripComment(string line)
        {
            var quote = false;
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == '\'' || line[i] == '"') quote = !quote;
                if (!quote && line[i] == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1]))) return line.Substring(0, i);
            }
            return line;
        }

        private static string Unquote(string text) =>
            text.Length >= 2 && (text[0] == '"' && text[text.Length - 1] == '"' || text[0] == '\'' && text[text.Length - 1] == '\'')
                ? text.Substring(1, text.Length - 2) : text;

        private static bool TryInline(string text, out YamlNode map)
        {
            map = YamlNode.Map();
            if (!text.EndsWith("}", StringComparison.Ordinal)) return false;
            var body = text.Substring(1, text.Length - 2).Trim();
            if (body.Length == 0) return true;
            foreach (var part in body.Split(','))
            {
                var colon = part.IndexOf(':');
                if (colon <= 0) return false;
                var key = part.Substring(0, colon).Trim();
                var value = Unquote(part.Substring(colon + 1).Trim());
                if (key.Length == 0 || value.Length == 0) return false;
                map.Entries.Add(new YamlEntry(key, YamlNode.Text(value), 0));
            }
            return true;
        }

        /// <summary>Plain text of one level of a map, for diagnostics: <c>key</c> list.</summary>
        public static string Keys(YamlNode map) => string.Join(", ", map.Entries.Select(e => e.Key));
    }
}
