// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;
using Biome = Heightmap.Biome;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>What the drop rules say about one creature prefab: its fixed tier (or 0 = the death position decides), why,
    /// its boss entry and its per-creature entry. Immutable; built from one rules snapshot.</summary>
    public sealed class CreatureProfile
    {
        internal CreatureProfile(string prefab, int tier, string tierSource, BossDrop? boss, CreatureDrop? creature)
        {
            Prefab = prefab;
            Tier = tier;
            TierSource = tierSource;
            Boss = boss;
            Creature = creature;
        }

        public string Prefab { get; }

        /// <summary>1..8, or 0 when the biome at the death position decides (no open-world spawn entry).</summary>
        public int Tier { get; }

        /// <summary><c>boss map</c>, <c>override</c>, <c>spawn: black_forest</c> or <c>death position</c>.</summary>
        public string TierSource { get; }

        /// <summary>The <c>drops.bosses</c> entry, when the prefab is in the boss map.</summary>
        public BossDrop? Boss { get; }

        /// <summary>The <c>drops.creatures</c> entry, when there is one.</summary>
        public CreatureDrop? Creature { get; }

        /// <summary><c>multiplier: 0</c>: this creature never drops our loot.</summary>
        public bool Blocked => Creature != null && Creature.Multiplier <= 0f;
    }

    /// <summary>
    /// Creature profiles by prefab hash, derived once per prefab and rules generation (drops.md sections 3 and 13), so
    /// a kill costs one dictionary lookup. The boss and creature entries are the YAML's, else what a mod set through the
    /// API (<see cref="CodeLoot"/>). Tier order: boss map, creature override, home biome from the spawn lists,
    /// then the death position (resolved per kill by the caller when <see cref="CreatureProfile.Tier"/> is 0).
    /// </summary>
    public static class CreatureProfiles
    {
        private static readonly Dictionary<int, CreatureProfile> Cache = new Dictionary<int, CreatureProfile>();
        private static int _generation = -1;

        internal static void Clear() => Cache.Clear();

        /// <summary>The profile of a creature, keyed by its ZDO prefab hash; the name is only read on a cache miss.</summary>
        public static CreatureProfile Get(Character character, int prefabHash, RuleSet rules)
        {
            if (_generation != rules.Generation)
            {
                Cache.Clear();
                _generation = rules.Generation;
            }
            if (Cache.TryGetValue(prefabHash, out CreatureProfile profile))
            {
                return profile;
            }
            profile = Build(Utils.GetPrefabName(character.gameObject), rules.Economy);
            if (SpawnBiomes.Ready)
            {
                Cache[prefabHash] = profile;   // before the zone system exists the spawn fallback is not final
            }
            return profile;
        }

        /// <summary>A profile by prefab name, uncached (commands and simulation).</summary>
        public static CreatureProfile Build(string prefab, EconomyRules economy)
        {
            DropRules drops = economy.Drops;
            BossDrop? boss = CodeLoot.Boss(drops, prefab);
            CreatureDrop? creature = CodeLoot.Creature(drops, prefab);
            if (boss != null)
            {
                return new CreatureProfile(prefab, BiomeTiers.Clamp(boss.Tier), "boss map", boss, creature);
            }
            if (creature != null && creature.Tier > 0)
            {
                return new CreatureProfile(prefab, BiomeTiers.Clamp(creature.Tier), "override", null, creature);
            }
            if (SpawnBiomes.TryGet(prefab, out Biome biomes))
            {
                int tier = BiomeTiers.LowestTier(biomes, economy, out string biomeId);
                if (tier > 0)
                {
                    return new CreatureProfile(prefab, tier, "spawn: " + biomeId, null, creature);
                }
            }
            return new CreatureProfile(prefab, 0, "death position", null, creature);
        }
    }
}
