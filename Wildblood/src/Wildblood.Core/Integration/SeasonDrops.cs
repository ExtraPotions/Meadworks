// Wildblood - ExtraPotions Meadworks creature difficulty.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Wildblood.Integration
{
    /// <summary>Meat and wood rates from one facts line. An inactive channel keeps the game's own amount.</summary>
    public readonly struct SeasonDropRates
    {
        public SeasonDropRates(bool meatActive, float meat, bool woodActive, float wood)
        {
            MeatActive = meatActive;
            Meat = meat;
            WoodActive = woodActive;
            Wood = wood;
        }

        public bool MeatActive { get; }
        public float Meat { get; }
        public bool WoodActive { get; }
        public float Wood { get; }
    }

    /// <summary>
    /// Optional reader for <c>meadworks.seasons.facts</c> 1.0. Wildblood does not reference Solstice.
    /// <c>meat</c> and <c>wood</c> apply only when the line is intact and <c>effects=1</c>.
    /// A published 0 becomes one quarter; fractional reductions are rolled once by the drop's owner.
    /// </summary>
    public static class SeasonDrops
    {
        public const string Id = "meadworks.seasons.facts";
        public static readonly Version Version = new Version(1, 0);
        public const float MinRate = 0.25f;
        public const float MaxRate = 3f;
        public const int StackCap = 100;
        public const int MaxLength = 8192;

        static readonly string[] Blocked = { "Trophy", "Cape", "Armor", "Helmet", "Legs", "Chest", "Arrow", "Bow", "Shield", "Axe", "Sword", "Knife", "Spear", "Mace", "Atgeir", "Saddle" };

        public static SeasonDropRates Read(Func<string>? provider)
        {
            try { return provider != null && TryParse(provider(), out var rates) ? rates : default; }
            catch (Exception) { return default; }
        }

        public static bool TryParse(string? text, out SeasonDropRates rates)
        {
            rates = default;
            if (!Fields(text, out var fields) || !Ready(fields)) return false;
            if (!Channel(fields, "meat", out var meatActive, out var meat) || !Channel(fields, "wood", out var woodActive, out var wood))
                return false;
            rates = new SeasonDropRates(meatActive, meat, woodActive, wood);
            return true;
        }

        public static float ClampDrop(float rate) => rate < MinRate ? MinRate : rate > MaxRate ? MaxRate : rate;

        /// <summary>Below one, round the fractional remainder using a sample in [0, 1). Increases retain ceiling and the stack cap.</summary>
        public static int ScaleAmount(int amount, float rate, double sample)
        {
            if (amount <= 0 || rate == 1f) return amount;
            double product = amount * (double)rate;
            double scaled;
            if (rate < 1f)
            {
                double whole = Math.Floor(product);
                scaled = whole + (sample < product - whole ? 1d : 0d);
            }
            else scaled = Math.Ceiling(product - 1e-9d);
            if (scaled < 0d) return 0;
            int next = scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
            int cap = Math.Max(StackCap, amount);
            return next > cap ? cap : next;
        }

        public static void ScaleRange(int min, int max, float rate, double sample, out int scaledMin, out int scaledMax)
        {
            if (rate == 1f) { scaledMin = min; scaledMax = max; return; }
            scaledMin = ScaleAmount(min, rate, sample);
            scaledMax = ScaleAmount(Math.Max(min, max), rate, sample);
            if (scaledMax < scaledMin) scaledMax = scaledMin;
        }

        public static int Stack(string? name, int amount, float rate, bool enabled, double sample) =>
            enabled && IsMeatOrHide(name) ? ScaleAmount(amount, rate, sample) : amount;

        public static bool IsMeatOrHide(string? name)
        {
            if (string.IsNullOrEmpty(name) || BlockedName(name!)) return false;
            return name!.EndsWith("Meat", StringComparison.Ordinal) || name == "NeckTail"
                || name.IndexOf("Hide", StringComparison.Ordinal) >= 0
                || name.IndexOf("Pelt", StringComparison.Ordinal) >= 0
                || name.IndexOf("Leather", StringComparison.Ordinal) >= 0;
        }

        public static bool IsWood(string? name) =>
            name == "Wood" || name == "FineWood" || name == "RoundLog" || name == "ElderBark" || name == "YggdrasilWood";

        public static string Prefix(long worldId, string sessionId) =>
            "1|" + worldId.ToString(CultureInfo.InvariantCulture) + "|"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(sessionId ?? "")) + "|";

        public static string Wrap(long worldId, string sessionId, string? facts) => Prefix(worldId, sessionId) + (facts ?? "");

        public static bool TryUnwrap(string? carrier, long worldId, string sessionId, out string facts)
        {
            facts = "";
            if (string.IsNullOrEmpty(carrier)) return false;
            var prefix = Prefix(worldId, sessionId);
            if (!carrier.StartsWith(prefix, StringComparison.Ordinal)) return false;
            facts = carrier.Substring(prefix.Length);
            return true;
        }

        static bool BlockedName(string name)
        {
            for (int i = 0; i < Blocked.Length; i++)
                if (name.IndexOf(Blocked[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        static bool Channel(Dictionary<string, string> fields, string key, out bool active, out float rate)
        {
            active = false;
            rate = 1f;
            if (!fields.ContainsKey(key)) return true;
            if (!Number(fields, key, out var value) || value < 0f) return false;
            active = true;
            rate = ClampDrop(value);
            return true;
        }

        static bool Ready(Dictionary<string, string> fields)
        {
            if (!Integer(fields, "effects", out var effects) || effects != 1) return false;
            if (!Integer(fields, "season", out var season) || season < 0 || season > 3) return false;
            return Integer(fields, "day", out var day) && Integer(fields, "length", out var length)
                && length >= 1 && day >= 0 && day <= length;
        }

        static bool Fields(string? text, out Dictionary<string, string> fields)
        {
            fields = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text) || text!.Length > MaxLength) return false;
            foreach (var part in text.Split(';'))
            {
                int split = part.IndexOf('=');
                if (split <= 0) continue;
                var key = part.Substring(0, split);
                if (key != "effects" && key != "season" && key != "day" && key != "length" && key != "meat" && key != "wood")
                    continue;
                if (fields.ContainsKey(key)) return false;
                fields.Add(key, part.Substring(split + 1));
            }
            return true;
        }

        static bool Integer(Dictionary<string, string> fields, string key, out int value)
        {
            value = 0;
            return fields.TryGetValue(key, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        static bool Number(Dictionary<string, string> fields, string key, out float value)
        {
            value = 0f;
            return fields.TryGetValue(key, out var text)
                && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
