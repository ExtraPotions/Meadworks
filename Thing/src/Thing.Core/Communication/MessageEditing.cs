// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExtraPotions.Thing.RemoteConfig;

namespace ExtraPotions.Thing.Communication
{
    public sealed class MessageEditRequest
    {
        public string Verb { get; }
        public string Action { get; }
        public string Version { get; }
        public int Index { get; }
        public int Minutes { get; }
        public string Text { get; }
        public bool Writes => Action != "get" && Action != "list";
        internal MessageEditRequest(string verb, string action, string version = "", int index = 0, int minutes = 0, string text = "")
        { Verb = verb; Action = action; Version = version; Index = index; Minutes = minutes; Text = text; }
    }

    // Scalar chunks travel inside the existing bounded string-array Operation; no transport or retry policy is added.
    public static class MessageEditing
    {
        public const int MaxEntries = 1000, ChunkBytes = 700;
        static readonly UTF8Encoding utf8 = new UTF8Encoding(false, true);
        public static bool Version(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');
        public static bool TextValid(string text, bool motd, bool empty = false) => text != null && text.Length <= 500
            && (empty || text.Trim().Length > 0) && (motd || text.IndexOfAny(new[] { '\r', '\n' }) < 0)
            && !text.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');

        public static IReadOnlyList<string> Pack(string text)
        {
            if (text == null || text.Length > 500) throw new ArgumentException("Message too long.");
            var encoded = Convert.ToBase64String(utf8.GetBytes(text));
            var parts = new List<string> { "--utf8" };
            for (var at = 0; at < encoded.Length; at += ChunkBytes) parts.Add(encoded.Substring(at, Math.Min(ChunkBytes, encoded.Length - at)));
            if (encoded.Length == 0) parts.Add("");
            return parts;
        }
        public static bool TryText(IReadOnlyList<string> args, int start, out string text)
        {
            text = "";
            if (start >= args.Count) return false;
            try
            {
                if (args[start] == "--utf8")
                {
                    if (args.Count - start < 2 || args.Count - start > 5) return false;
                    text = utf8.GetString(Convert.FromBase64String(string.Concat(args.Skip(start + 1))));
                }
                else text = string.Join(" ", args.Skip(start));
                return text.Length <= 500;
            }
            catch { return false; }
        }
        public static bool TryParse(IReadOnlyList<string> args, out MessageEditRequest request)
        {
            request = null!;
            if (args.Count == 0) return false;
            var verb = args[0].ToLowerInvariant();
            if (verb != "motd" && verb != "announce") return false;
            var action = args.Count > 1 ? args[1].ToLowerInvariant() : verb == "motd" ? "get" : "list";
            if (verb == "motd" && action == "get" && args.Count <= 2)
            { request = new MessageEditRequest(verb, action); return true; }
            if (verb == "announce" && (action == "list" || action == "get"))
            {
                if (action == "list" && args.Count <= 2) { request = new MessageEditRequest(verb, action); return true; }
                if (args.Count != 3 || !Number(args[2], 0, MaxEntries - 1, out var at)) return false;
                request = new MessageEditRequest(verb, action, index: at); return true;
            }
            if (args.Count < 3 || !Version(args[2])) return false;
            if (action == "clear" && args.Count == 3) { request = new MessageEditRequest(verb, action, args[2]); return true; }
            if (verb == "motd")
            {
                if (action != "set" || !TryText(args, 3, out var text) || !TextValid(text, true)) return false;
                request = new MessageEditRequest(verb, action, args[2], text: text.Trim()); return true;
            }
            if (action == "remove" && args.Count == 4 && Number(args[3], 0, MaxEntries - 1, out var remove))
            { request = new MessageEditRequest(verb, action, args[2], index: remove); return true; }
            var index = 0; var minuteAt = 3;
            if (action == "set") { if (args.Count < 6 || !Number(args[3], 0, MaxEntries - 1, out index)) return false; minuteAt = 4; }
            else if (action != "add") return false;
            if (args.Count <= minuteAt + 1 || !Number(args[minuteAt], 1, Announcements.MaxMinutes, out var minutes)
                || !TryText(args, minuteAt + 1, out var said) || !TextValid(said, false)) return false;
            request = new MessageEditRequest(verb, action, args[2], index, minutes, said.Trim()); return true;
        }
        static bool Number(string value, int min, int max, out int number) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= min && number <= max;

        public static IReadOnlyList<string> ValidateSource(string? source)
        {
            if (source == null || Encoding.UTF8.GetByteCount(source) > RemoteFiles.MaxBytes) return new[] { "AnnouncementSourceTooLarge" };
            var parsed = Announcements.Parse(source);
            var problems = parsed.Problems.ToList();
            if (parsed.Entries.Count > MaxEntries) problems.Add("TooManyAnnouncements");
            foreach (var line in source.Split('\n').Select(value => value.Trim()).Where(value => value.StartsWith("-", StringComparison.Ordinal)))
            {
                var body = line.Substring(1).Trim();
                if (body.Length >= 2 && (body[0] == '"' || body[0] == '\'') && body[body.Length - 1] == body[0]) body = body.Substring(1, body.Length - 2);
                var bar = body.IndexOf('|');
                if (bar >= 0 && body.Substring(bar + 1).Trim().Length > Announcements.MaxText) problems.Add("AnnouncementTextTooLong");
            }
            return problems;
        }

        // The existing source format has one entry per line. Only changed entries are replaced; comments and other lines stay byte-for-byte in the decoded text.
        public static string EditAnnouncements(string source, MessageEditRequest request)
        {
            if (request.Verb != "announce" || !request.Writes || Encoding.UTF8.GetByteCount(source) > RemoteFiles.MaxBytes) throw new ArgumentException("Invalid announcement edit.");
            if (ValidateSource(source).Count != 0) throw new FormatException("Malformed announcements.");
            var newline = source.Contains("\r\n") ? "\r\n" : "\n";
            var lines = source.Split('\n').ToList();
            var indexes = lines.Select((line, at) => (line, at)).Where(p => p.line.Trim().Length > 0 && !p.line.TrimStart().StartsWith("#", StringComparison.Ordinal)).Select(p => p.at).ToArray();
            string Entry() => "- " + request.Minutes.ToString(CultureInfo.InvariantCulture) + "|" + request.Text + (newline == "\r\n" ? "\r" : "");
            switch (request.Action)
            {
                case "add":
                    if (indexes.Length >= MaxEntries) throw new ArgumentException("Too many announcements.");
                    if (source.Length == 0) return Entry().TrimEnd('\r') + newline;
                    return source + (source.EndsWith("\n", StringComparison.Ordinal) ? "" : newline) + Entry().TrimEnd('\r') + newline;
                case "set":
                    if (request.Index >= indexes.Length) throw new ArgumentException("Missing announcement.");
                    var at = indexes[request.Index]; var old = lines[at];
                    lines[at] = Entry().TrimEnd('\r') + (old.EndsWith("\r", StringComparison.Ordinal) ? "\r" : "");
                    break;
                case "remove":
                    if (request.Index >= indexes.Length) throw new ArgumentException("Missing announcement.");
                    lines.RemoveAt(indexes[request.Index]); break;
                case "clear":
                    foreach (var removeAt in Enumerable.Reverse(indexes)) lines.RemoveAt(removeAt); break;
                default: throw new ArgumentException("Invalid announcement action.");
            }
            return string.Join("\n", lines);
        }
    }

    public sealed class MessageDocument
    {
        public string Kind { get; }
        public string Version { get; }
        public int Index { get; }
        public int Minutes { get; }
        public string Text { get; }
        public MessageDocument(string kind, string version, int index, int minutes, string text)
        { Kind = kind; Version = version; Index = index; Minutes = minutes; Text = text; }
        public IReadOnlyList<string> Encode()
        {
            if (!MessageEditing.Version(Version) || (Kind != "motd" && Kind != "announce")) throw new ArgumentException("Invalid message document.");
            return new[] { "THING_MESSAGE\t" + Kind + "\t" + Version + "\t" + Index.ToString(CultureInfo.InvariantCulture) + "\t" + Minutes.ToString(CultureInfo.InvariantCulture) }.Concat(MessageEditing.Pack(Text)).ToArray();
        }
        public static bool TryDecode(IReadOnlyList<string> lines, out MessageDocument document)
        {
            document = null!;
            if (lines.Count < 3 || lines.Count > 6) return false;
            var head = lines[0].Split('\t');
            if (head.Length != 5 || head[0] != "THING_MESSAGE" || head[1] != "motd" && head[1] != "announce"
                || !MessageEditing.Version(head[2]) || !int.TryParse(head[3], NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= MessageEditing.MaxEntries
                || !int.TryParse(head[4], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
                || (head[1] == "motd" ? minutes != 0 : minutes < 1 || minutes > Announcements.MaxMinutes)
                || lines[1] != "--utf8" || !MessageEditing.TryText(lines, 1, out var text) || !MessageEditing.TextValid(text, head[1] == "motd", empty: head[1] == "motd")) return false;
            document = new MessageDocument(head[1], head[2], index, minutes, text); return true;
        }
    }
}
