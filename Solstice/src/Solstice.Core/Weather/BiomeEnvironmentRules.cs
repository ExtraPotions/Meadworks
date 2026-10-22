// Solstice - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from Seasons 1.10.6 by shudnal (https://github.com/shudnal/Seasons), commit 4054af6e8823d787921d673607e4942554872b66,
// Unlicense (public domain), SeasonSettings/SeasonBiomeEnvironments.cs (the default rules) and SeasonState/SeasonState.cs
// (ApplySeasonBiomeEnvironmentRules, BiomeNameMatches, NormalizeBiomeName). See THIRD_PARTY_NOTICES/Seasons-Unlicense.txt.
// Changed by expDARE, 2026: Meadows winter adds Snow and then removes Rain Winter, LightRain Winter and ThunderStorm Winter.
// The rule application works on plain name and weight pairs instead of the game's EnvEntry list, reports the environments it had to skip,
// and never touches another mod's environments (Tempest_ prefix).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Weather
{
    /// <summary>One environment of a biome's weather list and its weight (the game's <c>EnvEntry</c>: name and weight).</summary>
    public sealed class EnvironmentWeight
    {
        public EnvironmentWeight(string name, float weight, object? source = null)
        {
            Name = name ?? "";
            Weight = weight;
            Source = source;
        }

        public string Name { get; }
        public float Weight { get; }

        /// <summary>The caller's own object for an entry that came from the baseline and was not replaced (the plugin keeps the game's entry here); null for an entry the rules made.</summary>
        public object? Source { get; }
    }

    /// <summary>Adds an environment to a biome (the reference's <c>add</c> rule).</summary>
    public sealed class EnvironmentAdd
    {
        public EnvironmentAdd(string biome, string environment, float weight)
        {
            Biome = biome;
            Environment = environment;
            Weight = weight;
        }

        public string Biome { get; }
        public string Environment { get; }
        public float Weight { get; }
    }

    /// <summary>Replaces an environment by another in every biome that lists it (the reference's <c>replace</c> rule).</summary>
    public sealed class EnvironmentReplace
    {
        public EnvironmentReplace(string environment, string replaceTo)
        {
            Environment = environment;
            ReplaceTo = replaceTo;
        }

        public string Environment { get; }
        public string ReplaceTo { get; }
    }

    /// <summary>Removes an environment from a biome (the reference's <c>remove</c> rule).</summary>
    public sealed class EnvironmentRemove
    {
        public EnvironmentRemove(string biome, string environment)
        {
            Biome = biome;
            Environment = environment;
        }

        public string Biome { get; }
        public string Environment { get; }
    }

    /// <summary>The weather rules of one season.</summary>
    public sealed class SeasonEnvironmentRules
    {
        public List<EnvironmentAdd> Add { get; } = new List<EnvironmentAdd>();
        public List<EnvironmentReplace> Replace { get; } = new List<EnvironmentReplace>();
        public List<EnvironmentRemove> Remove { get; } = new List<EnvironmentRemove>();

        public int Count => Add.Count + Replace.Count + Remove.Count;
    }

    public static class SeasonBiomeEnvironments
    {
        /// <summary>A fresh copy of the reference default rules of a season (SeasonBiomeEnvironments constructor).</summary>
        public static SeasonEnvironmentRules For(Season season)
        {
            var r = new SeasonEnvironmentRules();
            switch (season)
            {
                case Season.Spring:
                    break;
                case Season.Summer:
                    r.Replace.Add(new EnvironmentReplace("Clear", "Clear Summer"));
                    r.Replace.Add(new EnvironmentReplace("Misty", "Misty Summer"));
                    r.Replace.Add(new EnvironmentReplace("DeepForest Mist", "DeepForest Mist Summer"));
                    r.Replace.Add(new EnvironmentReplace("Mistlands_clear", "Mistlands_clear Summer"));
                    r.Replace.Add(new EnvironmentReplace("Heath clear", "Heath clear Summer"));

                    r.Add.Add(new EnvironmentAdd("Meadows", "Heath clear", 2.0f));
                    r.Add.Add(new EnvironmentAdd("Black forest", "LightRain", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Black forest", "Clear", 0.2f));
                    r.Add.Add(new EnvironmentAdd("Swamp", "SwampRain Summer", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Swamp", "Swamp Summer", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Mountain", "Twilight_Clear", 1.0f));
                    r.Add.Add(new EnvironmentAdd("Mountain", "Twilight_Snow", 1.0f));
                    r.Add.Add(new EnvironmentAdd("Plains", "ThunderStorm", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Ocean", "Heath clear", 1.0f));
                    r.Add.Add(new EnvironmentAdd("Mistlands", "Heath clear", 0.5f));
                    break;
                case Season.Fall:
                    r.Replace.Add(new EnvironmentReplace("ThunderStorm", "ThunderStorm Fall"));

                    r.Add.Add(new EnvironmentAdd("Meadows", "DeepForest Mist", 0.2f));
                    r.Add.Add(new EnvironmentAdd("Meadows", "SwampRain Fall", 0.2f));
                    r.Add.Add(new EnvironmentAdd("Black forest", "LightRain", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Black forest", "SwampRain Fall", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Swamp", "ThunderStorm", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Mountain", "Twilight_SnowStorm", 0.5f));
                    r.Add.Add(new EnvironmentAdd("Plains", "Rain", 0.4f));
                    r.Add.Add(new EnvironmentAdd("Plains", "ThunderStorm", 0.2f));
                    r.Add.Add(new EnvironmentAdd("Plains", "SwampRain Fall", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Ocean", "SwampRain Fall", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Ocean", "DeepForest Mist", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Mistlands", "SwampRain Fall", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Mistlands", "DeepForest Mist", 0.1f));
                    break;
                case Season.Winter:
                    r.Replace.Add(new EnvironmentReplace("Rain", "Rain Winter"));
                    r.Replace.Add(new EnvironmentReplace("LightRain", "LightRain Winter"));
                    r.Replace.Add(new EnvironmentReplace("ThunderStorm", "ThunderStorm Winter"));
                    r.Replace.Add(new EnvironmentReplace("Clear", "Clear Winter"));
                    r.Replace.Add(new EnvironmentReplace("Misty", "Misty Winter"));
                    r.Replace.Add(new EnvironmentReplace("DeepForest Mist", "DeepForest Mist Winter"));
                    r.Replace.Add(new EnvironmentReplace("SwampRain", "SwampRain Winter"));
                    r.Replace.Add(new EnvironmentReplace("Mistlands_clear", "Mistlands_clear Winter"));
                    r.Replace.Add(new EnvironmentReplace("Mistlands_rain", "Mistlands_rain Winter"));
                    r.Replace.Add(new EnvironmentReplace("Mistlands_thunder", "Mistlands_thunder Winter"));
                    r.Replace.Add(new EnvironmentReplace("Heath clear", "Heath clear Winter"));
                    r.Replace.Add(new EnvironmentReplace("Snow", "Snow Winter"));
                    r.Replace.Add(new EnvironmentReplace("SnowStorm", "SnowStorm Winter"));

                    r.Add.Add(new EnvironmentAdd("Meadows", "Snow", 1.0f));
                    r.Add.Add(new EnvironmentAdd("Black forest", "Snow", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Mountain", "Twilight_Snow", 1.0f));
                    r.Add.Add(new EnvironmentAdd("Mountain", "Twilight_SnowStorm", 1.0f));
                    r.Add.Add(new EnvironmentAdd("Plains", "Snow", 0.5f));
                    r.Add.Add(new EnvironmentAdd("Plains", "Twilight_SnowStorm", 0.3f));
                    r.Add.Add(new EnvironmentAdd("Ocean", "Darklands_dark Winter", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Mistlands", "Twilight_Snow", 0.1f));
                    r.Add.Add(new EnvironmentAdd("Mistlands", "Twilight_SnowStorm", 0.1f));

                    r.Remove.Add(new EnvironmentRemove("Meadows", "Rain Winter"));
                    r.Remove.Add(new EnvironmentRemove("Meadows", "LightRain Winter"));
                    r.Remove.Add(new EnvironmentRemove("Meadows", "ThunderStorm Winter"));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(season));
            }
            return r;
        }
    }

    public static class EnvironmentRules
    {
        /// <summary>Environments with this prefix belong to Tempest (<c>Tempest_Blizzard</c>, <c>Tempest_Storm</c>): Solstice never changes them.</summary>
        public const string TempestPrefix = "Tempest_";

        public static bool IsForeign(string? environment) =>
            environment != null && environment.StartsWith(TempestPrefix, StringComparison.Ordinal);

        /// <summary>
        /// Applies a season's rules to one biome's weather list: adds first, then replaces, then removes. A rule whose environment does not
        /// exist in the game (<paramref name="exists"/>) is skipped, reported to <paramref name="onSkipped"/> and leaves the list as it was.
        /// The baseline is not modified.
        /// </summary>
        public static List<EnvironmentWeight> Apply(SeasonEnvironmentRules rules, string setupName, string enumName,
            IEnumerable<EnvironmentWeight> baseline, Func<string, bool> exists, Action<string>? onSkipped = null)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            if (exists == null) throw new ArgumentNullException(nameof(exists));
            var result = baseline.Where(w => w != null).Select(w => new EnvironmentWeight(w.Name, w.Weight, w.Source)).ToList();

            foreach (var add in rules.Add)
            {
                if (add == null || string.IsNullOrWhiteSpace(add.Environment) || IsForeign(add.Environment) || !BiomeMatches(add.Biome, setupName, enumName)) continue;
                if (!exists(add.Environment)) { onSkipped?.Invoke(add.Environment); continue; }
                result.Add(new EnvironmentWeight(add.Environment, add.Weight));
            }

            foreach (var replace in rules.Replace)
            {
                if (replace == null || string.IsNullOrWhiteSpace(replace.Environment) || string.IsNullOrWhiteSpace(replace.ReplaceTo)) continue;
                if (IsForeign(replace.Environment) || IsForeign(replace.ReplaceTo)) continue;
                bool reported = false;
                for (int i = 0; i < result.Count; i++)
                {
                    if (!string.Equals(result[i].Name, replace.Environment, StringComparison.Ordinal)) continue;
                    if (!exists(replace.ReplaceTo))
                    {
                        if (!reported) onSkipped?.Invoke(replace.ReplaceTo);
                        reported = true;
                        continue;
                    }
                    result[i] = new EnvironmentWeight(replace.ReplaceTo, result[i].Weight);
                }
            }

            foreach (var remove in rules.Remove)
            {
                if (remove == null || string.IsNullOrWhiteSpace(remove.Environment) || IsForeign(remove.Environment) || !BiomeMatches(remove.Biome, setupName, enumName)) continue;
                result.RemoveAll(w => string.Equals(w.Name, remove.Environment, StringComparison.Ordinal));
            }
            return result;
        }

        /// <summary>
        /// A configured biome name matches a biome by its weather setup name or by its enum name, ignoring case, spaces, underscores and
        /// hyphens (BiomeNameMatches).
        /// </summary>
        public static bool BiomeMatches(string? configuredName, string? setupName, string? enumName)
        {
            if (string.IsNullOrWhiteSpace(configuredName)) return false;
            var configured = Normalize(configuredName);
            if (!string.IsNullOrWhiteSpace(setupName) && string.Equals(configured, Normalize(setupName), StringComparison.OrdinalIgnoreCase)) return true;
            return !string.IsNullOrWhiteSpace(enumName) && string.Equals(configured, Normalize(enumName), StringComparison.OrdinalIgnoreCase);
        }

        static string Normalize(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "" : name.Replace(" ", "").Replace("_", "").Replace("-", "");
    }
}
