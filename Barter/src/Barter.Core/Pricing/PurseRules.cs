// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Barter.Config;

namespace ExtraPotions.Barter.Pricing
{
    /// <summary>The purse settings of one trader (or of all traders): the limits, the daily change and the price curve ends.</summary>
    public sealed class PurseRules
    {
        /// <summary>Traders have a limited balance that moves with every trade.</summary>
        public bool Enabled { get; set; }

        /// <summary>Prices move with the trader's balance.</summary>
        public bool Flexible { get; set; }

        public int Minimum { get; set; }

        public int Maximum { get; set; }

        public int DailyIncrease { get; set; }

        public int DailyDecrease { get; set; }

        /// <summary>Buy-price factor at the maximum balance; the sell-price factor at an empty purse.</summary>
        public float Discount { get; set; }

        /// <summary>Buy-price factor at an empty purse; the sell-price factor at the maximum balance.</summary>
        public float Markup { get; set; }

        public int ReplenishDays { get; set; }

        /// <summary>The settings' defaults, the reference's: 2000 to 6000 coins, +1000 a day, discount 0.7, markup 1.5.</summary>
        public static PurseRules Defaults() => new PurseRules
        {
            Enabled = BarterSettings.Purse.Enabled.Default,
            Flexible = BarterSettings.Purse.FlexiblePricing.Default,
            Minimum = BarterSettings.Purse.MinimumBalance.Default,
            Maximum = BarterSettings.Purse.MaximumBalance.Default,
            DailyIncrease = BarterSettings.Purse.DailyIncrease.Default,
            DailyDecrease = BarterSettings.Purse.DailyDecrease.Default,
            Discount = BarterSettings.Purse.Discount.Default,
            Markup = BarterSettings.Purse.Markup.Default,
            ReplenishDays = BarterSettings.Purse.ReplenishDays.Default,
        };

        /// <summary>The minimum is never negative; the maximum is never below it.</summary>
        internal void Range(out int minimum, out int maximum)
        {
            minimum = System.Math.Max(Minimum, 0);
            maximum = System.Math.Max(Maximum, minimum);
        }
    }
}
