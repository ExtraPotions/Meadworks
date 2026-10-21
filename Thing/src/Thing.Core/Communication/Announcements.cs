// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: message of the day, recurring announcements and the shutdown countdown (design B1 to B3, 4.3).
// Admin-written texts are sent as written (design 11); only Thing's own frame texts are localized.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ExtraPotions.Thing.Communication
{
    public sealed class Announcement
    {
        public Announcement(int minutes, string text)
        {
            Minutes = minutes;
            Text = text;
        }

        /// <summary>Real minutes between repeats (1 to 1440).</summary>
        public int Minutes { get; }

        public string Text { get; }
    }

    public sealed class AnnouncementFile
    {
        internal AnnouncementFile(IReadOnlyList<Announcement> entries, IReadOnlyList<string> problems)
        {
            Entries = entries;
            Problems = problems;
        }

        public IReadOnlyList<Announcement> Entries { get; }

        /// <summary>One line per refused line: <c>line N: why</c>.</summary>
        public IReadOnlyList<string> Problems { get; }
    }

    /// <summary>
    /// <c>Thing.Announcements.yml</c>: a YAML list of <c>minutes|text</c> strings, one per line, optionally quoted;
    /// <c>#</c> comments and blank lines are ignored.
    /// </summary>
    public static class Announcements
    {
        public const int MaxMinutes = 1440;
        public const int MaxText = 500;
        const string Header = "# Thing announcements: one entry per line, minutes|text (real minutes between repeats, 1 to 1440)";

        public static AnnouncementFile Parse(string? text)
        {
            var entries = new List<Announcement>();
            var problems = new List<string>();
            var lines = (text ?? "").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var where = "line " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": ";
                if (!line.StartsWith("-", StringComparison.Ordinal)) { problems.Add(where + "expected a list entry starting with -"); continue; }
                var body = line.Substring(1).Trim();
                if (body.Length >= 2 && (body[0] == '"' || body[0] == '\'') && body[body.Length - 1] == body[0]) body = body.Substring(1, body.Length - 2);
                var bar = body.IndexOf('|');
                if (bar < 0) { problems.Add(where + "expected minutes|text"); continue; }
                var minutesText = body.Substring(0, bar).Trim();
                if (!int.TryParse(minutesText, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes < 1 || minutes > MaxMinutes)
                {
                    problems.Add(where + "minutes must be a whole number from 1 to " + MaxMinutes.ToString(CultureInfo.InvariantCulture));
                    continue;
                }
                var said = body.Substring(bar + 1).Trim();
                if (said.Length == 0) { problems.Add(where + "empty text"); continue; }
                entries.Add(new Announcement(minutes, said.Length > MaxText ? said.Substring(0, MaxText) : said));
            }
            return new AnnouncementFile(entries, problems);
        }

        public static string Format(IEnumerable<Announcement> entries)
        {
            var b = new StringBuilder(Header).Append('\n');
            foreach (var e in entries) b.Append("- \"").Append(e.Minutes.ToString(CultureInfo.InvariantCulture)).Append('|').Append(e.Text.Replace("\"", "'")).Append("\"\n");
            return b.ToString();
        }

        /// <summary>
        /// The entries due between two moments of real server time (seconds since Thing started on the server): each entry
        /// fires when a multiple of its period lies in (previous, now], at most once per call.
        /// </summary>
        public static IReadOnlyList<Announcement> Due(IEnumerable<Announcement> entries, double previousSeconds, double nowSeconds)
        {
            if (!(nowSeconds > previousSeconds)) return Array.Empty<Announcement>();
            return entries.Where(e =>
            {
                var period = e.Minutes * 60.0;
                return Math.Floor(nowSeconds / period) > Math.Floor(previousSeconds / period);
            }).ToList();
        }
    }

    public static class MessageOfTheDay
    {
        /// <summary>The text to show a joining player, or null when there is none.</summary>
        public static string? ForJoin(string? text)
        {
            var t = (text ?? "").Trim();
            return t.Length == 0 ? null : t;
        }
    }

    public enum CountdownEventKind { Start, Warn, Save }

    public readonly struct CountdownEvent
    {
        public CountdownEvent(CountdownEventKind kind, int minutesLeft)
        {
            Kind = kind;
            MinutesLeft = minutesLeft;
        }

        public CountdownEventKind Kind { get; }
        public int MinutesLeft { get; }

        public override string ToString() => Kind == CountdownEventKind.Save ? "save" : Kind.ToString().ToLowerInvariant() + " " + MinutesLeft.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Design B3: an announced countdown; warnings at the configured minutes below the total, then the world save at zero.
    /// The server process exit is left to the host's tooling. Times are real seconds on the server.
    /// </summary>
    public sealed class ShutdownCountdown
    {
        public const int MaxMinutes = 120;

        readonly List<int> pending = new List<int>();
        double endSeconds;

        public bool Running { get; private set; }

        public double SecondsLeft(double nowSeconds) => Running ? Math.Max(0, endSeconds - nowSeconds) : 0;

        public CountdownEvent Start(int totalMinutes, IEnumerable<int> warningMinutes, double nowSeconds)
        {
            if (totalMinutes < 1 || totalMinutes > MaxMinutes) throw new ArgumentException("Expected 1 to 120 minutes.", nameof(totalMinutes));
            pending.Clear();
            pending.AddRange((warningMinutes ?? Enumerable.Empty<int>()).Where(m => m >= 1 && m < totalMinutes).Distinct().OrderByDescending(m => m));
            endSeconds = nowSeconds + totalMinutes * 60.0;
            Running = true;
            return new CountdownEvent(CountdownEventKind.Start, totalMinutes);
        }

        public IReadOnlyList<CountdownEvent> Tick(double nowSeconds)
        {
            if (!Running) return Array.Empty<CountdownEvent>();
            var events = new List<CountdownEvent>();
            var left = endSeconds - nowSeconds;
            while (pending.Count > 0 && pending[0] * 60.0 >= left)
            {
                events.Add(new CountdownEvent(CountdownEventKind.Warn, pending[0]));
                pending.RemoveAt(0);
            }
            if (left <= 0)
            {
                events.Add(new CountdownEvent(CountdownEventKind.Save, 0));
                Running = false;
                pending.Clear();
            }
            return events;
        }

        public bool Cancel()
        {
            if (!Running) return false;
            Running = false;
            pending.Clear();
            return true;
        }
    }
}
