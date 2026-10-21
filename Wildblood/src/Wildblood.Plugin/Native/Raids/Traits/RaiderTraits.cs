// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// A raider's stars and mutations (features/raids.md section 4.4): it rolls as any creature of the base's biome does
    /// - the difficulty, the world tier, its own `creatures:` entry, what its body rules out - then takes the heat's extra
    /// stars (two more again for the Warlord) and, at the heat's mutated share, a mutation when it rolled none, picked by
    /// the biome's own leanings and never Gilded. <see cref="RaiderSpawn"/> calls it on the machine that spawns the
    /// raider, in the same frame as the Instantiate, and the result is handed to the creature's controller as a forced
    /// roll, exactly as the Summoner's adds and `elite spawn` do, so it is written to the ZDO as the creature's one roll.
    /// With this mod's creature stars off, the extra stars are added to the game's level instead, which the creature then
    /// keeps.
    /// </summary>
    public static class RaiderTraits
    {
        /// <summary>Rolls the raider and forces the roll on it; null for a creature the mod does not roll (a boss, or one
        /// without the mod's controller), which is left as it is.</summary>
        public static CreatureTraits? Roll(Character creature, Heightmap.Biome biome, RaidBand band, bool warlord) =>
            Roll(creature, biome, band.ExtraStars + (warlord ? RaidTable.WarlordExtraStars : 0), band.MutatedPercent, 0);

        /// <summary>
        /// The same roll with the extra stars and the mutated share given outright, and the mutations in
        /// <paramref name="barredMask"/> struck from the base roll: Hel's Trial rolls its creatures through here
        /// (Arena/Trial/ArenaRoll), with its wave's stars and mutation chance and no Gilded. A raid bars nothing.
        /// </summary>
        public static CreatureTraits? Roll(Character creature, Heightmap.Biome biome, int extraStars, float mutatedPercent,
            int barredMask)
        {
            EliteController? elite = creature != null ? creature.GetComponent<EliteController>() : null;
            if (elite == null || creature!.IsBoss())
            {
                return null;
            }
            CreatureTraits traits = BaseRoll(creature, biome);
            traits.Mask &= ~barredMask;
            AddStars(creature, traits, extraStars, biome);
            if (traits.Mask == 0 && mutatedPercent > 0f && Util.Dice.Percent(mutatedPercent))
            {
                traits.Mask = RaiderMutation.Pick(creature, biome, traits.Stars);
            }
            elite.ForceTraits(traits, biome);
            return traits;
        }

        // The roll a wild creature of this kind gets in this biome now: the controller's own roll, with whatever another
        // mod registered for the prefab (a custom creature's fixed mutations) applied.
        private static CreatureTraits BaseRoll(Character creature, Heightmap.Biome biome) =>
            TraitRoller.Roll(RuleState.Active.For(biome, Utils.GetPrefabName(creature.gameObject)), RuleState.Active, WorldTier.Current(), biome, MutationBars.Of(creature));

        // The mod's stars when they are on; otherwise the game's level, which a creature with no stars of the mod keeps.
        private static void AddStars(Character creature, CreatureTraits traits, int extra, Heightmap.Biome biome)
        {
            if (extra <= 0)
            {
                return;
            }
            if (RuleState.Active.CreatureStars)
            {
                // Extra stars stop at the difficulty cap for this biome (ECR 3.28.1), not the fixed 8.
                RuleSet set = RuleState.Active;
                BiomeRules rules = set.For(biome, Utils.GetPrefabName(creature.gameObject));
                int cap = set.Difficulty == Difficulty.Custom
                    ? rules.StarCeiling
                    : PresetCell.For(set.Difficulty, traits.Tier, PresetTables.Rank(biome)).Cap;
                traits.Stars = ExtraPotions.Wildblood.Stars.TamedStars.ClampRaiderStars(traits.Stars, extra, cap);
                return;
            }
            creature.SetLevel(Mathf.Min(Mathf.Max(1, creature.GetLevel()) + extra, RaidTable.MaxRaiderStars + 1));
        }
    }
}
