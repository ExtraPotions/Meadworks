// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Traits/TraitRoller.cs, Traits/PresetRoller.cs, Rules/BiomeRules.cs (ChanceOf) and the mutation chance
// defaults of Rules/RuleDefaults.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; Unity-free with IDice; mutation rolls take an enabled mask; the modifier roll
// (one per group, Max cap, design C5 and Q2) is Meadworks-original after CreatureManager's documented behaviour.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Stars;

namespace ExtraPotions.Wildblood.Traits
{
    /// <summary>One biome's mutation lines: the shared chance by stars and per-mutation lines that replace it.</summary>
    public sealed class BiomeTraitRules
    {
        public BiomeTraitRules(float[] mutationChance)
        {
            MutationChance = mutationChance ?? throw new ArgumentNullException(nameof(mutationChance));
        }

        /// <summary>Percent chance of each mutation by stars (index = stars, last entry repeats).</summary>
        public float[] MutationChance { get; }

        public Dictionary<Mutation, float[]> MutationChances { get; } = new Dictionary<Mutation, float[]>();

        /// <summary>The rule file's built-in defaults (Meadows row; Devouring and Gilded have their own lines).</summary>
        public static BiomeTraitRules Default()
        {
            var r = new BiomeTraitRules(new[] { 2.5f, 3.5f, 5f, 6f, 7.5f, 10f });
            r.MutationChances[Mutation.Devouring] = new[] { 0.6f, 0.9f, 1.2f, 1.5f, 1.8f, 2.4f };
            r.MutationChances[Mutation.Gilded] = new[] { 0.2f, 0.25f, 0.35f, 0.45f, 0.55f, 0.7f };
            return r;
        }

        public float ChanceOf(Mutation mutation, int stars)
        {
            var curve = MutationChances.TryGetValue(mutation, out var own) ? own : MutationChance;
            if (curve.Length == 0) return 0f;
            return curve[Math.Min(curve.Length - 1, Math.Max(0, stars))];
        }
    }

    public static class TraitRoller
    {
        /// <summary>
        /// Custom difficulty: every enabled, unbarred mutation rolled independently at its biome-and-star chance times the
        /// tier's mutation boost; Max Mutations (0 = no limit) keeps a random subset.
        /// </summary>
        public static int RollMutationsCustom(BiomeTraitRules rules, int stars, int enabled, int barred, float boost, int maxMutations, IDice dice)
        {
            var hits = new List<Mutation>();
            foreach (var mutation in Mutations.InOrder)
            {
                var chance = Math.Min(100f, rules.ChanceOf(mutation, stars) * Math.Max(0f, boost));
                if (MayRoll(mutation, enabled, barred) && dice.Percent(chance)) hits.Add(mutation);
            }
            if (maxMutations > 0 && hits.Count > maxMutations)
            {
                for (var i = hits.Count - 1; i > 0; i--)
                {
                    var j = dice.Range(0, i + 1);
                    (hits[i], hits[j]) = (hits[j], hits[i]);
                }
                hits.RemoveRange(maxMutations, hits.Count - maxMutations);
            }
            return Mutations.Mask(hits);
        }

        /// <summary>The preset rate of a creature with stars: the cell's rate plus a quarter of it per star, at most 100.</summary>
        public static float PresetRate(float cellRate, int stars) => Math.Min(100f, cellRate * (1f + PresetTables.StarMutation * Math.Max(0, stars)));

        /// <summary>
        /// Preset difficulty: one chance to be mutated at <see cref="PresetRate"/>; if it is, exactly one mutation weighted by
        /// the biome's chance of each at this star count, so the Swamp still breeds Miasmic. 0 when none can be taken.
        /// </summary>
        public static int RollMutationPreset(float cellRate, int stars, BiomeTraitRules rules, int enabled, int barred, IDice dice)
        {
            if (!dice.Percent(PresetRate(cellRate, stars))) return 0;
            var all = Mutations.InOrder;
            var weights = new float[all.Count];
            var total = 0f;
            for (var i = 0; i < all.Count; i++)
            {
                weights[i] = MayRoll(all[i], enabled, barred) ? Math.Max(0f, rules.ChanceOf(all[i], stars)) : 0f;
                total += weights[i];
            }
            if (total <= 0f) return 0;
            return Mutations.Bit(all[dice.PickWeighted(weights)]);
        }

        public static bool MayRoll(Mutation mutation, int enabled, int barred) =>
            (enabled & Mutations.Bit(mutation)) != 0 && (barred & Mutations.Bit(mutation)) == 0;

        /// <summary>CreatureManager's levels.yml default modifier chance: 5 percent each, 10 for bosses and Enforcers.</summary>
        public static float DefaultModifierChance(bool boss) => boss ? 10f : 5f;

        /// <summary>
        /// Modifiers: each group in order takes at most one (its modifiers rolled in order, the first hit wins); when more
        /// groups hit than the cap allows, a random subset of groups is kept. Result in catalog order.
        /// </summary>
        public static IReadOnlyList<Modifier> RollModifiers(IReadOnlyDictionary<Modifier, float> chances, ModifierCap cap, IDice dice)
        {
            var allowed = Modifiers.Count(cap);
            if (allowed == 0) return Array.Empty<Modifier>();
            var hits = new List<Modifier>();
            foreach (ModifierGroup group in Enum.GetValues(typeof(ModifierGroup)))
                foreach (var modifier in Modifiers.InGroup(group))
                {
                    if (!chances.TryGetValue(modifier, out var chance) || !dice.Percent(chance)) continue;
                    hits.Add(modifier);
                    break;
                }
            while (hits.Count > allowed) hits.RemoveAt(dice.Range(0, hits.Count));
            return hits.OrderBy(m => m).ToList();
        }
    }
}
