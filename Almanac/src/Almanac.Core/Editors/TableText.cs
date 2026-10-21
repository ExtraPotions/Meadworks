// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behavior reference only (ConfigDrawers 1.1.3 data grids for recipe and drop lists); no source is copied.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Almanac.Editors
{
    /// <summary>Rows of a structured list split on one column delimiter; <see cref="TableText.Join"/> restores each row exactly.</summary>
    public sealed class TableShape
    {
        internal TableShape(char delimiter, int columns, IReadOnlyList<string[]> rows) { Delimiter = delimiter; Columns = columns; Rows = rows; }
        public char Delimiter { get; }
        public int Columns { get; }
        public IReadOnlyList<string[]> Rows { get; }
    }

    /// <summary>
    /// Detects multi-column structure in a list of entries, such as "Wood:5" pairs or "Boar|TrophyBoar|1|0.15" drop rows,
    /// so a list can be edited as a table. Detection needs every non-empty row to contain the same delimiter count; anything
    /// else stays a plain list. Cells are never trimmed, so a row the table did not touch is written back unchanged.
    /// </summary>
    public static class TableText
    {
        public const int MaxColumns = 6;
        static readonly char[] Candidates = { '|', ';', ':', '=', ',', '\t' };

        public static TableShape? Detect(IReadOnlyList<string> items, string? listSeparator = null)
        {
            var filled = items.Where(item => item.Length > 0).ToList();
            if (filled.Count == 0) return null;
            foreach (char delimiter in Candidates)
            {
                if (listSeparator != null && listSeparator.IndexOf(delimiter) >= 0) continue;
                int count = Count(filled[0], delimiter);
                if (count < 1 || count + 1 > MaxColumns || filled.Any(item => Count(item, delimiter) != count)) continue;
                var rows = items.Select(item => item.Length == 0 ? new string[count + 1].Select(_ => "").ToArray() : item.Split(delimiter)).ToList();
                return new TableShape(delimiter, count + 1, rows);
            }
            return null;
        }

        /// <summary>A row whose cells are all empty joins to an empty entry, matching how <see cref="Detect"/> read it.</summary>
        public static string Join(IReadOnlyList<string> cells, char delimiter) => cells.All(cell => cell.Length == 0) ? "" : string.Join(delimiter.ToString(), cells);

        /// <summary>Empty when the cell can be stored; a cell may not contain the column delimiter, the list separator or a line break.</summary>
        public static string CellError(string cell, char delimiter, string? listSeparator)
        {
            if (cell.IndexOf(delimiter) >= 0) return "A cell cannot contain the column separator";
            if (!string.IsNullOrEmpty(listSeparator) && cell.Contains(listSeparator!)) return "A cell cannot contain the list separator";
            if (cell.IndexOfAny(new[] { '\r', '\n' }) >= 0) return "A cell cannot contain a line break";
            return "";
        }

        /// <summary>Splits multi-line text into rows, remembering the line break so <see cref="JoinLines"/> restores it.</summary>
        public static IReadOnlyList<string> Lines(string text, out string lineBreak)
        {
            lineBreak = text.Contains("\r\n") ? "\r\n" : "\n";
            return text.Split(new[] { lineBreak }, StringSplitOptions.None);
        }

        public static string JoinLines(IEnumerable<string> rows, string lineBreak) => string.Join(lineBreak, rows);

        static int Count(string text, char c)
        {
            int n = 0;
            foreach (char x in text) if (x == c) n++;
            return n;
        }
    }
}
