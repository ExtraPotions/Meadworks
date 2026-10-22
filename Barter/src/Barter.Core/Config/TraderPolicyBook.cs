// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ExtraPotions.Barter.Lists;

namespace ExtraPotions.Barter.Config
{
    public sealed class TraderPolicyBook
    {
        public const int Version = 1;
        public const int MaximumTraders = 256;
        public const string CurrencyKey = "Currency";
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> traders;
        public static readonly TraderPolicyBook Empty = new TraderPolicyBook(new Dictionary<string, IDictionary<string, object>>());

        public TraderPolicyBook(IDictionary<string, IDictionary<string, object>> input)
        {
            if (input == null || input.Count > MaximumTraders) throw new ArgumentException("Too many trader policies.");
            var result = new Dictionary<string, IReadOnlyDictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            foreach (var trader in input)
            {
                string name = TraderNames.Normalize(trader.Key);
                if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name == BarterCore.CommonTrader ||
                    trader.Key.Any(c => char.IsControl(c) || "/\\:".IndexOf(c) >= 0) || result.ContainsKey(name))
                    throw new ArgumentException("Invalid or duplicate trader policy: " + trader.Key);
                var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in trader.Value)
                {
                    string key;
                    object value;
                    if (pair.Key == CurrencyKey)
                    {
                        if (!(pair.Value is string text) || text.Length > 128 || text.Any(c => char.IsWhiteSpace(c) || "/\\,:;=".IndexOf(c) >= 0))
                            throw new ArgumentException("Expected a currency prefab name.");
                        key = CurrencyKey;
                        value = text;
                    }
                    else
                    {
                        BarterSetting setting = BarterSettings.Find(pair.Key) ?? throw new ArgumentException("Unknown trader setting: " + pair.Key);
                        if (setting.Scope != SettingScope.ServerAuthoritative || setting == BarterSettings.Lists.TraderCurrencies ||
                            setting == BarterSettings.Lists.CustomTraders) throw new ArgumentException("Not a per-trader setting: " + pair.Key);
                        key = setting.Id;
                        value = Validate(setting, pair.Value);
                    }
                    if (values.ContainsKey(key)) throw new ArgumentException("Duplicate trader setting: " + key);
                    values.Add(key, value);
                }
                result.Add(name, new ReadOnlyDictionary<string, object>(values));
            }
            traders = new ReadOnlyDictionary<string, IReadOnlyDictionary<string, object>>(result);
        }

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> Traders => traders;
        public T Get<T>(string trader, BarterSetting<T> setting, T fallback) =>
            traders.TryGetValue(TraderNames.Normalize(trader), out var values) && values.TryGetValue(setting.Id, out object value)
                ? (T)value : fallback;
        public string Currency(string trader, string fallback) =>
            traders.TryGetValue(TraderNames.Normalize(trader), out var values) && values.TryGetValue(CurrencyKey, out object value)
                ? (string)value : fallback;

        private static object Validate(BarterSetting setting, object value)
        {
            object typed;
            if (setting.ValueType == typeof(bool) && value is bool) typed = value;
            else if (setting.ValueType == typeof(string) && value is string s && s.Length <= 16384) typed = s;
            else if (setting.ValueType == typeof(int) && (value is int || value is long))
                typed = checked(Convert.ToInt32(value, CultureInfo.InvariantCulture));
            else if (setting.ValueType == typeof(float) && (value is float || value is double || value is decimal || value is int || value is long))
                typed = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            else throw new ArgumentException("Wrong value type for " + setting.Id);
            if (!Equals(setting.NormalizeBoxed(typed), typed)) throw new ArgumentException("Value outside supported range for " + setting.Id);
            return typed;
        }
    }
}
