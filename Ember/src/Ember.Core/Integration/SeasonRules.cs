// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtraPotions.Ember.Integration
{
    public readonly struct SeasonReading
    {
        public SeasonReading(int nightPercent, bool torchHeat, float torchDrain)
        {
            Active = true;
            NightPercent = Math.Max(10, Math.Min(50, nightPercent));
            TorchHeat = torchHeat;
            TorchDrain = Math.Max(0.1f, Math.Min(5f, torchDrain));
        }

        public bool Active { get; }
        public int NightPercent { get; }
        public float Dawn => Active ? NightPercent / 200f : 0.15f;
        public bool TorchHeat { get; }
        public float TorchDrain { get; }
    }

    public static class SeasonRules
    {
        public const string Id = "meadworks.seasons.facts";
        public static readonly Version Version = new Version(1, 0);

        public static SeasonReading Read(Func<string>? provider)
        {
            try { return provider != null && TryParse(provider(), out var value) ? value : default; }
            catch (Exception) { return default; }
        }

        public static bool TryParse(string? text, out SeasonReading reading)
        {
            reading = default;
            if (string.IsNullOrEmpty(text) || text!.Length > 8192) return false;
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in text.Split(';'))
            {
                int split = part.IndexOf('=');
                if (split <= 0) continue;
                var key = part.Substring(0, split);
                if (key != "effects" && key != "season" && key != "day" && key != "length" && key != "night" &&
                    key != "daystart" && key != "torch" && key != "torchdrain") continue;
                if (fields.ContainsKey(key)) return false;
                fields.Add(key, part.Substring(split + 1));
            }
            if (!Integer(fields, "effects", out var effects) || effects != 1 ||
                !Integer(fields, "season", out var season) || season < 0 || season > 3 ||
                !Integer(fields, "day", out var day) || !Integer(fields, "length", out var length) ||
                length < 1 || day < 0 || day > length ||
                !Integer(fields, "night", out var night) || night <= 0 || night >= 100 ||
                !Number(fields, "daystart", out var dawn) || Math.Abs(dawn - night / 200f) > 0.0011f ||
                !Integer(fields, "torch", out var torch) || (torch != 0 && torch != 1) ||
                !Number(fields, "torchdrain", out var drain) || drain < 0) return false;
            reading = new SeasonReading(night, torch == 1, drain);
            return true;
        }

        static bool Integer(Dictionary<string, string> fields, string key, out int value)
        {
            value = 0;
            return fields.TryGetValue(key, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        static bool Number(Dictionary<string, string> fields, string key, out float value)
        {
            value = 0;
            return fields.TryGetValue(key, out var text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // Preserve native visual dawn/dusk (0.25/0.75); only their raw clock positions move.
        public static float Rescale(float fraction, float dawn)
        {
            if (fraction < dawn) return fraction / dawn * 0.25f;
            if (fraction <= 1f - dawn) return 0.25f + (fraction - dawn) / (1f - 2f * dawn) * 0.5f;
            return 0.75f + (fraction - (1f - dawn)) / dawn * 0.25f;
        }

        public static double NextMorning(double time, double dayLength, float dawn) =>
            (Math.Floor(time / dayLength - dawn) + 1 + dawn) * dayLength;

        public static float Drain(float currentDrain, SeasonReading reading, bool enabled, bool fuelFree) =>
            fuelFree ? 0 : enabled && reading.Active ? reading.TorchDrain : currentDrain;

        public static bool Heat(bool currentHeat, SeasonReading reading, bool enabled) =>
            currentHeat && (!enabled || !reading.Active || reading.TorchHeat);
    }
}
