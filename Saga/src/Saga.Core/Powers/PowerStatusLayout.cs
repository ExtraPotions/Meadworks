// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Saga.Powers
{
    /// <summary>One guardian status line after it has been wrapped. <see cref="Text"/> uses a newline between the two lines.</summary>
    public readonly struct PowerStatusFit
    {
        public PowerStatusFit(string text, int lines, bool ellipsis)
        {
            Text = text ?? "";
            Lines = lines < 1 ? 1 : lines;
            Ellipsis = ellipsis;
        }

        public string Text { get; }
        public int Lines { get; }
        public bool Ellipsis { get; }
    }

    /// <summary>One row of the guardian status plate, coordinates relative to the plate's top left.</summary>
    public readonly struct PowerStatusRow
    {
        public PowerStatusRow(float x, float y, float width, float height, float iconX, float iconY, float iconSize,
            float textX, float textY, float textWidth, float textHeight, string shown, int lines, bool ellipsis)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            IconX = iconX;
            IconY = iconY;
            IconSize = iconSize;
            TextX = textX;
            TextY = textY;
            TextWidth = textWidth;
            TextHeight = textHeight;
            Shown = shown ?? "";
            Lines = lines < 1 ? 1 : lines;
            Ellipsis = ellipsis;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float IconX { get; }
        public float IconY { get; }
        public float IconSize { get; }
        public float TextX { get; }
        public float TextY { get; }
        public float TextWidth { get; }
        public float TextHeight { get; }
        public string Shown { get; }
        public int Lines { get; }
        public bool Ellipsis { get; }

        public bool Holds(float x, float y, float width, float height) =>
            x >= X - 0.01f && y >= Y - 0.01f && x + width <= X + Width + 0.01f && y + height <= Y + Height + 0.01f;
    }

    /// <summary>
    /// The wood plate above the native guardian icon. Compact rows keep the name and the mastery or timer inside
    /// the plate, wrapping to two lines. Three periods stand in for an ellipsis because the game fonts have no
    /// ellipsis glyph.
    /// </summary>
    public sealed class PowerStatusLayout
    {
        public const float PlateWidth = 245f;
        public const float Pad = 8f;
        public const float RowWidth = 229f;
        public const float Icon = 24f;
        public const float TextWidth = 173f;
        public const float FontSize = 13f;
        public const float CharFactor = 0.55f;
        public const int MaxLines = 2;
        const float OneLineText = 24f;
        const float ExtraLine = 18f;
        const float MinRow = 28f;
        const string Dots = "...";

        PowerStatusLayout(float plateHeight, PowerStatusRow[] rows)
        {
            PlateHeight = plateHeight;
            Rows = rows;
        }

        public float PlateHeight { get; }
        public IReadOnlyList<PowerStatusRow> Rows { get; }

        public bool Holds(float x, float y, float width, float height) =>
            x >= -0.01f && y >= -0.01f && x + width <= PlateWidth + 0.01f && y + height <= PlateHeight + 0.01f;

        public static PowerStatusLayout Layout(IReadOnlyList<string> labels, float rowGap)
        {
            if (rowGap < 0f) rowGap = 0f;
            int count = labels?.Count ?? 0;
            var rows = new PowerStatusRow[count];
            float y = Pad;
            for (int i = 0; i < count; i++)
            {
                var fit = Fit(labels[i], TextWidth);
                float textHeight = fit.Lines <= 1 ? OneLineText : OneLineText + (fit.Lines - 1) * ExtraLine;
                float rowHeight = Math.Max(MinRow, 2f + textHeight + 2f);
                float iconY = y + Math.Max(0f, (rowHeight - Icon) * 0.5f);
                rows[i] = new PowerStatusRow(
                    Pad, y, RowWidth, rowHeight,
                    Pad, iconY, Icon,
                    Pad + 32f, y + 2f, TextWidth, textHeight,
                    fit.Text, fit.Lines, fit.Ellipsis);
                y += rowHeight + rowGap;
            }
            float plateHeight = count == 0 ? 0f : y - rowGap + Pad;
            return new PowerStatusLayout(plateHeight, rows);
        }

        public static PowerStatusFit Fit(string text, float width)
        {
            if (string.IsNullOrEmpty(text)) return new PowerStatusFit("", 1, false);
            if (width < 1f) width = 1f;
            if (text.IndexOf('\n') < 0 && WidthOf(text) <= width) return new PowerStatusFit(text, 1, false);
            var lines = new List<string>(4);
            string current = "";
            foreach (string word in Words(text)) Append(lines, ref current, word, width);
            if (current.Length > 0) lines.Add(current);
            if (lines.Count == 0) return new PowerStatusFit("", 1, false);
            if (lines.Count <= MaxLines) return new PowerStatusFit(string.Join("\n", lines), lines.Count, false);
            string rest = text.Trim();
            if (rest.StartsWith(lines[0], StringComparison.Ordinal)) rest = rest.Substring(lines[0].Length).TrimStart();
            return new PowerStatusFit(lines[0] + "\n" + Clip(rest, width), MaxLines, true);
        }

        public static float WidthOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return text.Length * FontSize * CharFactor;
        }

        static string Clip(string text, float width)
        {
            if (string.IsNullOrEmpty(text)) return Dots;
            if (WidthOf(text) <= width) return text;
            float room = width - WidthOf(Dots);
            if (room < WidthOf("n")) return WidthOf(Dots) <= width ? Dots : "";
            string line = "";
            foreach (string word in Words(text))
            {
                if (WidthOf(word) > room)
                {
                    if (line.Length > 0) break;
                    int n = 1;
                    while (n < word.Length && WidthOf(word.Substring(0, n + 1)) <= room) n++;
                    line = word.Substring(0, n);
                    break;
                }
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (WidthOf(candidate) > room) break;
                line = candidate;
            }
            return line + Dots;
        }

        static void Append(List<string> lines, ref string current, string word, float width)
        {
            if (WidthOf(word) > width)
            {
                if (current.Length > 0) { lines.Add(current); current = ""; }
                int i = 0;
                while (i < word.Length)
                {
                    int n = 1;
                    while (i + n < word.Length && WidthOf(word.Substring(i, n + 1)) <= width) n++;
                    string chunk = word.Substring(i, n);
                    i += n;
                    if (i < word.Length) lines.Add(chunk);
                    else current = chunk;
                }
                return;
            }
            if (current.Length == 0) { current = word; return; }
            string joined = current + " " + word;
            if (WidthOf(joined) <= width) current = joined;
            else { lines.Add(current); current = word; }
        }

        static List<string> Words(string text)
        {
            var words = new List<string>(8);
            int i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && (text[i] == ' ' || text[i] == '\n' || text[i] == '\r' || text[i] == '\t')) i++;
                if (i >= text.Length) break;
                int start = i;
                while (i < text.Length && text[i] != ' ' && text[i] != '\n' && text[i] != '\r' && text[i] != '\t') i++;
                words.Add(text.Substring(start, i - start));
            }
            return words;
        }
    }
}
