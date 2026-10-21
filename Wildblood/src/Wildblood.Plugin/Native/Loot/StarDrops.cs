// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Loot
{
    /// <summary>
    /// What a kill's stars add to its loot: the star `drops` line (Scaled) and the extra rolls (Rolled, Curated). A
    /// creature that keeps its game level (this mod's stars off for its kind, <see cref="RuleSet.KeepsLevel"/>) has none
    /// of this mod's stars to pay for - the game already scaled its pile by that level - so it takes neither. Nor does a
    /// boss (the user, 2026-10-05: a starred boss dropped a pile of Wishbones): its stars pay in heads
    /// (<see cref="BossTrophies"/>), and the rest of its table drops once, scaled only by its aspect and the multipliers.
    /// </summary>
    internal static class StarDrops
    {
        /// <summary>The quantity multiplier: the creature's own `drops` line, else its biome's; 1 for a boss without one.</summary>
        public static float Multiplier(CreatureLootRule? rule, EliteController controller, bool isBoss, int stars)
        {
            if (!controller.LootOnly && (controller.KeepsNativeLevel || RuleState.Active.KeepsLevel(stars, isBoss)))
            {
                return 1f;
            }
            if (rule?.Drops != null)
            {
                return LineAt(rule.Drops, stars); // the server's own words for this creature, a boss's too
            }
            return isBoss ? 1f : LiveDropsLine(controller, stars);
        }

        /// <summary>How many times the table is rolled again: never for a boss.</summary>
        public static int ExtraRolls(LootRules loot, bool isBoss, int stars, bool keepsNativeLevel, bool independentLoot = false) =>
            isBoss || keepsNativeLevel || (!independentLoot && RuleState.Active.KeepsLevel(stars, isBoss)) ? 0 : DropRoller.ExtraRolls(loot, stars);

        /// <summary>
        /// The star `drops` line from the rules active right now, not the snapshot the creature resolved with. Loot
        /// is applied at death, so a rule-file edit re-tunes what an already-spawned creature pays - the same hot
        /// reload every other loot setting gets by reading RuleState at death. Falls back to the resolve-time
        /// snapshot only when the ZDO is already gone.
        /// </summary>
        private static float LiveDropsLine(EliteController controller, int stars)
        {
            if (controller.LootOnly) return RuleState.Active.For(controller.LootBiome).Star.DropsAt(stars);
            ZDO? zdo = controller.View != null && controller.View.IsValid() ? controller.View.GetZDO() : null;
            if (zdo == null)
            {
                return controller.Rules.Star.DropsAt(stars);
            }
            return RuleState.Active.For(Traits.TraitStore.GetBiome(zdo)).Star.DropsAt(stars);
        }

        private static float LineAt(float[] line, int stars)
        {
            if (line.Length == 0)
            {
                return 1f;
            }
            return line[Mathf.Clamp(stars, 0, line.Length - 1)];
        }
    }
}
