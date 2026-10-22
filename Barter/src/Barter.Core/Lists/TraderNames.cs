// Adapted from TradersExtended 2.0.5 (shudnal), ItemLists/ItemConfigFiles.cs (TraderName, TraderListKey, CommonListKey), pin 2d5dd879.
// Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt). Changed by expDARE, 2026: pure functions, no Unity or ItemsListType enum.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Barter.Lists
{
    /// <summary>Trader prefab names as the lists key them: lower case without the NPC prefix.</summary>
    public static class TraderNames
    {
        /// <summary>The traders the game ships; they always have a purse and a currency rule.</summary>
        public static readonly IReadOnlyList<string> Vanilla = new[] { "Haldor", "Hildir", "BogWitch" };

        /// <summary>"Haldor", "npc_Haldor" and "$npc_haldor" are the same trader: lower case, prefix removed.</summary>
        public static string Normalize(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            return name!.Trim().ToLowerInvariant().Replace("$npc_", string.Empty).Replace("npc_", string.Empty);
        }

        /// <summary>The key one trader's list of one kind is stored under ("haldor.Buy").</summary>
        public static string ListKey(string? trader, ListKind kind) => Normalize(trader) + "." + kind;

        /// <summary>The key of the list that applies to every trader ("common.Sell").</summary>
        public static string CommonKey(ListKind kind) => ListKey(BarterCore.CommonTrader, kind);

        public static bool IsCommon(string? trader) =>
            string.Equals(Normalize(trader), BarterCore.CommonTrader, StringComparison.Ordinal);

        /// <summary>Splits a comma, semicolon or newline separated list of names, trimmed, empty parts dropped.</summary>
        public static IEnumerable<string> SplitList(string? value)
        {
            foreach (string part in (value ?? string.Empty).Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                    yield return trimmed;
            }
        }

        public static bool ListContains(string? value, string? trader)
        {
            string wanted = Normalize(trader);
            foreach (string entry in SplitList(value))
                if (string.Equals(Normalize(entry), wanted, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
