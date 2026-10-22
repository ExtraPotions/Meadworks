// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original code. The list file fields are those of TradersExtended 2.0.5 (Unlicense); the reader around them is Barter's own.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExtraPotions.Barter.Lists;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Barter.Runtime.Lists
{
    /// <summary>
    /// Turns the text of a list file into the neutral rows Barter.Core reads. YAML is read with YamlDotNet; a JSON list is
    /// YAML flow syntax and goes through the same reader (TradersExtended's JSON files read unchanged). CSV is Barter.Core's own reader.
    /// This file depends on YamlDotNet only, so the portable tests compile it too.
    /// </summary>
    public static class ListRows
    {
        /// <summary>The longest list file Barter reads.</summary>
        public const int MaximumBytes = 1048576;

        public static ListReadResult Read(ListFormat format, string? content, string source)
        {
            if (content != null && Encoding.UTF8.GetByteCount(content) > MaximumBytes)
            {
                var tooLarge = new ListReadResult();
                tooLarge.Errors.Add($"List '{source}' is larger than 1 MiB.");
                return tooLarge;
            }

            if (format == ListFormat.Csv)
                return TradeListReader.FromCsv(content, source);

            try
            {
                return TradeListReader.FromRows(FromYaml(content, source), source);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException) && !(exception is StackOverflowException))
            {
                // YamlDotNet reports some syntax errors as a plain System.Exception with no useful message, so any failure
                // while reading the text is the file's fault and rejects only this file.
                var failed = new ListReadResult();
                failed.Errors.Add(exception is FormatException ? exception.Message
                    : exception is YamlException yaml && yaml.Start.Line > 0
                        ? $"List '{source}' is not valid YAML or JSON (line {yaml.Start.Line}): {yaml.Message}"
                        : $"List '{source}' is not valid YAML or JSON.");
                return failed;
            }
        }

        /// <summary>The rows of a YAML or JSON list: a sequence of mappings from field name to a single value.</summary>
        public static List<IReadOnlyDictionary<string, string>> FromYaml(string? content, string source)
        {
            var rows = new List<IReadOnlyDictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(content))
                return rows;

            var stream = new YamlStream();
            using (var reader = new StringReader(content))
                stream.Load(reader);
            if (stream.Documents.Count == 0)
                return rows;
            if (stream.Documents.Count > 1)
                throw new FormatException($"List '{source}' has more than one YAML document.");

            YamlNode root = stream.Documents[0].RootNode;
            if (root is YamlScalarNode emptyRoot && IsNull(emptyRoot))
                return rows;
            if (!(root is YamlSequenceNode sequence))
                throw new FormatException($"List '{source}' must be a list of entries.");

            int number = 0;
            foreach (YamlNode item in sequence.Children)
            {
                number++;
                if (!(item is YamlMappingNode mapping))
                    throw new FormatException($"Entry {number} of '{source}' must be a mapping of field names to values.");

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children)
                {
                    if (!(pair.Key is YamlScalarNode key) || key.Value == null)
                        throw new FormatException($"Entry {number} of '{source}' has a field name that is not text.");
                    if (!(pair.Value is YamlScalarNode value))
                        throw new FormatException($"Field '{key.Value}' of entry {number} in '{source}' must be a single value.");
                    if (row.ContainsKey(key.Value))
                        throw new FormatException($"Duplicate field '{key.Value}' of entry {number} in '{source}'.");
                    row[key.Value] = IsNull(value) ? string.Empty : value.Value ?? string.Empty;
                }
                rows.Add(row);
            }
            return rows;
        }

        private static bool IsNull(YamlScalarNode node)
        {
            if (node.Value == null)
                return true;
            if (node.Style != ScalarStyle.Plain)
                return false;
            string value = node.Value;
            return value.Length == 0 || value == "~" ||
                   string.Equals(value, "null", StringComparison.OrdinalIgnoreCase);
        }
    }
}
