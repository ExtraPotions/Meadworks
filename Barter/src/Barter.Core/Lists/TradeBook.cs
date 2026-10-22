// Adapted from TradersExtended 2.0.5 (shudnal), ItemLists/ItemConfigFiles.cs (LoadConfigs list assembly, AddCommonValuableItems),
// pin 2d5dd879ead70f809f53025d6b28feb3dee7b605. Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: an immutable snapshot with a content revision, built from already parsed files, no coroutine or ObjectDB access.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Barter.Lists
{
    /// <summary>The entries one list file contributed.</summary>
    public sealed class ListContribution
    {
        /// <param name="sourceName">The file's path relative to the config folder, which decides the order lists are applied in; defaults to the bare file name.</param>
        /// <param name="embedded">The file is a default shipped inside the plugin; those are applied before the files of the config folder.</param>
        public ListContribution(ListFileName name, IReadOnlyList<TradeEntry> entries, string? sourceName = null, bool embedded = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            SourceName = string.IsNullOrEmpty(sourceName) ? name.FileName : sourceName!;
            Embedded = embedded;
        }

        public ListFileName Name { get; }

        public IReadOnlyList<TradeEntry> Entries { get; }

        public string SourceName { get; }

        public bool Embedded { get; }
    }

    /// <summary>
    /// Every trade list in force: for each trader, a Buy and a Sell list, plus the common lists that apply to all traders.
    /// A snapshot never changes; a reload builds a new one and the plugin swaps it in as a whole.
    /// </summary>
    public sealed class TradeBook
    {
        private static readonly IReadOnlyList<TradeEntry> None = Array.Empty<TradeEntry>();

        private readonly Dictionary<string, List<TradeEntry>> lists;

        private TradeBook(Dictionary<string, List<TradeEntry>> lists)
        {
            this.lists = lists;
            Revision = ComputeRevision(lists);
        }

        public static TradeBook Empty { get; } = new TradeBook(new Dictionary<string, List<TradeEntry>>(StringComparer.OrdinalIgnoreCase));

        /// <summary>Changes whenever any entry of any list changes; equal content gives an equal revision.</summary>
        public long Revision { get; }

        /// <summary>The traders that have a list of their own, normalized and sorted; the common lists are not a trader.</summary>
        public IReadOnlyList<string> Traders =>
            lists.Keys
                .Select(key => key.Substring(0, key.LastIndexOf('.')))
                .Where(trader => !TraderNames.IsCommon(trader))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(trader => trader, StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// Builds a book from parsed files, applied in the order of <see cref="ListFileName.Order"/> whatever order they are given in
        /// (embedded defaults first, then the config folder by path); entries are copied.
        /// </summary>
        public static TradeBook Build(IEnumerable<ListContribution> files)
        {
            var built = new Dictionary<string, List<TradeEntry>>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<ListContribution> ordered = files
                .OrderBy(file => file.Embedded ? 0 : 1)
                .ThenBy(file => file.SourceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.SourceName, StringComparer.Ordinal);
            foreach (ListContribution file in ordered)
            {
                string key = file.Name.ListKey;
                if (!built.TryGetValue(key, out List<TradeEntry>? list))
                {
                    list = new List<TradeEntry>();
                    built.Add(key, list);
                }
                list.AddRange(file.Entries.Select(entry => entry.Clone()));
            }
            return new TradeBook(built);
        }

        /// <summary>A book with <paramref name="automatic"/> entries added after the written ones of the common Sell list.</summary>
        public TradeBook WithAutomatic(IEnumerable<TradeEntry> automatic)
        {
            var copy = lists.ToDictionary(pair => pair.Key, pair => pair.Value.Select(entry => entry.Clone()).ToList(), StringComparer.OrdinalIgnoreCase);
            string key = TraderNames.CommonKey(ListKind.Sell);
            if (!copy.TryGetValue(key, out List<TradeEntry>? list))
            {
                list = new List<TradeEntry>();
                copy.Add(key, list);
            }
            list.AddRange(automatic.Select(entry => { TradeEntry clone = entry.Clone(); clone.Automatic = true; return clone; }));
            return new TradeBook(copy);
        }

        /// <summary>What the trader sells: the common Buy list, then the trader's own.</summary>
        public IReadOnlyList<TradeEntry> Buy(string? trader) => Combined(trader, ListKind.Buy, includeAutomatic: true);

        /// <summary>What the trader pays for: the common Sell list, then the trader's own.</summary>
        public IReadOnlyList<TradeEntry> Sell(string? trader, bool includeAutomatic = true) => Combined(trader, ListKind.Sell, includeAutomatic);

        private IReadOnlyList<TradeEntry> Combined(string? trader, ListKind kind, bool includeAutomatic)
        {
            var result = new List<TradeEntry>();
            if (lists.TryGetValue(TraderNames.CommonKey(kind), out List<TradeEntry>? common))
                result.AddRange(common.Where(entry => includeAutomatic || !entry.Automatic));
            if (!TraderNames.IsCommon(trader) && lists.TryGetValue(TraderNames.ListKey(trader, kind), out List<TradeEntry>? own))
                result.AddRange(own.Where(entry => includeAutomatic || !entry.Automatic));
            return result.Count == 0 ? None : result;
        }

        private static long ComputeRevision(Dictionary<string, List<TradeEntry>> lists)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                void Mix(string text)
                {
                    foreach (char c in text)
                    {
                        hash ^= c;
                        hash *= 1099511628211UL;
                    }
                    hash ^= 0x1F;
                    hash *= 1099511628211UL;
                }

                foreach (string key in lists.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                {
                    Mix(key.ToLowerInvariant());
                    foreach (TradeEntry e in lists[key])
                    {
                        Mix(e.Prefab);
                        Mix(e.Stack.ToString());
                        Mix(e.Price.ToString());
                        Mix(e.Quality.ToString());
                        Mix(e.Currency);
                        Mix(e.RequiredGlobalKey);
                        Mix(e.NotRequiredGlobalKey);
                        Mix(e.RequiredPlayerKey);
                        Mix(e.NotRequiredPlayerKey);
                        Mix(e.Automatic ? "1" : "0");
                    }
                }
                return (long)hash;
            }
        }
    }

    /// <summary>The "common valuable items" sell entries: every item the game gives a value, priced at that value.</summary>
    public static class CommonValuables
    {
        /// <param name="items">Every item prefab with its vanilla value.</param>
        /// <param name="alreadyListed">Prefabs a written common Sell entry already prices.</param>
        /// <param name="currencyPrefab">The currency; it is never sold for itself.</param>
        public static List<TradeEntry> Build(IEnumerable<(string Prefab, int Value)> items, IEnumerable<string> alreadyListed, string currencyPrefab)
        {
            var seen = new HashSet<string>(alreadyListed ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var entries = new List<TradeEntry>();
            foreach ((string prefab, int value) in items)
            {
                if (string.IsNullOrWhiteSpace(prefab) || value <= 0 ||
                    string.Equals(prefab, BarterCore.VanillaCurrency, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefab, currencyPrefab, StringComparison.OrdinalIgnoreCase) ||
                    !seen.Add(prefab))
                    continue;

                entries.Add(new TradeEntry { Prefab = prefab, Price = value, Stack = 1, Automatic = true });
            }
            return entries;
        }
    }
}
