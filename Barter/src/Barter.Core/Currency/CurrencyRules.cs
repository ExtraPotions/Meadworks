// Adapted from TradersExtended 2.0.5 (shudnal), Configuration/TraderConfig.cs (ResolveCurrencyForTrader) and
// Trading/TraderCurrency.cs (RegisterCurrency), pin 2d5dd879ead70f809f53025d6b28feb3dee7b605.
// Public domain under the Unlicense (licenses/Unlicense-TradersExtended.txt).
// Changed by expDARE, 2026: pure functions that return the warning instead of logging it.
using System;
using ExtraPotions.Barter.Lists;

namespace ExtraPotions.Barter.Currency
{
    /// <summary>Which item a trader trades in: an entry's own currency, else the trader's, else the vanilla coin.</summary>
    public static class CurrencyRules
    {
        /// <summary>
        /// The currency a "Trader:Currency" list (comma, semicolon or line separated; = also works) names for one trader, or empty.
        /// <paramref name="warning"/> is set when the text is a bare name that is not a pair.
        /// </summary>
        public static string ForTrader(string? overrides, string? trader, out string? warning)
        {
            warning = null;
            if (string.IsNullOrWhiteSpace(overrides))
                return string.Empty;

            string direct = overrides!.Trim();
            if (direct.IndexOf(':') < 0 && direct.IndexOf('=') < 0 &&
                direct.IndexOf(',') < 0 && direct.IndexOf(';') < 0 &&
                direct.IndexOf('\n') < 0 && direct.IndexOf('\r') < 0)
            {
                warning = $"Ignored invalid trader currency override '{direct}'. Expected TraderPrefab:CurrencyPrefab.";
                return string.Empty;
            }

            string wanted = TraderNames.Normalize(trader);
            foreach (string rawEntry in direct.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string entry = rawEntry.Trim();
                int separator = entry.IndexOf(':');
                if (separator < 0)
                    separator = entry.IndexOf('=');
                if (separator <= 0 || separator >= entry.Length - 1)
                    continue;

                if (string.Equals(TraderNames.Normalize(entry.Substring(0, separator)), wanted, StringComparison.OrdinalIgnoreCase))
                    return entry.Substring(separator + 1).Trim();
            }

            return string.Empty;
        }

        /// <summary>The currency an entry is priced in: its own when it names one, else the trader's, else empty (the vanilla coin).</summary>
        public static string Effective(string? entryCurrency, string? traderCurrency)
        {
            string own = (entryCurrency ?? string.Empty).Trim();
            return own.Length > 0 ? own : (traderCurrency ?? string.Empty).Trim();
        }

        public static bool IsVanilla(string? currency) =>
            string.IsNullOrWhiteSpace(currency) || string.Equals(currency!.Trim(), BarterCore.VanillaCurrency, StringComparison.OrdinalIgnoreCase);

        public static string PrefabOrVanilla(string? currency) =>
            string.IsNullOrWhiteSpace(currency) ? BarterCore.VanillaCurrency : currency!.Trim();
    }
}
