// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The mutation keys of one creature's `creatures:` entry (`mutation chance`, `mutation chances`, `mutation power`),
    /// held apart from any biome because the same creature walks several: they are laid over whichever biome's rules the
    /// creature rolled in, biome first and only what the entry names on top. The keys are read by
    /// <see cref="RuleOverlay"/> into an empty layer, so they parse and fail exactly as a biome block's do, and a second
    /// entry for the same creature read onto the same layer overrides only what it names. The merged rules are built
    /// once per biome and shared by every creature of the kind; the cache belongs to the <see cref="RuleSet"/> that owns
    /// this rule, so a reload, which builds a new set, starts it clean.
    /// </summary>
    public sealed class CreatureMutationRule
    {
        // An empty default curve means "not named": a list the parser accepts is never empty, so it cannot collide.
        private readonly BiomeRules _layer = new BiomeRules { MutationChance = new float[0] };

        // Keyed by the biome's rules object itself (BiomeRules keeps reference equality): one biome block, one merge.
        private readonly Dictionary<BiomeRules, BiomeRules> _merged = new Dictionary<BiomeRules, BiomeRules>();

        /// <summary>Reads one entry's mutation keys; a key an earlier entry for this creature set is replaced.</summary>
        internal void Read(YamlMappingNode block, List<string> errors)
        {
            RuleOverlay.ApplyMutations(_layer, block, errors);
            _merged.Clear();
        }

        /// <summary>The biome's rules with this creature's keys on top. The biome's own object is never changed.</summary>
        public BiomeRules Over(BiomeRules biome)
        {
            if (!_merged.TryGetValue(biome, out BiomeRules merged))
            {
                merged = biome.Clone();
                LayOnto(merged);
                _merged[biome] = merged;
            }
            return merged;
        }

        private void LayOnto(BiomeRules rules)
        {
            if (_layer.MutationChance.Length > 0)
            {
                rules.MutationChance = (float[])_layer.MutationChance.Clone();
            }
            foreach (KeyValuePair<Mutation, float[]> pair in _layer.MutationChances)
            {
                rules.MutationChances[pair.Key] = (float[])pair.Value.Clone();
            }
            MergeFields(_layer.MutationPower, rules.MutationPower);
            MergeFields(_layer.MutationText, rules.MutationText);
        }

        /// <summary>Power fields merge one by one: `Cloaked: { reveal distance: 15 }` keeps the biome's fade time.</summary>
        private static void MergeFields<T>(Dictionary<Mutation, Dictionary<string, T>> from,
            Dictionary<Mutation, Dictionary<string, T>> into)
        {
            foreach (KeyValuePair<Mutation, Dictionary<string, T>> pair in from)
            {
                if (!into.TryGetValue(pair.Key, out Dictionary<string, T> fields))
                {
                    into[pair.Key] = fields = new Dictionary<string, T>();
                }
                foreach (KeyValuePair<string, T> field in pair.Value)
                {
                    fields[field.Key] = field.Value;
                }
            }
        }
    }
}
