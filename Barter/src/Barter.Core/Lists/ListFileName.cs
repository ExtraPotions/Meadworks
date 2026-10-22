// Adapted from TradersExtended 2.0.5 (shudnal), ItemLists/ItemConfigFiles.cs (TryParseConfigFileName, TryParseConfigName,
// IsSupportedItemConfigExtension, ConfigSourcePriority), pin 2d5dd879ead70f809f53025d6b28feb3dee7b605.
// Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: the Barter. file prefix, a parsed result type, the file format, and a stable load order
// (embedded defaults first, then the config folder by name) replacing the upstream's synced key scheme.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtraPotions.Barter.Lists
{
    /// <summary>A list file name taken apart: <c>Barter.&lt;trader&gt;.&lt;Buy|Sell&gt;[.&lt;identifier&gt;].&lt;yml|yaml|json|csv&gt;</c>.</summary>
    public sealed class ListFileName
    {
        private ListFileName(string fileName, string trader, ListKind kind, string identifier, ListFormat format)
        {
            FileName = fileName;
            Trader = trader;
            Kind = kind;
            Identifier = identifier;
            Format = format;
        }

        public string FileName { get; }

        /// <summary>The trader the list belongs to, normalized (lower case); "common" applies to every trader.</summary>
        public string Trader { get; }

        public ListKind Kind { get; }

        /// <summary>Anything between the kind and the extension; lets one trader have several files.</summary>
        public string Identifier { get; }

        public ListFormat Format { get; }

        /// <summary>The key of the list this file adds to ("haldor.Buy").</summary>
        public string ListKey => TraderNames.ListKey(Trader, Kind);

        public static bool IsSupportedExtension(string? extension)
        {
            return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);
        }

        /// <param name="allowUnprefixed">
        /// Also accept <c>haldor.buy.json</c> without the Barter. prefix, as TradersExtended names its files. The plugin allows this only
        /// inside the Barter folder of the config directory, where a bare name cannot be mistaken for another mod's file.
        /// </param>
        public static bool TryParse(string? fileName, out ListFileName? parsed, bool allowUnprefixed = false)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            string name = Path.GetFileName(fileName!);
            string extension = Path.GetExtension(name);
            if (!IsSupportedExtension(extension))
                return false;

            string withoutExtension = Path.GetFileNameWithoutExtension(name);
            bool prefixed = withoutExtension.StartsWith(BarterCore.FilePrefix, StringComparison.OrdinalIgnoreCase);
            if (!prefixed && !allowUnprefixed)
                return false;

            string[] segments = (prefixed ? withoutExtension.Substring(BarterCore.FilePrefix.Length) : withoutExtension).Split('.');
            for (int index = 1; index < segments.Length; index++)
            {
                ListKind kind;
                if (string.Equals(segments[index], nameof(ListKind.Buy), StringComparison.OrdinalIgnoreCase))
                    kind = ListKind.Buy;
                else if (string.Equals(segments[index], nameof(ListKind.Sell), StringComparison.OrdinalIgnoreCase))
                    kind = ListKind.Sell;
                else
                    continue;

                string trader = TraderNames.Normalize(string.Join(".", segments.Take(index)));
                if (trader.Length == 0)
                    return false;

                string identifier = string.Join(".", segments.Skip(index + 1));
                ListFormat format = string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase) ? ListFormat.Csv
                    : string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ? ListFormat.Json
                    : ListFormat.Yaml;
                parsed = new ListFileName(name, trader, kind, identifier, format);
                return true;
            }

            return false;
        }

        /// <summary>
        /// The order lists are applied in: embedded defaults first, then the files of the config folder, each group by file name
        /// without regard to case. Later entries of the same trader and kind follow earlier ones.
        /// </summary>
        public static IEnumerable<ListSourceFile> Order(IEnumerable<ListSourceFile> files)
        {
            return files
                .OrderBy(file => file.Embedded ? 0 : 1)
                .ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.FileName, StringComparer.Ordinal);
        }
    }

    /// <summary>A list file found in the config folder or among the embedded defaults.</summary>
    public sealed class ListSourceFile
    {
        public ListSourceFile(string fileName, bool embedded)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            Embedded = embedded;
        }

        public string FileName { get; }

        public bool Embedded { get; }
    }
}
