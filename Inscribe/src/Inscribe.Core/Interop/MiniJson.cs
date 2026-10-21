// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a strict, small JSON reader for capability payloads (Inscribe.Core references nothing beyond
// netstandard, so no System.Text.Json). Objects become ordered dictionaries, arrays lists, numbers double.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Inscribe.Interop
{
    public static class MiniJson
    {
        /// <summary>Parses one JSON value; <see cref="FormatException"/> on any syntax error or trailing text.</summary>
        public static object? Parse(string text)
        {
            if (text == null) throw new FormatException("No JSON text.");
            var i = 0;
            var value = Value(text, ref i, 0);
            Skip(text, ref i);
            if (i != text.Length) throw new FormatException("Unexpected text at " + i + ".");
            return value;
        }

        private static object? Value(string s, ref int i, int depth)
        {
            if (depth > 32) throw new FormatException("Nesting too deep.");
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end.");
            var c = s[i];
            if (c == '{') return Object(s, ref i, depth);
            if (c == '[') return Array(s, ref i, depth);
            if (c == '"') return String(s, ref i);
            if (c == '-' || (c >= '0' && c <= '9')) return Number(s, ref i);
            if (Literal(s, ref i, "true")) return true;
            if (Literal(s, ref i, "false")) return false;
            if (Literal(s, ref i, "null")) return null;
            throw new FormatException("Unexpected '" + c + "' at " + i + ".");
        }

        private static Dictionary<string, object?> Object(string s, ref int i, int depth)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            i++;
            Skip(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }
            while (true)
            {
                Skip(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("Expected a key at " + i + ".");
                var key = String(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("Expected ':' at " + i + ".");
                i++;
                result[key] = Value(s, ref i, depth + 1);
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return result; }
                throw new FormatException("Expected ',' or '}' at " + i + ".");
            }
        }

        private static List<object?> Array(string s, ref int i, int depth)
        {
            var result = new List<object?>();
            i++;
            Skip(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }
            while (true)
            {
                result.Add(Value(s, ref i, depth + 1));
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return result; }
                throw new FormatException("Expected ',' or ']' at " + i + ".");
            }
        }

        private static string String(string s, ref int i)
        {
            var b = new StringBuilder();
            i++;
            while (i < s.Length)
            {
                var c = s[i++];
                if (c == '"') return b.ToString();
                if (c < ' ') throw new FormatException("Control character in a string.");
                if (c != '\\') { b.Append(c); continue; }
                if (i >= s.Length) break;
                var e = s[i++];
                switch (e)
                {
                    case '"': b.Append('"'); break;
                    case '\\': b.Append('\\'); break;
                    case '/': b.Append('/'); break;
                    case 'b': b.Append('\b'); break;
                    case 'f': b.Append('\f'); break;
                    case 'n': b.Append('\n'); break;
                    case 'r': b.Append('\r'); break;
                    case 't': b.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            throw new FormatException("Bad \\u escape.");
                        b.Append((char)code);
                        i += 4;
                        break;
                    default: throw new FormatException("Bad escape \\" + e + ".");
                }
            }
            throw new FormatException("Unterminated string.");
        }

        private static double Number(string s, ref int i)
        {
            var start = i;
            if (s[i] == '-') i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsInfinity(value))
                throw new FormatException("Bad number at " + start + ".");
            return value;
        }

        private static bool Literal(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }
    }
}
