// Adapted from TradersExtended 2.0.5 (shudnal), Trading/TraderCoins.cs (UpdateTradersCoinsDaily, GetTraderCoins),
// pin 2d5dd879ead70f809f53025d6b28feb3dee7b605. Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: the arithmetic of one trader's daily update as a pure function; the ZDO reads and writes stay in the plugin.
using System;
using ExtraPotions.Barter.Trading;

namespace ExtraPotions.Barter.Pricing
{
    public static class PurseRefill
    {
        /// <summary>The balance a trader starts with before it has ever been updated: the minimum.</summary>
        public static int Initial(PurseRules rules)
        {
            rules.Range(out int minimum, out _);
            return minimum;
        }

        /// <summary>
        /// One trader's morning update. Returns false when nothing is due (too few days since the last update, or the purse is
        /// at its maximum and nothing drains it); otherwise <paramref name="next"/> is the new balance and the day is stamped.
        /// Below the maximum the purse gains <see cref="PurseRules.DailyIncrease"/> up to the maximum and never ends under the minimum;
        /// at or above it the purse loses <see cref="PurseRules.DailyDecrease"/> but not below the maximum.
        /// </summary>
        public static bool TryReplenish(int current, int lastDay, int today, PurseRules rules, out int next)
        {
            next = current;
            if (today - lastDay < Math.Max(rules.ReplenishDays, 1))
                return false;

            rules.Range(out int minimum, out int maximum);
            if (current >= maximum && rules.DailyDecrease <= 0)
                return false;

            next = current >= maximum
                ? Math.Max(maximum, current - Math.Max(rules.DailyDecrease, 0))
                : Math.Max(minimum, Math.Min(maximum, TradeAmounts.ClampBalance((long)current + rules.DailyIncrease)));
            return true;
        }
    }
}
