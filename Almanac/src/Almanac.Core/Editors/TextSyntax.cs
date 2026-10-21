// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behavior reference only (ConfigDrawers 1.1.3 code editor: highlighting and live JSON/YAML validation); no source is copied.
using System;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Almanac.Editors
{
    public enum SyntaxKind { Plain, Cfg, Yaml, Json }

    public sealed class SyntaxIssue
    {
        public SyntaxIssue(int line, string message) { Line = line; Message = message; }
        /// <summary>1-based line.</summary>
        public int Line { get; }
        public string Message { get; }
        public override string ToString() => "Line " + Line + ": " + Message;
        internal sealed class Failure : Exception { public Failure(SyntaxIssue issue) { Issue = issue; } public SyntaxIssue Issue { get; } }
    }

    /// <summary>
    /// Coloring and structural checks for the text editors. <see cref="Highlight"/> only adds TextMeshPro color tags and
    /// escapes '&lt;', so the visible characters (and therefore line wrapping and caret positions) equal the input.
    /// </summary>
    public static class TextSyntax
    {
        public const string KeyColor = "#E8C27A", SectionColor = "#F0A050", StringColor = "#B8D88A", NumberColor = "#8EC4E8", CommentColor = "#8A8070", MarkColor = "#C8B89A";
        /// <summary>Longer texts are shown uncolored; rich-text layout of very large files would cost frames.</summary>
        public const int HighlightLimit = 60000;

        public static SyntaxKind Detect(string? fileName, string? text)
        {
            string name = (fileName ?? "").ToLowerInvariant();
            if (name.EndsWith(".json")) return SyntaxKind.Json;
            if (name.EndsWith(".yml") || name.EndsWith(".yaml")) return SyntaxKind.Yaml;
            if (name.EndsWith(".cfg")) return SyntaxKind.Cfg;
            string t = (text ?? "").Trim();
            if (t.Length >= 2 && ((t[0] == '{' && t[t.Length - 1] == '}') || (t[0] == '[' && t[t.Length - 1] == ']'))) return SyntaxKind.Json;
            if (t.IndexOf('\n') >= 0)
                foreach (var raw in t.Split('\n'))
                {
                    var line = raw.TrimStart();
                    if (line.StartsWith("- ") || line == "-" || line == "---" || YamlKeyEnd(line) > 0) return SyntaxKind.Yaml;
                }
            return SyntaxKind.Plain;
        }

        public static string Highlight(string? text, SyntaxKind kind)
        {
            text ??= "";
            if (kind == SyntaxKind.Plain || text.Length > HighlightLimit) return Escape(text);
            var b = new StringBuilder(text.Length * 2);
            if (kind == SyntaxKind.Json) { Json(text, b); return b.ToString(); }
            int start = 0;
            while (start <= text.Length)
            {
                int end = text.IndexOf('\n', start);
                if (end < 0) end = text.Length;
                string line = text.Substring(start, end - start);
                if (kind == SyntaxKind.Cfg) CfgLine(line, b); else YamlLine(line, b);
                if (end < text.Length) b.Append('\n');
                start = end + 1;
            }
            return b.ToString();
        }

        /// <summary>The first structural problem, or null. <see cref="SyntaxIssue.Message"/> is a fixed English phrase the UI translates.</summary>
        public static SyntaxIssue? Find(string? text, SyntaxKind kind)
        {
            text ??= "";
            try
            {
                if (kind == SyntaxKind.Json) JsonCheck.Run(text);
                else if (kind == SyntaxKind.Yaml) YamlCheck(text);
                return null;
            }
            catch (SyntaxIssue.Failure failure) { return failure.Issue; }
        }

        /// <summary>English form: empty when valid, otherwise "Line N: reason".</summary>
        public static string Check(string? text, SyntaxKind kind) => Find(text, kind)?.ToString() ?? "";

        public static string Escape(string text) => text.IndexOf('<') < 0 ? text : text.Replace("<", "<noparse><</noparse>");

        static void Paint(StringBuilder b, string color, string text)
        {
            if (text.Length == 0) return;
            b.Append("<color=").Append(color).Append('>').Append(Escape(text)).Append("</color>");
        }

        static void Value(StringBuilder b, string value)
        {
            string core = value.Trim();
            if (core.Length == 0) { b.Append(Escape(value)); return; }
            int lead = value.IndexOf(core[0]);
            b.Append(value, 0, lead);
            Paint(b, Scalar(core), core);
            b.Append(value, lead + core.Length, value.Length - lead - core.Length);
        }

        static string Scalar(string core)
        {
            if (core.Length > 0 && (core[0] == '"' || core[0] == '\'')) return StringColor;
            string lower = core.ToLowerInvariant();
            if (lower == "true" || lower == "false" || lower == "null" || lower == "~" || double.TryParse(core, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return NumberColor;
            return StringColor;
        }

        static void CfgLine(string line, StringBuilder b)
        {
            string t = line.TrimStart();
            if (t.StartsWith("#") || t.StartsWith(";")) { Paint(b, CommentColor, line); return; }
            if (t.StartsWith("[") && t.TrimEnd().EndsWith("]")) { Paint(b, SectionColor, line); return; }
            int eq = line.IndexOf('=');
            if (eq < 0) { b.Append(Escape(line)); return; }
            Paint(b, KeyColor, line.Substring(0, eq));
            Paint(b, MarkColor, "=");
            Value(b, line.Substring(eq + 1));
        }

        /// <summary>Index of the ':' ending a plain or quoted mapping key, or -1.</summary>
        static int YamlKeyEnd(string line)
        {
            int i = 0;
            if (line.Length > 0 && (line[0] == '"' || line[0] == '\''))
            {
                int close = line.IndexOf(line[0], 1);
                if (close < 0) return -1;
                i = close + 1;
                return i < line.Length && line[i] == ':' && (i + 1 == line.Length || line[i + 1] == ' ') ? i : -1;
            }
            for (; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '#' || c == '"' || c == '\'' || c == '{' || c == '[') return -1;
                if (c == ':' && (i + 1 == line.Length || line[i + 1] == ' ')) return i > 0 ? i : -1;
            }
            return -1;
        }

        /// <summary>Start of an unquoted '#' comment (at line start or after whitespace), or -1.</summary>
        static int CommentStart(string line)
        {
            char quote = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                if (c == '"' || c == '\'') quote = c;
                else if (c == '#' && (i == 0 || line[i - 1] == ' ' || line[i - 1] == '\t')) return i;
            }
            return -1;
        }

        static void YamlLine(string line, StringBuilder b)
        {
            int hash = CommentStart(line);
            string body = hash >= 0 ? line.Substring(0, hash) : line;
            string comment = hash >= 0 ? line.Substring(hash) : "";
            int indent = 0;
            while (indent < body.Length && body[indent] == ' ') indent++;
            b.Append(body, 0, indent);
            string rest = body.Substring(indent);
            if (rest.TrimEnd() == "---" || rest.TrimEnd() == "...") { Paint(b, MarkColor, rest); Paint(b, CommentColor, comment); return; }
            while (rest.StartsWith("- ") || rest == "-")
            {
                Paint(b, MarkColor, "-");
                int n = 1;
                while (n < rest.Length && rest[n] == ' ') n++;
                b.Append(rest, 1, n - 1);
                rest = rest.Substring(n);
            }
            int colon = YamlKeyEnd(rest);
            if (colon > 0)
            {
                Paint(b, KeyColor, rest.Substring(0, colon));
                Paint(b, MarkColor, ":");
                rest = rest.Substring(colon + 1);
            }
            if (rest.TrimStart().StartsWith("&") || rest.TrimStart().StartsWith("*")) Paint(b, SectionColor, rest);
            else Value(b, rest);
            Paint(b, CommentColor, comment);
        }

        static void Json(string text, StringBuilder b)
        {
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '"')
                {
                    int j = i + 1;
                    while (j < text.Length && text[j] != '"' && text[j] != '\n') j += text[j] == '\\' ? 2 : 1;
                    j = Math.Min(j + 1, text.Length);
                    int k = j;
                    while (k < text.Length && (text[k] == ' ' || text[k] == '\t')) k++;
                    Paint(b, k < text.Length && text[k] == ':' ? KeyColor : StringColor, text.Substring(i, j - i));
                    i = j;
                }
                else if (c == '-' || char.IsDigit(c) || char.IsLetter(c))
                {
                    int j = i;
                    while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] == '.' || text[j] == '-' || text[j] == '+')) j++;
                    Paint(b, NumberColor, text.Substring(i, j - i));
                    i = j;
                }
                else if (c == '{' || c == '}' || c == '[' || c == ']' || c == ':' || c == ',') { Paint(b, MarkColor, c.ToString()); i++; }
                else { b.Append(c == '<' ? "<noparse><</noparse>" : c.ToString()); i++; }
            }
        }

        static string YamlCheck(string text)
        {
            var lines = text.Split('\n');
            int depth = 0, depthLine = 0;
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].TrimEnd('\r');
                int indent = 0;
                while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t'))
                {
                    if (line[indent] == '\t') return Line(n, "Tab used for indentation; use spaces");
                    indent++;
                }
                char quote = '\0';
                for (int i = indent; i < line.Length; i++)
                {
                    char c = line[i];
                    if (quote == '\'') { if (c == '\'') { if (i + 1 < line.Length && line[i + 1] == '\'') i++; else quote = '\0'; } continue; }
                    if (quote == '"') { if (c == '\\') i++; else if (c == '"') quote = '\0'; continue; }
                    if (c == '#' && (i == indent || line[i - 1] == ' ')) break;
                    // A quote only opens a quoted scalar at the start of a value, not inside plain text such as don't.
                    if ((c == '"' || c == '\'') && (i == indent || " :-[{,".IndexOf(line[i - 1]) >= 0)) quote = c;
                    else if (c == '[' || c == '{') { if (depth == 0) depthLine = n; depth++; }
                    else if (c == ']' || c == '}') { if (--depth < 0) return Line(n, "Closing bracket without an opening one"); }
                }
                if (quote != '\0') return Line(n, "Unclosed quote");
            }
            return depth > 0 ? Line(depthLine, "Unclosed bracket") : "";
        }

        static string Line(int index, string message) => throw new SyntaxIssue.Failure(new SyntaxIssue(index + 1, message));

        /// <summary>A strict RFC 8259 reader that reports the first error by line.</summary>
        sealed class JsonCheck
        {
            readonly string s; int i;
            JsonCheck(string text) { s = text; }

            public static void Run(string text)
            {
                var check = new JsonCheck(text);
                check.Space(); check.Value(0); check.Space();
                if (check.i < text.Length) check.Fail("Unexpected text after the end");
            }

            void Fail(string message)
            {
                int line = 0;
                for (int k = 0; k < Math.Min(i, s.Length); k++) if (s[k] == '\n') line++;
                Line(line, message);
            }

            void Space() { while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++; }

            void Value(int depth)
            {
                if (depth > 256) Fail("Nested too deeply");
                if (i >= s.Length) Fail("Value expected");
                char c = s[i];
                if (c == '{') Container('}', depth, true);
                else if (c == '[') Container(']', depth, false);
                else if (c == '"') String();
                else if (c == '-' || char.IsDigit(c)) Number();
                else if (!Word("true") && !Word("false") && !Word("null")) Fail("Unexpected character");
            }

            void Container(char close, int depth, bool members)
            {
                i++; Space();
                if (i < s.Length && s[i] == close) { i++; return; }
                while (true)
                {
                    if (members)
                    {
                        if (i >= s.Length || s[i] != '"') Fail("Quoted key expected");
                        String(); Space();
                        if (i >= s.Length || s[i] != ':') Fail("Colon expected after the key");
                        i++; Space();
                    }
                    Value(depth + 1); Space();
                    if (i < s.Length && s[i] == ',') { i++; Space(); continue; }
                    if (i < s.Length && s[i] == close) { i++; return; }
                    Fail("Comma or closing bracket expected");
                }
            }

            void String()
            {
                i++;
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return;
                    if (c == '\n') { i--; Fail("Unclosed string"); }
                    if (c == '\\')
                    {
                        if (i >= s.Length) break;
                        char e = s[i++];
                        if (e == 'u') { for (int k = 0; k < 4; k++, i++) if (i >= s.Length || !Uri.IsHexDigit(s[i])) Fail("Invalid escape"); }
                        else if ("\"\\/bfnrt".IndexOf(e) < 0) Fail("Invalid escape");
                    }
                }
                Fail("Unclosed string");
            }

            void Number()
            {
                int start = i;
                if (s[i] == '-') i++;
                if (i < s.Length && s[i] == '0') i++;
                else if (i < s.Length && char.IsDigit(s[i])) while (i < s.Length && char.IsDigit(s[i])) i++;
                else Fail("Invalid number");
                if (i < s.Length && s[i] == '.') { i++; if (i >= s.Length || !char.IsDigit(s[i])) Fail("Invalid number"); while (i < s.Length && char.IsDigit(s[i])) i++; }
                if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
                {
                    i++; if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                    if (i >= s.Length || !char.IsDigit(s[i])) Fail("Invalid number");
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                }
                if (i == start) Fail("Invalid number");
            }

            bool Word(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
                i += word.Length; return true;
            }
        }
    }
}
