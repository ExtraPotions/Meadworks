// Adapted from TradersExtended 2.0.5 (shudnal), ItemLists/TradeableItem.cs, pin 2d5dd879ead70f809f53025d6b28feb3dee7b605.
// Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: the data and its validation only. The upstream class also resolved the game item and read the player's
// keys; here that is ITradeContext/EntryAvailability so it can be tested without the game. Field names are the upstream list-file field names.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Barter.Lists
{
    /// <summary>
    /// One line of a trade list: an item a trader sells (Buy list) or pays for (Sell list), with its lot size, price, optional
    /// quality, optional currency and the keys that decide when it is offered. The field names are those of the TradersExtended
    /// list files, so a list written for that mod reads here unchanged.
    /// </summary>
    public sealed class TradeEntry
    {
        /// <summary>The item prefab, for example "SwordIron".</summary>
        public string Prefab { get; set; } = string.Empty;

        /// <summary>Items per lot (Buy: items bought per purchase; Sell: items per lot sold).</summary>
        public int Stack { get; set; } = 1;

        /// <summary>The price of one lot in the entry's currency.</summary>
        public int Price { get; set; } = 1;

        /// <summary>The item quality this entry is for; 0 for any quality.</summary>
        public int Quality { get; set; }

        /// <summary>The currency item prefab for this entry; empty uses the trader's currency, then Coins.</summary>
        public string Currency { get; set; } = string.Empty;

        public string RequiredGlobalKey { get; set; } = string.Empty;

        public string NotRequiredGlobalKey { get; set; } = string.Empty;

        public string RequiredPlayerKey { get; set; } = string.Empty;

        public string NotRequiredPlayerKey { get; set; } = string.Empty;

        /// <summary>The entry was added by Barter (common valuables), not written in a list file.</summary>
        public bool Automatic { get; set; }

        /// <summary>What is wrong with this entry; empty when it can be offered.</summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(Prefab))
                problems.Add("prefab is empty");
            if (Price <= 0)
                problems.Add("price must be above 0, found " + Price);
            if (Stack <= 0 || Stack >= BarterCore.QualityStackMultiplier)
                problems.Add("stack must be from 1 to " + (BarterCore.QualityStackMultiplier - 1) + ", found " + Stack);
            if (Quality < 0)
                problems.Add("quality must not be negative, found " + Quality);
            else if (Stack > 0 && Stack < BarterCore.QualityStackMultiplier &&
                     !Trading.TradeAmounts.TryEncodeStack(Stack, Quality, out _))
                problems.Add("quality " + Quality + " is too large to combine with stack " + Stack);
            return problems;
        }

        /// <summary>The keys of a comma separated key field, trimmed, empty parts dropped.</summary>
        public static IReadOnlyList<string> SplitKeys(string? value)
        {
            var keys = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
                return keys;

            foreach (string part in value!.Split(','))
            {
                string key = part.Trim();
                if (key.Length > 0)
                    keys.Add(key);
            }
            return keys;
        }

        public TradeEntry Clone() => new TradeEntry
        {
            Prefab = Prefab,
            Stack = Stack,
            Price = Price,
            Quality = Quality,
            Currency = Currency,
            RequiredGlobalKey = RequiredGlobalKey,
            NotRequiredGlobalKey = NotRequiredGlobalKey,
            RequiredPlayerKey = RequiredPlayerKey,
            NotRequiredPlayerKey = NotRequiredPlayerKey,
            Automatic = Automatic,
        };
    }
}
