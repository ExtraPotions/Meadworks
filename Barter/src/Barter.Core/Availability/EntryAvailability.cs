// Adapted from TradersExtended 2.0.5 (shudnal), ItemLists/TradeableItem.cs (RequirementsMet, IsItemToSell) and
// ItemLists/StorePanel.Lists.cs (AddItemToSellList, TryGetPriceKey), pin 2d5dd879ead70f809f53025d6b28feb3dee7b605.
// Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: the game lookups (keys, known materials, item prefabs) are behind ITradeContext so the rules are testable.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Barter.Lists;

namespace ExtraPotions.Barter.Availability
{
    /// <summary>What Barter asks the game: world and player keys, which materials the player knows, and an item's stack size.</summary>
    public interface ITradeContext
    {
        bool HasGlobalKey(string key);

        bool HasPlayerKey(string key);

        /// <summary>
        /// The player has met the item with this prefab name. The game tracks known materials by the item's shared name (its
        /// $item_ token), so the plugin resolves the prefab to its item first and asks the player about that name.
        /// </summary>
        bool IsKnown(string prefab);

        /// <summary>The item's maximum stack size, or null when no item has this prefab name.</summary>
        int? MaxStack(string prefab);
    }

    /// <summary>The discovery rule: a trader does not sell items the player has not met, except those on the ignore list.</summary>
    public sealed class DiscoveryRule
    {
        public static readonly DiscoveryRule Off = new DiscoveryRule(false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        public DiscoveryRule(bool sellOnlyDiscovered, ISet<string> ignored)
        {
            SellOnlyDiscovered = sellOnlyDiscovered;
            Ignored = ignored ?? throw new ArgumentNullException(nameof(ignored));
        }

        public bool SellOnlyDiscovered { get; }

        /// <summary>Prefabs offered even when undiscovered (compared without regard to case).</summary>
        public ISet<string> Ignored { get; }

        public static DiscoveryRule From(bool sellOnlyDiscovered, string? ignoredList) =>
            new DiscoveryRule(sellOnlyDiscovered, new HashSet<string>(TraderNames.SplitList(ignoredList), StringComparer.OrdinalIgnoreCase));
    }

    public static class EntryAvailability
    {
        /// <summary>The entry's global and player key conditions hold: every required key set, no blocked key set.</summary>
        public static bool RequirementsMet(TradeEntry entry, ITradeContext context)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (TradeEntry.SplitKeys(entry.RequiredGlobalKey).Any(key => !context.HasGlobalKey(key)))
                return false;
            if (TradeEntry.SplitKeys(entry.NotRequiredGlobalKey).Any(context.HasGlobalKey))
                return false;
            if (TradeEntry.SplitKeys(entry.RequiredPlayerKey).Any(key => !context.HasPlayerKey(key)))
                return false;
            if (TradeEntry.SplitKeys(entry.NotRequiredPlayerKey).Any(context.HasPlayerKey))
                return false;
            return true;
        }

        /// <summary>A Buy entry the trader may offer now: a real item, a lot and a price, its requirements met, and the discovery rule satisfied.</summary>
        public static bool CanOffer(TradeEntry entry, ITradeContext context, DiscoveryRule discovery)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (discovery == null) throw new ArgumentNullException(nameof(discovery));

            if (string.IsNullOrWhiteSpace(entry.Prefab) || entry.Stack <= 0 || entry.Price <= 0)
                return false;
            if (!RequirementsMet(entry, context))
                return false;
            if (context.MaxStack(entry.Prefab) == null)
                return false;
            return !discovery.SellOnlyDiscovered || discovery.Ignored.Contains(entry.Prefab) || context.IsKnown(entry.Prefab);
        }

        /// <summary>A Sell entry that can set a price now: a real item, a lot and a price, requirements met; an item that does not stack only prices as one.</summary>
        public static bool CanPrice(TradeEntry entry, ITradeContext context)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (string.IsNullOrWhiteSpace(entry.Prefab) || entry.Stack <= 0 || entry.Price <= 0)
                return false;
            if (!RequirementsMet(entry, context))
                return false;
            int? maxStack = context.MaxStack(entry.Prefab);
            if (maxStack == null)
                return false;
            return maxStack.Value != 1 || entry.Stack == 1;
        }
    }
}
