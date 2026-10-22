// Adapted from TradersExtended 2.0.5 (shudnal), Trading/TradeAmounts.cs, pin 2d5dd879ead70f809f53025d6b28feb3dee7b605.
// Most of the file is the upstream arithmetic unchanged; the factor-to-decimal step differs (see below), so it is recorded as adapted.
// The upstream project is released into the public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: namespace, public accessibility, the stack encoder defaults to the Barter constant, XML summaries, and
// the factor reaches decimal through its shortest round-trip text (ToDecimal). The double-to-decimal cast rounds differently on
// different runtimes: on current .NET (decimal)0.7 is 0.69999999999999995, which floors 100 * 0.7 to 69. The text route is the same
// everywhere and gives 70. It has been run on current .NET only; behaviour on the game's Mono runtime is not yet verified.
using System;
using System.Globalization;

namespace ExtraPotions.Barter.Trading
{
    /// <summary>Whole trade-lot arithmetic, independent of the UI and inventory implementation.</summary>
    public static class TradeAmounts
    {
        /// <summary>Unity's Slider stores floats; every integer up to this limit is represented exactly.</summary>
        public const int MaximumSliderLots = 1 << 24;

        /// <summary>Items in <paramref name="lots"/> lots of <paramref name="lotSize"/>; false when empty or beyond Int32.</summary>
        public static bool TryGetItemCount(int lotSize, int lots, out int amount)
        {
            long total = (long)lotSize * lots;
            amount = 0;
            if (lotSize <= 0 || lots <= 0 || total > int.MaxValue)
                return false;
            amount = (int)total;
            return true;
        }

        public static bool TryGetPrice(int pricePerLot, int lots, float factor, out int price, bool roundUp = true)
        {
            // Trader factors are rounded to whole percentage points before reaching this boundary.
            // Do not promote a binary float approximation (for example 0.99f) into a pricing error.
            return TryGetPrice(pricePerLot, lots, Math.Round((double)factor, 2), out price, roundUp);
        }

        /// <summary>Price of <paramref name="lots"/> lots at a factor: rounded up (a purchase) or down (a sale), never below 1.</summary>
        public static bool TryGetPrice(int pricePerLot, int lots, double factor, out int price, bool roundUp = true)
        {
            price = 0;
            if (pricePerLot <= 0 || lots <= 0 || double.IsNaN(factor) || double.IsInfinity(factor) || factor < 0d || factor > int.MaxValue)
                return false;

            // All three factors are bounded by Int32.MaxValue, so their product fits Decimal.
            decimal product = (decimal)pricePerLot * lots * ToDecimal(factor);
            decimal total = Math.Max(roundUp ? decimal.Ceiling(product) : decimal.Floor(product), 1m);
            if (total > int.MaxValue)
                return false;
            price = (int)total;
            return true;
        }

        /// <summary>Price of an item above quality 1: the base price plus <paramref name="multiplier"/> of it per extra quality level.</summary>
        public static bool TryGetQualityPrice(int basePrice, int quality, float multiplier, out int price)
        {
            price = 0;
            if (basePrice <= 0 || quality <= 0 || float.IsNaN(multiplier) || float.IsInfinity(multiplier) ||
                Math.Abs((double)multiplier) > int.MaxValue)
                return false;
            decimal total = basePrice * (1m + ToDecimal(multiplier) * ((long)quality - 1));
            if (total < 1m || total > int.MaxValue)
                return false;
            price = (int)total;
            return true;
        }

        /// <summary>The decimal a double or float means as written ("0.7"), the same on every runtime.</summary>
        public static decimal ToDecimal(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) < 1e-9)
                return 0m;
            return decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public static decimal ToDecimal(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) < 1e-9f)
                return 0m;
            return decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public static int MaximumBuyLots(int lotSize, int pricePerLot, int currency, int capacity)
        {
            if (lotSize <= 0 || pricePerLot <= 0 || currency <= 0 || capacity <= 0)
                return 0;
            return Math.Min(MaximumSliderLots, Math.Min(capacity / lotSize, currency / pricePerLot));
        }

        public static int MaximumSellLots(int lotSize, int pricePerLot, int availableItems, float factor, int budget)
        {
            return MaximumSellLots(lotSize, pricePerLot, availableItems, Math.Round((double)factor, 2), budget);
        }

        /// <summary>The most lots the player can sell: limited by the items held and by the trader's balance (<paramref name="budget"/>).</summary>
        public static int MaximumSellLots(int lotSize, int pricePerLot, int availableItems, double factor, int budget)
        {
            if (lotSize <= 0 || pricePerLot <= 0 || availableItems <= 0 || budget <= 0)
                return 0;

            int low = 0;
            int high = Math.Min(MaximumSliderLots, availableItems / lotSize);
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (TryGetPrice(pricePerLot, middle, factor, out int price, roundUp: false) && price <= budget)
                    low = middle;
                else
                    high = middle - 1;
            }
            return low;
        }

        public static bool TryEncodeStack(int stack, int quality, int multiplier, out int encoded)
        {
            encoded = 0;
            long value = stack + (long)multiplier * quality;
            if (multiplier <= 1 || stack <= 0 || stack >= multiplier || quality < 0 || value > int.MaxValue)
                return false;
            encoded = (int)value;
            return true;
        }

        /// <summary>Encodes a stack and quality with Barter's multiplier (see <see cref="BarterCore.QualityStackMultiplier"/>).</summary>
        public static bool TryEncodeStack(int stack, int quality, out int encoded) =>
            TryEncodeStack(stack, quality, BarterCore.QualityStackMultiplier, out encoded);

        /// <summary>Splits a stack encoded by <see cref="TryEncodeStack(int,int,out int)"/> back into stack and quality.</summary>
        public static void DecodeStack(int encoded, out int stack, out int quality)
        {
            stack = encoded % BarterCore.QualityStackMultiplier;
            quality = encoded / BarterCore.QualityStackMultiplier;
        }

        public static int ClampBalance(long amount)
        {
            return (int)Math.Max(0L, Math.Min(int.MaxValue, amount));
        }
    }
}
