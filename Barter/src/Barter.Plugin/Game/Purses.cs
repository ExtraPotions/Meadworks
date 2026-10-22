// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behaviour adapted from TradersExtended 2.0.5 (Unlicense, pin 2d5dd879) Trading/TraderCoins.cs.
// Authority state lives on server disk; the original ZDO keys are display mirrors only.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ExtraPotions.Barter.Config;
using ExtraPotions.Barter.Lists;
using ExtraPotions.Barter.Pricing;
using ExtraPotions.Barter.Trading;
using HarmonyLib;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>
    /// A trader's purse: its durable server balance, the price factors it gives, and the morning refill. Only the vanilla traders, the
    /// custom traders in the settings and traders with a Barter list have a purse; any other trader keeps neutral prices.
    /// </summary>
    internal static class Purses
    {
        private static readonly int BalanceKey = "traderCoins".GetStableHashCode();
        private static readonly int DayKey = "traderCoinsReplenished".GetStableHashCode();
        private static readonly int LedgerKey = "meadworks.barter.ledger.v1".GetStableHashCode();

        internal static bool HasPurse(string trader)
        {
            if (string.IsNullOrEmpty(trader) || Plugin.Settings == null)
                return false;
            if (TraderNames.Vanilla.Any(name => TraderNames.Normalize(name) == trader))
                return true;
            if (TraderNames.ListContains(Plugin.Settings.Get(BarterSettings.Lists.CustomTraders), trader))
                return true;
            return TradeLists.Written.Traders.Contains(trader) || TraderPolicies.HasTrader(trader);
        }

        /// <summary>The purse rules for a trader; disabled when the trader has no purse.</summary>
        internal static PurseRules Rules(string trader)
        {
            PurseRules rules = Plugin.Settings.Purse();
            rules.Enabled = TraderPolicies.Get(BarterSettings.Purse.Enabled, trader, rules.Enabled);
            rules.Flexible = TraderPolicies.Get(BarterSettings.Purse.FlexiblePricing, trader, rules.Flexible);
            rules.Minimum = TraderPolicies.Get(BarterSettings.Purse.MinimumBalance, trader, rules.Minimum);
            rules.Maximum = TraderPolicies.Get(BarterSettings.Purse.MaximumBalance, trader, rules.Maximum);
            rules.DailyIncrease = TraderPolicies.Get(BarterSettings.Purse.DailyIncrease, trader, rules.DailyIncrease);
            rules.DailyDecrease = TraderPolicies.Get(BarterSettings.Purse.DailyDecrease, trader, rules.DailyDecrease);
            rules.Discount = TraderPolicies.Get(BarterSettings.Purse.Discount, trader, rules.Discount);
            rules.Markup = TraderPolicies.Get(BarterSettings.Purse.Markup, trader, rules.Markup);
            rules.ReplenishDays = TraderPolicies.Get(BarterSettings.Purse.ReplenishDays, trader, rules.ReplenishDays);
            if (!HasPurse(trader))
                rules.Enabled = false;
            return rules;
        }

        internal static bool Applies(Trader trader) => Rules(TradeWorld.TraderName(trader)).Enabled;

        /// <summary>The trader's balance now (the starting balance when it has never been stored).</summary>
        internal static int Balance(Trader trader, ZDO authorityZdo = null)
        {
            PurseRules rules = Rules(TradeWorld.TraderName(trader));
            int initial = PurseRefill.Initial(rules);
            if (authorityZdo != null) return ReadLedger(authorityZdo, initial).Balance;
            ZNetView view = trader != null ? trader.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid())
                return initial;
            return ReadLedger(view.GetZDO(), initial).Balance;
        }

        internal static float BuyFactor(Trader trader, ZDO authorityZdo = null)
        {
            PurseRules rules = Rules(TradeWorld.TraderName(trader));
            return rules.Enabled ? PriceCurve.BuyFactor(Balance(trader, authorityZdo), rules) : 1f;
        }

        internal static float SellFactor(Trader trader, ZDO authorityZdo = null)
        {
            PurseRules rules = Rules(TradeWorld.TraderName(trader));
            return rules.Enabled ? PriceCurve.SellFactor(Balance(trader, authorityZdo), rules) : 1f;
        }

        /// <summary>The trader can pay out <paramref name="amount"/>.</summary>
        internal static bool CanPay(Trader trader, int amount) => amount > 0 && (!Applies(trader) || amount <= Balance(trader));

        /// <summary>Legacy callers cannot mutate a purse outside the authenticated trade protocol.</summary>
        internal static void Change(Trader trader, long delta)
        {
            if (delta != 0) Plugin.Log?.Warning("Ignored purse write outside the trade authority protocol.");
        }

        internal static PurseLedger ReadLedger(ZDO zdo, int initial)
        {
            string encoded = AuthorityState.Read(zdo, "ledger", zdo.GetString(LedgerKey, ""));
            var ledger = encoded.Length == 0 ? new PurseLedger(initial) : PurseLedger.Decode(encoded);
            if (AuthorityState.IsAuthority)
            {
                if (encoded.Length == 0)
                {
                    // Preserve unsigned migration evidence for the host, without accepting a peer-writable balance.
                    AuthorityState.Write(zdo, "untrusted-legacy-ledger", zdo.GetString(LedgerKey, "") + "\n" +
                        zdo.GetInt(BalanceKey, initial).ToString(CultureInfo.InvariantCulture));
                    AuthorityState.Write(zdo, "ledger", ledger.Encode());
                }
                if (ledger.QuarantineExpired(DateTimeOffset.UtcNow.ToUnixTimeSeconds())) WriteLedger(zdo, ledger);
                Mirror(zdo, ledger);
            }
            return ledger;
        }

        internal static void WriteLedger(ZDO zdo, PurseLedger ledger)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) throw new InvalidOperationException("Only the server writes purse state.");
            AuthorityState.Write(zdo, "ledger", ledger.Encode());
            Mirror(zdo, ledger);
        }

        private static void Mirror(ZDO zdo, PurseLedger ledger)
        {
            zdo.SetOwner(ZDOMan.GetSessionID());
            string encoded = ledger.Encode();
            if (zdo.GetString(LedgerKey, "") != encoded) zdo.Set(LedgerKey, encoded);
            if (zdo.GetInt(BalanceKey, -1) != ledger.Balance) zdo.Set(BalanceKey, ledger.Balance);
        }

        private static int RefillDay(ZDO zdo)
        {
            string day = AuthorityState.Read(zdo, "refill-day");
            if (day.Length == 0) return 0;
            if (!int.TryParse(day, NumberStyles.None, CultureInfo.InvariantCulture, out int result) || result < 0)
                throw new InvalidDataException("Invalid authority refill day");
            return result;
        }

        /// <summary>The morning refill; run on the machine that owns the world. True when any balance changed.</summary>
        internal static bool Refill()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || !EnvMan.instance || !ZNetScene.instance || ZDOMan.instance == null || Plugin.Settings == null)
                return false;

            bool message = false;
            int today = EnvMan.instance.GetCurrentDay();
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values.Where(zdo => zdo != null))
            {
                string prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab())?.name;
                string trader = TraderNames.Normalize(prefab);
                if (string.IsNullOrEmpty(prefab) || !HasPurse(trader))
                    continue;

                PurseRules rules = Rules(trader);
                if (!rules.Enabled)
                    continue;

                PurseLedger ledger = ReadLedger(zdo, PurseRefill.Initial(rules));
                if (ledger.Pending) continue;
                int current = ledger.Balance;
                if (!PurseRefill.TryReplenish(current, RefillDay(zdo), today, rules, out int next))
                    continue;

                if (!ledger.Refill(next)) continue;
                // Stamp first: a failed balance commit may under-refill, but never grants the same day's refill twice.
                AuthorityState.Write(zdo, "refill-day", today.ToString(CultureInfo.InvariantCulture));
                WriteLedger(zdo, ledger);
                zdo.Set(DayKey, today);
                message |= TraderPolicies.Get(BarterSettings.Purse.ReplenishMessage, trader, Plugin.Settings.Get(BarterSettings.Purse.ReplenishMessage));
                Plugin.Log?.Verbose($"{prefab} balance updated {current} -> {next}");
            }
            return message;
        }
    }
}
