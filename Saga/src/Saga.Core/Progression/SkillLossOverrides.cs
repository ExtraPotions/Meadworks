// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-skill exceptions to Skill Loss On Death, written as "Skill=Percent" entries.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Saga.Progression
{
    /// <summary>
    /// The "Skill Loss Overrides" setting: a comma list of <c>Skill=Percent</c> entries, such as <c>Swords=0, Run=10</c>.
    /// A listed skill loses its own percent on death instead of Skill Loss On Death; every other skill keeps the global
    /// value. Skill names ignore case, spaces, underscores and hyphens ("Wood Cutting" is WoodCutting). Percent is 0 to
    /// 100; the world's death penalty modifier still scales it. Malformed entries are dropped and a later entry for the
    /// same skill replaces an earlier one.
    /// </summary>
    public static class SkillLossOverrides
    {
        public static IReadOnlyDictionary<string, float> Parse(string? text)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var (name, percent) in Entries(text)) result[Key(name)] = percent;
            return result;
        }

        /// <summary>The stored form: valid entries only, one per skill (the last wins), in first-seen order.</summary>
        public static string Normalize(string? text)
        {
            var order = new List<string>();
            var latest = new Dictionary<string, (string Name, float Percent)>(StringComparer.Ordinal);
            foreach (var (name, percent) in Entries(text))
            {
                var key = Key(name);
                if (!latest.ContainsKey(key)) order.Add(key);
                latest[key] = (name, percent);
            }
            return string.Join(", ", order.Select(k => latest[k].Name + "=" + latest[k].Percent.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Loss percent for one skill: the first of <paramref name="names"/> (its native or registered names) that has an
        /// override, else <paramref name="globalPercent"/>. The result is clamped to 0..100.
        /// </summary>
        public static float LossPercent(IReadOnlyDictionary<string, float> overrides, IEnumerable<string?> names, float globalPercent) =>
            TryFind(overrides, names, out var percent) ? percent : Clamp(globalPercent);

        /// <summary>One of <paramref name="names"/> has an override.</summary>
        public static bool Lists(IReadOnlyDictionary<string, float> overrides, IEnumerable<string?> names) => TryFind(overrides, names, out _);

        private static bool TryFind(IReadOnlyDictionary<string, float> overrides, IEnumerable<string?> names, out float percent)
        {
            percent = 0f;
            if (overrides == null || names == null) return false;
            foreach (var name in names)
                if (!string.IsNullOrWhiteSpace(name) && overrides.TryGetValue(Key(name!), out percent)) return true;
            return false;
        }

        /// <summary>A skill's level after death: <c>level - level * percent / 100 * worldRate</c>, never below zero.</summary>
        public static float LevelAfterDeath(float level, float lossPercent, float worldRate)
        {
            var factor = Clamp(lossPercent) / 100f * Math.Max(0f, worldRate);
            return Math.Max(0f, level - level * factor);
        }

        private static IEnumerable<(string Name, float Percent)> Entries(string? text)
        {
            foreach (var part in (text ?? "").Split(',', ';', '\n'))
            {
                var at = part.IndexOfAny(new[] { '=', ':' });
                if (at <= 0) continue;
                var name = part.Substring(0, at).Trim();
                if (Key(name).Length == 0) continue;
                if (!float.TryParse(part.Substring(at + 1).Trim().TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
                    || float.IsNaN(percent) || float.IsInfinity(percent)) continue;
                yield return (name, Clamp(percent));
            }
        }

        private static string Key(string name) =>
            new string(name.Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').Select(char.ToLowerInvariant).ToArray());

        private static float Clamp(float percent) => float.IsNaN(percent) ? 0f : Math.Min(100f, Math.Max(0f, percent));
    }
}
