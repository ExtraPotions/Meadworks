// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtraPotions.Barter.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExtraPotions.Barter.Runtime
{
    internal static class TraderPolicyJson
    {
        internal const int MaximumBytes = 262144;
        internal static TraderPolicyBook Read(string text)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(text) > MaximumBytes) throw new InvalidDataException("Trader policies exceed 256 KiB.");
            using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, MaxDepth = 12 };
            var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("Unexpected content after trader policies.");
            if (root.Properties().Count() != 2 || root["version"]?.Type != JTokenType.Integer || root["version"]!.Value<int>() != TraderPolicyBook.Version || root["traders"] is not JObject traders)
                throw new InvalidDataException("Expected version 1 and a traders object.");
            var policies = new Dictionary<string, IDictionary<string, object>>(StringComparer.Ordinal);
            foreach (var trader in traders.Properties())
            {
                if (trader.Value is not JObject values) throw new InvalidDataException("Expected trader settings object.");
                var settings = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var pair in values.Properties())
                {
                    if (pair.Value is not JValue scalar || scalar.Value == null) throw new InvalidDataException("Expected scalar setting values.");
                    settings.Add(pair.Name, scalar.Value);
                }
                policies.Add(trader.Name, settings);
            }
            return new TraderPolicyBook(policies);
        }

        internal static string Write(TraderPolicyBook book) => JsonConvert.SerializeObject(new { version = TraderPolicyBook.Version, traders = book.Traders }, Formatting.Indented);
    }
}
