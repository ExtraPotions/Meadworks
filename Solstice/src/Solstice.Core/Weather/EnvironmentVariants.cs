// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the environment names the seasonal rules add or replace, and a tint so a clone is not a copy of the source.
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Weather
{
    /// <summary>One environment the game does not ship. The plugin clones <see cref="Source"/> and names the clone <see cref="Name"/>.</summary>
    public readonly struct EnvironmentVariant
    {
        public EnvironmentVariant(string name, string source, byte red, byte green, byte blue)
        {
            Name = name;
            Source = source;
            Red = red;
            Green = green;
            Blue = blue;
        }

        public string Name { get; }
        public string Source { get; }
        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }
    }

    /// <summary>Every seasonal name in the default biome rules that is not a vanilla environment.</summary>
    public static class EnvironmentVariants
    {
        static readonly string[] Suffixes = { " Summer", " Fall", " Winter" };

        public static IReadOnlyList<EnvironmentVariant> All { get; } = Build();

        public static bool IsVariant(string? name) => Suffix(name) != null;

        static List<EnvironmentVariant> Build()
        {
            var map = new SortedDictionary<string, EnvironmentVariant>(System.StringComparer.Ordinal);
            foreach (var season in Seasons.All)
            {
                var rules = SeasonBiomeEnvironments.For(season);
                foreach (var replace in rules.Replace) Add(map, replace.ReplaceTo);
                foreach (var add in rules.Add) Add(map, add.Environment);
            }
            return map.Values.ToList();
        }

        static void Add(SortedDictionary<string, EnvironmentVariant> map, string name)
        {
            var suffix = Suffix(name);
            if (suffix == null || map.ContainsKey(name)) return;
            var source = name.Substring(0, name.Length - suffix.Length);
            if (name == "Swamp Summer") source = "Darklands_dark";
            byte red, green, blue;
            if (suffix == " Summer") { red = 255; green = 214; blue = 140; }
            else if (suffix == " Fall") { red = 196; green = 112; blue = 48; }
            else { red = 186; green = 206; blue = 214; }
            map.Add(name, new EnvironmentVariant(name, source, red, green, blue));
        }

        static string? Suffix(string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var suffix in Suffixes)
                if (name!.EndsWith(suffix, System.StringComparison.Ordinal) && name.Length > suffix.Length) return suffix;
            return null;
        }
    }
}
