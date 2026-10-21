// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Tempest.Weather
{
    public static class SeasonalEnvironments
    {
        // Only Solstice's published variants are aliases; another mod's suffix is not an ownership contract.
        static readonly Dictionary<string, string> Bases = Build();
        static readonly string[] Suffixes = { "", " Summer", " Fall", " Winter" };
        static readonly Dictionary<string, string?[]> Variants = BuildVariants();

        static Dictionary<string, string?[]> BuildVariants()
        {
            var result = new Dictionary<string, string?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in Bases)
            {
                if (!result.TryGetValue(pair.Value, out var names)) result.Add(pair.Value, names = new string?[4]);
                for (int season = 1; season <= 3; season++)
                    if (pair.Key.EndsWith(Suffixes[season], StringComparison.Ordinal)) names[season] = pair.Key;
            }
            return result;
        }

        static Dictionary<string, string> Build()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Add(string suffix, params string[] names)
            {
                foreach (var name in names) result.Add(name + suffix, name);
            }
            Add(" Summer", "Clear", "Misty", "DeepForest Mist", "Mistlands_clear", "Heath clear", "SwampRain", "Swamp");
            Add(" Fall", "ThunderStorm", "SwampRain");
            Add(" Winter", "Rain", "LightRain", "ThunderStorm", "Clear", "Misty", "DeepForest Mist", "SwampRain",
                "Mistlands_clear", "Mistlands_rain", "Mistlands_thunder", "Heath clear", "Snow", "SnowStorm", "Darklands_dark");
            return result;
        }

        public static string BaseName(string name) => name != null && Bases.TryGetValue(name, out var source) ? source : name!;

        public static string? ForSeason(string? name, int season, Func<string, bool> exists)
        {
            if (string.IsNullOrEmpty(name) || season < 1 || season > 3 || EnvironmentPlan.IsEventEnvironment(name)) return name;
            return Variants.TryGetValue(BaseName(name!), out var names) && names[season] is string variant && exists(variant) ? variant : name;
        }
    }

    public readonly struct SeasonReading
    {
        SeasonReading(int season, bool effects) { Season = season; Effects = effects; Available = true; }
        public int Season { get; }
        public bool Effects { get; }
        public bool Available { get; }

        public static bool TryRead(string? facts, out SeasonReading reading)
        {
            reading = default;
            if (string.IsNullOrEmpty(facts) || facts!.Length > 16384) return false;
            var values = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var part in facts.Split(';'))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0) continue;
                var key = part.Substring(0, eq);
                if (key != "season" && key != "effects" && key != "day" && key != "length" && key != "doy" && key != "year") continue;
                if (values.ContainsKey(key) || !int.TryParse(part.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return false;
                values.Add(key, value);
            }
            if (!values.TryGetValue("season", out var season) || season < 0 || season > 3
                || !values.TryGetValue("effects", out var effects) || effects < 0 || effects > 1
                || !values.TryGetValue("length", out var length) || length <= 0
                || !values.TryGetValue("day", out var day) || day < 0 || day > length
                || !values.TryGetValue("year", out var year) || year < length
                || !values.TryGetValue("doy", out var doy) || doy < 0 || doy > year) return false;
            reading = new SeasonReading(season, effects == 1);
            return true;
        }
    }

    public sealed class SeasonFactsReader
    {
        readonly Func<Func<string>?> resolve;
        double nextRead = double.NegativeInfinity;
        public SeasonFactsReader(Func<Func<string>?> resolver) => resolve = resolver ?? throw new ArgumentNullException(nameof(resolver));
        public SeasonReading Reading { get; private set; }

        public bool Poll(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < nextRead) return false;
            nextRead = now + 1;
            SeasonReading next = default;
            try { SeasonReading.TryRead(resolve()?.Invoke(), out next); }
            catch (Exception) { next = default; }
            var changed = Reading.Available != next.Available || Reading.Season != next.Season || Reading.Effects != next.Effects;
            Reading = next;
            return changed;
        }

        public void Reset() { Reading = default; nextRead = double.NegativeInfinity; }
    }

    public sealed class SeasonalWeights
    {
        readonly Dictionary<string, float> values;
        public SeasonalWeights(string? text) => values = Parse(text);

        static Dictionary<string, float> Parse(string? text)
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (text ?? "").Split(','))
            {
                var eq = part.IndexOf('=');
                if (eq < 1) continue;
                var name = part.Substring(0, eq).Trim();
                if (name.Length == 0 || name.IndexOf(';') >= 0 || name.IndexOf('\n') >= 0 || name.IndexOf('\r') >= 0) continue;
                if (!float.TryParse(part.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var weight)
                    || float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0 || weight > 4) continue;
                result[name] = weight;
            }
            return result;
        }

        public static string Normalize(string? text) => string.Join(", ", Parse(text).Select(p => p.Key + "=" + p.Value.ToString("R", CultureInfo.InvariantCulture)));

        public float Multiplier(string? name)
        {
            if (string.IsNullOrEmpty(name) || name!.IndexOf("clear", StringComparison.OrdinalIgnoreCase) >= 0 || EnvironmentPlan.IsEventEnvironment(name)) return 1;
            var source = SeasonalEnvironments.BaseName(name);
            var weather = WeatherSwitches.Find(source);
            if (weather != null && weather.Group != SwitchGroup.World) return 1;
            if (values.TryGetValue(name, out var exact)) return exact;
            return values.TryGetValue(source, out var weight) ? weight : 1;
        }
    }
}
