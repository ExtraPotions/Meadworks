// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>One reading of the optional season facts for tame breeding. Inactive means use the game's own pace.</summary>
    public readonly struct SeasonBreedRate
    {
        public SeasonBreedRate(bool active, float rate)
        {
            if (!active || float.IsNaN(rate) || float.IsInfinity(rate) || rate < 0f)
            {
                Active = false;
                Rate = 1f;
                Stop = false;
                return;
            }
            Active = true;
            Stop = rate <= 0f;
            if (Stop) Rate = 0f;
            else if (rate < SeasonBreeding.MinRate) Rate = SeasonBreeding.MinRate;
            else if (rate > SeasonBreeding.MaxRate) Rate = SeasonBreeding.MaxRate;
            else Rate = rate;
        }

        public bool Active { get; }
        /// <summary>1 when inactive. 0 means start no new pregnancy.</summary>
        public float Rate { get; }
        public bool Stop { get; }
    }

    /// <summary>Field values for one Procreate call. The caller restores the prefab afterward.</summary>
    public readonly struct SeasonEdit
    {
        public SeasonEdit(bool changed, bool stop, double duration, double chance, double partnerRange, double totalRange)
        {
            Changed = changed;
            Stop = stop;
            Duration = duration;
            Chance = chance;
            PartnerRange = partnerRange;
            TotalRange = totalRange;
        }

        public bool Changed { get; }
        public bool Stop { get; }
        public double Duration { get; }
        public double Chance { get; }
        public double PartnerRange { get; }
        public double TotalRange { get; }
    }

    /// <summary>
    /// Optional reader for <c>meadworks.seasons.facts</c> 1.0. Shepherd does not reference Solstice.
    /// <c>breed</c> applies only when the line is intact and <c>effects=1</c>.
    /// </summary>
    public static class SeasonBreeding
    {
        public const string Id = "meadworks.seasons.facts";
        public static readonly Version Version = new Version(1, 0);
        public const float MinRate = 0.25f;
        public const float MaxRate = 3f;
        public const int MaxLength = 8192;

        public static SeasonBreedRate Read(Func<string>? provider)
        {
            try { return provider != null && TryParse(provider(), out var rate) ? rate : default; }
            catch (Exception) { return default; }
        }

        public static bool TryParse(string? text, out SeasonBreedRate rate)
        {
            rate = default;
            if (!Fields(text, out var fields) || !Ready(fields) || !Number(fields, "breed", out var breed) || breed < 0f)
                return false;
            rate = new SeasonBreedRate(true, breed);
            return rate.Active;
        }

        /// <summary>
        /// Applies a rate that pace has already folded in. Chance is the game's skip chance, so a higher rate lowers it.
        /// Rate 0 sets that skip chance to 1 and leaves the duration alone, so a due birth still finishes.
        /// </summary>
        public static SeasonEdit Edit(double duration, double chance, double partner, double total, SeasonBreedRate rate)
        {
            if (!rate.Active || rate.Rate == 1f)
                return new SeasonEdit(false, false, duration, chance, partner, total);
            if (rate.Stop)
                return new SeasonEdit(true, true, duration, 1d, partner, total);
            double nextDuration = duration > 0d ? Math.Max(1d, duration / rate.Rate) : duration;
            double skip = (1d - chance) * rate.Rate;
            if (skip < 0d) skip = 0d;
            else if (skip > 1d) skip = 1d;
            double nextPartner = partner * rate.Rate;
            double nextTotal = total > 0d ? Math.Max(0.5d, total / rate.Rate) : total;
            return new SeasonEdit(true, false, nextDuration, 1d - skip, nextPartner, nextTotal);
        }

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
                if (key != "effects" && key != "season" && key != "day" && key != "length" && key != "breed") continue;
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
