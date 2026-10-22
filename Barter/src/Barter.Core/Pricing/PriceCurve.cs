// Adapted from TradersExtended 2.0.5 (shudnal), Trading/TraderCoins.cs (GetTraderBuyPriceFactor, GetTraderSellPriceFactor,
// GetPriceRange, RoundFactorToPercent) and UI/StorePanel.cs (GetPriceFactorString), pin 2d5dd879ead70f809f53025d6b28feb3dee7b605.
// Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: pure functions of the balance and PurseRules (no ZDO or Unity Mathf), no factor when the purse is off.
using System;
using System.Globalization;

namespace ExtraPotions.Barter.Pricing
{
    /// <summary>How a trader's balance moves prices. An empty purse makes the trader charge more and pay less; a full one the opposite.</summary>
    public static class PriceCurve
    {
        /// <summary>The factor on what the player pays: <c>Markup</c> at an empty purse, 1 at the minimum balance, <c>Discount</c> at the maximum.</summary>
        public static float BuyFactor(int balance, PurseRules rules)
        {
            if (!rules.Enabled || !rules.Flexible)
                return 1f;

            rules.Range(out int minimum, out int maximum);
            if (balance < minimum)
            {
                float progress = minimum > 0 ? Clamp01((float)balance / minimum) : 1f;
                return RoundToPercent(Lerp(rules.Markup, 1f, progress));
            }

            float upperProgress = Clamp01((float)(balance - minimum) / Math.Max(maximum - minimum, 1));
            return RoundToPercent(Lerp(1f, rules.Discount, upperProgress));
        }

        /// <summary>The factor on what the player is paid: <c>Discount</c> at an empty purse, 1 at the minimum balance, <c>Markup</c> at the maximum.</summary>
        public static float SellFactor(int balance, PurseRules rules)
        {
            if (!rules.Enabled || !rules.Flexible)
                return 1f;

            rules.Range(out int minimum, out int maximum);
            if (balance < minimum)
            {
                float progress = minimum > 0 ? Clamp01((float)balance / minimum) : 1f;
                return RoundToPercent(Lerp(rules.Discount, 1f, progress));
            }

            float upperProgress = Clamp01((float)(balance - minimum) / Math.Max(maximum - minimum, 1));
            return RoundToPercent(Lerp(1f, rules.Markup, upperProgress));
        }

        /// <summary>A factor to whole percentage points; a factor that is not a finite non-negative number reads as 1.</summary>
        public static float RoundToPercent(float factor)
        {
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor < 0f)
                return 1f;
            return (float)(Math.Round(factor * 100d) / 100d);
        }

        /// <summary>
        /// The percentage change of a factor as text ("+25", "-15"; empty for 1) and whether it favours the player.
        /// <paramref name="reversed"/> is true for a buy factor, where a lower number is better.
        /// </summary>
        public static string Describe(float factor, bool reversed, out bool favourable)
        {
            favourable = true;
            if (factor == 1f)
                return string.Empty;

            favourable = (reversed && factor < 1f) || (!reversed && factor > 1f);
            double percent = (factor - 1f) * 100f;
            return (percent >= 0 ? "+" : "-") + Math.Abs(Math.Round(percent)).ToString("0", CultureInfo.InvariantCulture);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
