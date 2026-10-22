// Adapted from TradersExtended 2.0.5 (shudnal), ItemLists/ItemConfigFiles.cs (DeserializeCsvItems, ParseCsvInt, ParseCsvRecords),
// pin 2d5dd879ead70f809f53025d6b28feb3dee7b605. Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: one reader for rows from any format (the plugin turns YAML and JSON into rows), errors and warnings
// returned instead of logged, the CSV record parser kept as upstream wrote it, entries that cannot be offered skipped with a warning.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ExtraPotions.Barter.Lists
{
    /// <summary>What reading one list file produced. A file with any error is rejected whole; the previous lists stay in force.</summary>
    public sealed class ListReadResult
    {
        public List<TradeEntry> Entries { get; } = new List<TradeEntry>();

        public List<string> Errors { get; } = new List<string>();

        public List<string> Warnings { get; } = new List<string>();

        public bool Ok => Errors.Count == 0;
    }

    public static class TradeListReader
    {
        private static readonly string[] Fields =
        {
            "prefab", "stack", "price", "quality", "currency",
            "requiredGlobalKey", "notRequiredGlobalKey", "requiredPlayerKey", "notRequiredPlayerKey",
        };

        /// <summary>Reads rows of field name to text, as the plugin gets them from a YAML or JSON list.</summary>
        public static ListReadResult FromRows(IEnumerable<IReadOnlyDictionary<string, string>>? rows, string source)
        {
            var result = new ListReadResult();
            if (rows == null)
                return result;

            var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int rowNumber = 0;
            foreach (IReadOnlyDictionary<string, string> raw in rows)
            {
                rowNumber++;
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> pair in raw)
                {
                    if (pair.Key == null)
                        continue;
                    if (!Fields.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        if (unknown.Add(pair.Key))
                            result.Warnings.Add($"Ignored unknown field '{pair.Key}' in '{source}'.");
                        continue;
                    }
                    row[pair.Key] = pair.Value ?? string.Empty;
                }

                var entry = new TradeEntry();
                try
                {
                    entry.Prefab = Text(row, "prefab");
                    entry.Stack = Number(row, "stack", entry.Stack, source, rowNumber);
                    entry.Price = Number(row, "price", entry.Price, source, rowNumber);
                    entry.Quality = Number(row, "quality", entry.Quality, source, rowNumber);
                }
                catch (FormatException exception)
                {
                    result.Errors.Add(exception.Message);
                    continue;
                }
                entry.Currency = Text(row, "currency");
                entry.RequiredGlobalKey = Text(row, "requiredGlobalKey");
                entry.NotRequiredGlobalKey = Text(row, "notRequiredGlobalKey");
                entry.RequiredPlayerKey = Text(row, "requiredPlayerKey");
                entry.NotRequiredPlayerKey = Text(row, "notRequiredPlayerKey");

                if (string.IsNullOrWhiteSpace(entry.Prefab))
                {
                    result.Warnings.Add($"Skipped row {rowNumber} of '{source}': it has no prefab.");
                    continue;
                }

                IReadOnlyList<string> problems = entry.Validate();
                if (problems.Count > 0)
                {
                    result.Warnings.Add($"Skipped row {rowNumber} of '{source}' ({entry.Prefab}): {string.Join("; ", problems)}.");
                    continue;
                }

                result.Entries.Add(entry);
            }

            return result;
        }

        /// <summary>Reads a CSV list: a header row with at least "prefab", then one entry per row.</summary>
        public static ListReadResult FromCsv(string? content, string source)
        {
            try
            {
                List<List<string>> records = ParseCsvRecords(content ?? string.Empty);
                if (records.Count == 0)
                    return new ListReadResult();

                string[] headers = records[0]
                    .Select((header, index) => index == 0 ? (header ?? string.Empty).TrimStart('﻿').Trim() : (header ?? string.Empty).Trim())
                    .ToArray();

                if (!headers.Any(header => string.Equals(header, "prefab", StringComparison.OrdinalIgnoreCase)))
                    throw new FormatException($"CSV list '{source}' must contain the 'prefab' header.");

                if (headers.Where(header => header.Length > 0)
                    .GroupBy(header => header, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                    throw new FormatException($"CSV list '{source}' contains a duplicate column header.");

                var rows = new List<IReadOnlyDictionary<string, string>>();
                for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
                {
                    List<string> record = records[rowIndex];
                    if (record.All(string.IsNullOrWhiteSpace))
                        continue;

                    if (record.Skip(headers.Length).Any(value => !string.IsNullOrWhiteSpace(value)))
                        throw new FormatException($"CSV row {rowIndex + 1} of '{source}' has more values than its header.");

                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int column = 0; column < headers.Length; column++)
                    {
                        if (headers[column].Length == 0)
                            continue;
                        string value = column < record.Count ? record[column] ?? string.Empty : string.Empty;
                        row[headers[column]] = value;
                    }
                    rows.Add(row);
                }

                return FromRows(rows, source);
            }
            catch (FormatException exception)
            {
                var failed = new ListReadResult();
                failed.Errors.Add(exception.Message);
                return failed;
            }
        }

        private static string Text(Dictionary<string, string> row, string field) =>
            row.TryGetValue(field, out string? value) ? (value ?? string.Empty).Trim() : string.Empty;

        private static int Number(Dictionary<string, string> row, string field, int fallback, string source, int rowNumber)
        {
            string value = Text(row, field);
            if (value.Length == 0)
                return fallback;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                return result;

            throw new FormatException($"Invalid integer '{value}' in row {rowNumber}, field '{field}' of '{source}'.");
        }

        /// <summary>CSV records with quoted fields, doubled quotes, CRLF or LF, and a leading byte order mark. Upstream algorithm.</summary>
        internal static List<List<string>> ParseCsvRecords(string content)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            bool closedQuote = false;
            content = (content ?? string.Empty).TrimStart('﻿');
            for (int index = 0; index < content.Length; index++)
            {
                char current = content[index];
                if (quoted)
                {
                    if (current != '"')
                        field.Append(current);
                    else if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                        closedQuote = true;
                    }
                    continue;
                }
                if (current == ',' || current == '\r' || current == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    closedQuote = false;
                    if (current != ',')
                    {
                        if (current == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                            index++;
                        rows.Add(row);
                        row = new List<string>();
                    }
                }
                else if (current == '"' && field.Length == 0 && !closedQuote)
                    quoted = true;
                else if (closedQuote || current == '"')
                    throw new FormatException("CSV contains unexpected text outside a quoted field.");
                else
                    field.Append(current);
            }
            if (quoted)
                throw new FormatException("CSV contains an unterminated quote in a quoted field.");
            if (field.Length > 0 || closedQuote || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }
}
