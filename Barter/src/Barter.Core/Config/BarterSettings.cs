// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// The purse and list settings and their defaults follow TradersExtended 2.0.5 (Unlicense, pin 2d5dd879); the names and ranges are Barter's.
using System;
using System.Collections.Generic;
using System.Linq;
using static ExtraPotions.Barter.Config.SettingScope;

namespace ExtraPotions.Barter.Config
{
    /// <summary>
    /// Every Barter setting, in file order. The plugin binds exactly this list. Trader lists and currencies change shared rules,
    /// so everything that shapes a trade is ServerAuthoritative: the server's value wins in multiplayer and shows as read-only.
    /// Only how the panels look and behave for one player is Local.
    /// </summary>
    public static class BarterSettings
    {
        public static class Purse
        {
            public const string Section = "Trader Purse";

            public static readonly BarterSetting<bool> Enabled = BarterSetting.Toggle(Section, "Enabled", true, ServerAuthoritative,
                "Traders that have a Barter list (Haldor, Hildir and the Bog Witch with the shipped lists, Custom Traders, and any trader you write a list for) have a limited balance that goes down when they pay you and up when you buy, and refills each morning. Other mods' traders keep the game's prices.");
            public static readonly BarterSetting<bool> FlexiblePricing = BarterSetting.Toggle(Section, "Flexible Pricing", true, ServerAuthoritative,
                "Prices follow the trader's balance: a poor trader charges more and pays less, a rich one the opposite.");
            public static readonly BarterSetting<int> MinimumBalance = BarterSetting.Integer(Section, "Minimum Balance", 2000, 0, 1000000, ServerAuthoritative,
                "The balance a trader starts with and never refills below; prices are neutral at this balance.");
            public static readonly BarterSetting<int> MaximumBalance = BarterSetting.Integer(Section, "Maximum Balance", 6000, 0, 1000000, ServerAuthoritative,
                "The balance a trader refills up to; prices reach the discount and the markup at this balance.");
            public static readonly BarterSetting<int> DailyIncrease = BarterSetting.Integer(Section, "Daily Increase", 1000, 0, 1000000, ServerAuthoritative,
                "How much a trader's balance grows at each refill, up to the maximum.");
            public static readonly BarterSetting<int> DailyDecrease = BarterSetting.Integer(Section, "Daily Decrease", 0, 0, 1000000, ServerAuthoritative,
                "How much a balance above the maximum shrinks at each refill, down to the maximum.");
            public static readonly BarterSetting<float> Discount = BarterSetting.Number(Section, "Discount", 0.7f, 0f, 10f, ServerAuthoritative,
                "The price you pay at the maximum balance, as a factor (0.7 is 30 percent off); also what an empty purse pays you.");
            public static readonly BarterSetting<float> Markup = BarterSetting.Number(Section, "Markup", 1.5f, 0f, 10f, ServerAuthoritative,
                "The price you pay at an empty purse, as a factor (1.5 is 50 percent more); also what a full purse pays you.");
            public static readonly BarterSetting<int> ReplenishDays = BarterSetting.Integer(Section, "Replenish Days", 1, 1, 365, ServerAuthoritative,
                "How many in-game days pass between refills of a trader's balance.");
            public static readonly BarterSetting<bool> ReplenishMessage = BarterSetting.Toggle(Section, "Replenish Message", true, ServerAuthoritative,
                "Tell everyone in the morning when trader balances were refilled.");
        }

        public static class Lists
        {
            public const string Section = "Trade Lists";

            public static readonly BarterSetting<bool> SellOnlyDiscovered = BarterSetting.Toggle(Section, "Sell Only Discovered", true, ServerAuthoritative,
                "A trader does not offer items you have not discovered yet.");
            public static readonly BarterSetting<string> AlwaysOffered = BarterSetting.Text(Section, "Always Offered", "", ServerAuthoritative,
                "Comma list of item prefabs a trader offers even when you have not discovered them. Items the game's own traders sell are always included.",
                Trim);
            public static readonly BarterSetting<bool> DisableVanillaItems = BarterSetting.Toggle(Section, "Disable Vanilla Items", false, ServerAuthoritative,
                "Remove the game's own items from trader buy lists, so only Barter's lists remain. Progress purchases such as inventory rows stay.");
            public static readonly BarterSetting<bool> DisableOtherModsItems = BarterSetting.Toggle(Section, "Disable Other Mods Items", false, ServerAuthoritative,
                "Remove items other mods added to trader buy lists before Barter adds its own.");
            public static readonly BarterSetting<float> QualityPriceMultiplier = BarterSetting.Number(Section, "Quality Price Multiplier", 0f, 0f, 10f, ServerAuthoritative,
                "Extra price per quality level above one when you sell gear, as a share of the listed price (0.5 adds half per level). 0 ignores quality.");
            public static readonly BarterSetting<bool> CommonValuables = BarterSetting.Toggle(Section, "Common Valuables", true, ServerAuthoritative,
                "Every item the game gives a value can be sold to any trader at that value, unless a Sell list prices it.");
            public static readonly BarterSetting<string> CustomTraders = BarterSetting.Text(Section, "Custom Traders", "", ServerAuthoritative,
                "Comma list of extra trader prefab names (case sensitive in the game) that get a balance and use Barter's lists.",
                Trim);
            public static readonly BarterSetting<string> TraderCurrencies = BarterSetting.Text(Section, "Trader Currencies", "", ServerAuthoritative,
                "Comma list of Trader:Currency pairs, for example Haldor:Coins,Hildir:SilverCoin. The item is that trader's currency unless a list entry names its own.",
                Trim);
        }

        public static class Services
        {
            public const string Section = "Trader Services";
            public static readonly BarterSetting<bool> BuybackEnabled = BarterSetting.Toggle(Section, "Buyback Enabled", true, ServerAuthoritative,
                "Keep exact sold items available to their seller until the next in-game day. The server owns receipts; buying back costs the original payout.");
            public static readonly BarterSetting<bool> RepairEnabled = BarterSetting.Toggle(Section, "Repair Enabled", true, ServerAuthoritative,
                "Let traders repair carried equipment for their own currency. The server owns eligibility and prices.");
            public static readonly BarterSetting<int> RepairBasePrice = BarterSetting.Integer(Section, "Repair Base Price", 10, 1, 1000000, ServerAuthoritative,
                "Base price for repairing a fully damaged quality-one item; scales with quality, missing durability and the trader's buy price.");
        }

        public static class Interface
        {
            public const string Section = "Interface";

            public static readonly BarterSetting<bool> PriceTips = BarterSetting.Toggle(Section, "Price Tips", true, Local,
                "Append this trader's current effective buy and sell prices to item tooltips while trading. Prices are quotes and may change before a trade.");
            public static readonly BarterSetting<bool> SellPanel = BarterSetting.Toggle(Section, "Sell Panel", true, Local,
                "Show the sell list beside the trader's window.");
            public static readonly BarterSetting<bool> ShowPurse = BarterSetting.Toggle(Section, "Show Purse", true, Local,
                "Show the trader's balance, currency and price change beside the trader's window.");
            public static readonly BarterSetting<bool> HideEquipped = BarterSetting.Toggle(Section, "Hide Equipped Items", true, Local,
                "Leave unequipped hotbar gear out of the sell list. Equipped gear and protected extra slots are always kept.");
        }

        // A holder normalizes its defaults through this containing type. Defer catalogue
        // enumeration until all holders are initialized, including under Unity's Mono runtime.
        private static readonly Lazy<BarterSetting[]> all = new Lazy<BarterSetting[]>(() => new BarterSetting[]
        {
            Purse.Enabled, Purse.FlexiblePricing, Purse.MinimumBalance, Purse.MaximumBalance, Purse.DailyIncrease,
            Purse.DailyDecrease, Purse.Discount, Purse.Markup, Purse.ReplenishDays, Purse.ReplenishMessage,
            Lists.SellOnlyDiscovered, Lists.AlwaysOffered, Lists.DisableVanillaItems, Lists.DisableOtherModsItems,
            Lists.QualityPriceMultiplier, Lists.CommonValuables, Lists.CustomTraders, Lists.TraderCurrencies,
            Services.BuybackEnabled, Services.RepairEnabled, Services.RepairBasePrice, Interface.PriceTips,
            Interface.SellPanel, Interface.ShowPurse, Interface.HideEquipped,
        });

        private static readonly Lazy<Dictionary<string, BarterSetting>> byId = new Lazy<Dictionary<string, BarterSetting>>(
            () => all.Value.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase));

        /// <summary>Every setting in file order.</summary>
        public static IReadOnlyList<BarterSetting> All => all.Value;

        /// <summary>A setting by <c>Section.Key</c> in any case; null when unknown.</summary>
        public static BarterSetting? Find(string? id) => id != null && byId.Value.TryGetValue(id.Trim(), out var setting) ? setting : null;

        private static string Trim(string? text) => (text ?? string.Empty).Trim();
    }
}
