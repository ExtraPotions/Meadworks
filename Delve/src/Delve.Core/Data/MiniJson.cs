// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the small JSON subset of Delve's sidecar (design: worlds_local/<world>.delve.json). Delve.Core
// references nothing beyond netstandard, so it carries its own reader and writer.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Delve.Data
{
    /// <summary>
    /// Objects become <c>IDictionary&lt;string, object?&gt;</c> (insertion order kept), arrays <c>IList&lt;object?&gt;</c>,
    /// numbers <c>double</c>, plus <c>string</c>, <c>bool</c> and null. Malformed text throws <see cref="FormatException"/>.
    /// </summary>
    public static class MiniJson
    {
        public static object? Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var reader = new Reader(text);
            reader.Space();
            var value = reader.Value(0);
            reader.Space();
            if (!reader.End) throw reader.Error("trailing text");
            return value;
        }

        public static string Write(object? value)
        {
            var text = new StringBuilder();
            WriteValue(text, value, 0);
            text.Append('\n');
            return text.ToString();
        }

        private static void WriteValue(StringBuilder text, object? value, int depth)
        {
            switch (value)
            {
                case null: text.Append("null"); break;
                case bool b: text.Append(b ? "true" : "false"); break;
                case string s: WriteString(text, s); break;
                case double d: text.Append(Number(d)); break;
                case float f: text.Append(Number(f)); break;
                case int i: text.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: text.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object?> map:
                    text.Append('{');
                    var first = true;
                    foreach (var pair in map)
                    {
                        text.Append(first ? "\n" : ",\n").Append(' ', (depth + 1) * 2);
                        first = false;
                        WriteString(text, pair.Key);
                        text.Append(": ");
                        WriteValue(text, pair.Value, depth + 1);
                    }
                    if (!first) text.Append('\n').Append(' ', depth * 2);
                    text.Append('}');
                    break;
                case System.Collections.IEnumerable list:
                    text.Append('[');
                    var any = false;
                    foreach (var item in list)
                    {
                        if (any) text.Append(", ");
                        any = true;
                        WriteValue(text, item, depth + 1);
                    }
                    text.Append(']');
                    break;
                default: throw new ArgumentException("Unsupported JSON value " + value.GetType().Name + ".", nameof(value));
            }
        }

        private static string Number(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) throw new ArgumentException("JSON numbers must be finite.");
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void WriteString(StringBuilder text, string s)
        {
            text.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c > 0x7e) text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else text.Append(c);
                        break;
                }
            }
            text.Append('"');
        }

        private sealed class Reader
        {
            private const int MaxDepth = 64;
            private readonly string s;
            private int i;

            public Reader(string text) => s = text;

            public bool End => i >= s.Length;

            public FormatException Error(string what) => new FormatException("JSON " + what + " at offset " + i + ".");

            public void Space()
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
            }

            public object? Value(int depth)
            {
                if (depth > MaxDepth) throw Error("nesting too deep");
                if (End) throw Error("value expected");
                char c = s[i];
                if (c == '{') return Object(depth);
                if (c == '[') return Array(depth);
                if (c == '"') return String();
                if (Word("true")) return true;
                if (Word("false")) return false;
                if (Word("null")) return null;
                if (c == '-' || (c >= '0' && c <= '9')) return Number();
                throw Error("value expected");
            }

            private bool Word(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
                i += word.Length;
                return true;
            }

            private double Number()
            {
                int start = i;
                if (s[i] == '-') i++;
                while (i < s.Length && "0123456789.eE+-".IndexOf(s[i]) >= 0) i++;
                if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) throw Error("bad number");
                return d;
            }

            private string String()
            {
                i++;
                var text = new StringBuilder();
                while (true)
                {
                    if (End) throw Error("unterminated string");
                    char c = s[i++];
                    if (c == '"') return text.ToString();
                    if (c != '\\')
                    {
                        text.Append(c);
                        continue;
                    }
                    if (End) throw Error("bad escape");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': text.Append('"'); break;
                        case '\\': text.Append('\\'); break;
                        case '/': text.Append('/'); break;
                        case 'b': text.Append('\b'); break;
                        case 'f': text.Append('\f'); break;
                        case 'n': text.Append('\n'); break;
                        case 'r': text.Append('\r'); break;
                        case 't': text.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)) throw Error("bad unicode escape");
                            text.Append((char)code);
                            i += 4;
                            break;
                        default: throw Error("bad escape");
                    }
                }
            }

            private IDictionary<string, object?> Object(int depth)
            {
                i++;
                var map = new Dictionary<string, object?>(StringComparer.Ordinal);
                Space();
                if (!End && s[i] == '}')
                {
                    i++;
                    return map;
                }
                while (true)
                {
                    Space();
                    if (End || s[i] != '"') throw Error("key expected");
                    var key = String();
                    Space();
                    if (End || s[i] != ':') throw Error("colon expected");
                    i++;
                    Space();
                    map[key] = Value(depth + 1);
                    Space();
                    if (End) throw Error("unterminated object");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return map; }
                    throw Error("comma or brace expected");
                }
            }

            private IList<object?> Array(int depth)
            {
                i++;
                var list = new List<object?>();
                Space();
                if (!End && s[i] == ']')
                {
                    i++;
                    return list;
                }
                while (true)
                {
                    Space();
                    list.Add(Value(depth + 1));
                    Space();
                    if (End) throw Error("unterminated array");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return list; }
                    throw Error("comma or bracket expected");
                }
            }
        }
    }
}
