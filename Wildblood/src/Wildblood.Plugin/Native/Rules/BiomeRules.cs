// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The fully resolved rules for one biome: a straight star-count distribution, the star-power lines, the default
    /// mutation chance curve, the per-mutation chance overrides and the per-mutation power fields. A biome block in the
    /// file overrides only what it names, so by the time a creature holds one of these every field is already filled.
    /// A creature whose `creatures:` entry names mutation keys holds a copy with those keys on top
    /// (<see cref="CreatureMutationRule"/>).
    /// </summary>
    public sealed class BiomeRules
    {
        public float[] StarChances = { 100f };
        public StarPower Star = new StarPower();
        public float LargeStarPower = 2f;
        public float[] MutationChance = { 0f };
        public readonly Dictionary<Mutation, float[]> MutationChances = new Dictionary<Mutation, float[]>();
        public readonly Dictionary<Mutation, Dictionary<string, float>> MutationPower =
            new Dictionary<Mutation, Dictionary<string, float>>();
        public readonly Dictionary<Mutation, Dictionary<string, string>> MutationText =
            new Dictionary<Mutation, Dictionary<string, string>>();

        /// <summary>The highest star count the star-chance distribution can produce.</summary>
        public int StarCeiling => Mathf.Max(0, StarChances.Length - 1);

        /// <summary>The chance this mutation appears at the given star count: its own curve, or the default one.</summary>
        public float ChanceOf(Mutation mutation, int stars)
        {
            float[] curve = MutationChances.TryGetValue(mutation, out float[] own) ? own : MutationChance;
            return Sample(curve, stars);
        }

        /// <summary>A named power field of a mutation; the built-in default if the file left it out.</summary>
        public float PowerOf(Mutation mutation, string field)
        {
            if (MutationPower.TryGetValue(mutation, out Dictionary<string, float> fields)
                && fields.TryGetValue(field, out float value))
            {
                return value;
            }
            return RuleDefaults.Power(mutation, field);
        }

        /// <summary>A named prefab field of a mutation (a vanilla effect name); the built-in default if the file left it out.</summary>
        public string PrefabOf(Mutation mutation, string field)
        {
            if (MutationText.TryGetValue(mutation, out Dictionary<string, string> fields)
                && fields.TryGetValue(field, out string value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }
            return RuleDefaults.Prefab(mutation, field);
        }

        private static float Sample(float[] curve, int stars)
        {
            if (curve == null || curve.Length == 0)
            {
                return 0f;
            }
            return curve[Mathf.Clamp(stars, 0, curve.Length - 1)];
        }

        public BiomeRules Clone()
        {
            BiomeRules copy = new BiomeRules
            {
                StarChances = (float[])StarChances.Clone(), Star = Star.Clone(),
                LargeStarPower = LargeStarPower, MutationChance = (float[])MutationChance.Clone(),
            };
            foreach (KeyValuePair<Mutation, float[]> pair in MutationChances)
            {
                copy.MutationChances[pair.Key] = (float[])pair.Value.Clone();
            }
            foreach (KeyValuePair<Mutation, Dictionary<string, float>> pair in MutationPower)
            {
                copy.MutationPower[pair.Key] = new Dictionary<string, float>(pair.Value);
            }
            foreach (KeyValuePair<Mutation, Dictionary<string, string>> pair in MutationText)
            {
                copy.MutationText[pair.Key] = new Dictionary<string, string>(pair.Value);
            }
            return copy;
        }
    }
}
