// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Sockets;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// The chance model (drops.md sections 4-11) as a pure function of the rules, the input and a random source: no
    /// Unity, no world, no allocation. Used by the death hook, by world containers (<see cref="ChestRoller"/>: the same
    /// model with <c>drops.chests</c>' flat chances, no stars, no killer) and by <c>LootRoller.Simulate</c>.
    /// <para>
    /// Ordinary creature: <c>p = chance[tier] * star * multiplier * stone_/gear_multiplier</c> (stones also times the
    /// killer's Fateweaver factor and the ECR world-tier factor), <c>count = floor(p) + (random &lt; frac(p))</c>, capped
    /// per kill. The star multiplier is <c>drops.ecr</c>'s table for Elite Creatures Reborn stars (<see cref="EcrTerms"/>).
    /// A boss in the boss map instead gets its guaranteed <c>stone_rolls</c> / <c>gear_rolls</c> times the star multiplier (same
    /// floor/fraction rule, uncapped) plus its bonus rows; it makes no chance roll on top ("independent of the chance
    /// model", section 9, read as "instead of"). A boss only the game flags uses the chance model with the boss rarity
    /// rows. Creature bonus rows add to either and are not capped.
    /// </para>
    /// </summary>
    public static class LootPlanner
    {
        public static void Plan(EconomyRules economy, LootInput input, Random random, LootPlan plan)
        {
            int tier = BiomeTiers.Clamp(input.Tier);
            plan.Reset(tier, input.IsBoss || input.Boss != null);
            DropRules drops = economy.Drops;
            float star = EcrTerms.StarMultiplier(drops, input);
            if (input.Boss != null)
            {
                PlanBoss(economy, input, star, random, plan);
            }
            else
            {
                PlanChance(economy, input, star, random, plan);
            }
            if (input.StonesOn && input.Creature != null)
            {
                AddBonus(economy, input.Creature.Bonus, random, plan);
            }
        }

        /// <summary><c>floor(p)</c> plus one more with probability <c>frac(p)</c>; 0 for p at or below 0.</summary>
        public static int Count(float p, Random random)
        {
            if (!(p > 0f) || float.IsInfinity(p))
            {
                return 0;
            }
            int whole = (int)Math.Floor(p);
            return random.NextDouble() < p - whole ? whole + 1 : whole;
        }

        private static void PlanChance(EconomyRules economy, LootInput input, float star, Random random, LootPlan plan)
        {
            DropRules drops = economy.Drops;
            CreatureDrop? creature = input.Creature;
            float scale = star * (creature?.Multiplier ?? 1f);
            bool chest = input.Source == LootSource.Chest;
            if (input.StonesOn)
            {
                float stone = chest ? drops.Chests.StoneChance / 100f : Percent(drops.StoneChance, plan.Tier);
                plan.StoneChance = stone * scale * (creature?.StoneMultiplier ?? 1f) * input.Modifiers.StoneChanceFactor
                    * EcrTerms.StoneFactor(drops, input);
                DrawStones(economy, Math.Min(Count(plan.StoneChance, random), drops.MaxStonesPerKill), random, plan);
            }
            if (input.GearOn)
            {
                float gear = chest ? drops.Chests.GearChance / 100f : Percent(drops.GearChance, plan.Tier);
                plan.GearChance = gear * scale * (creature?.GearMultiplier ?? 1f);
                DrawGear(economy, Math.Min(Count(plan.GearChance, random), drops.MaxGearPerKill), input, random, plan);
            }
        }

        private static void PlanBoss(EconomyRules economy, LootInput input, float star, Random random, LootPlan plan)
        {
            BossDrop boss = input.Boss!;
            if (input.StonesOn)
            {
                DrawStones(economy, Count(boss.StoneRolls * star, random), random, plan);
                AddBonus(economy, boss.Bonus, random, plan);
                BossGems.Add(economy, input.Prefab, input.Stars, random, plan.Stones);
            }
            if (input.GearOn)
            {
                DrawGear(economy, Count(boss.GearRolls * star, random), input, random, plan);
            }
        }

        private static void DrawStones(EconomyRules economy, int count, Random random, LootPlan plan)
        {
            WeightedTable<StoneDef> table = economy.StoneDraw(plan.Tier, SocketSwitch.On);
            for (int i = 0; i < count; i++)
            {
                if (table.TryPick((float)random.NextDouble(), out StoneDef stone))
                {
                    plan.Stones.Add(stone);
                }
            }
        }

        // Norns' Favour (plus the ECR star and tier shares) re-weights the row before each pick (RarityFind).
        private static void DrawGear(EconomyRules economy, int count, LootInput input, Random random, LootPlan plan)
        {
            WeightedTable<RarityDef> table = economy.GearRarityDraw(plan.Tier, plan.IsBoss);
            float bonus = EcrTerms.RarityBonus(economy.Drops, input);
            for (int i = 0; i < count; i++)
            {
                if (RarityFind.TryPick(economy, table, plan.Tier, plan.IsBoss, bonus, (float)random.NextDouble(), out RarityDef rarity))
                {
                    plan.Gear.Add(rarity);
                }
            }
        }

        // Each row rolls on its own; a row naming a disabled or undefined stone is inert.
        private static void AddBonus(EconomyRules economy, IReadOnlyList<DropBonus> rows, Random random, LootPlan plan)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                DropBonus row = rows[i];
                StoneDef? stone = economy.Stone(row.Stone);
                if (stone == null || !stone.Enabled || !SocketSwitch.Drops(stone) || random.NextDouble() * 100.0 >= row.Chance)
                {
                    continue;
                }
                for (int n = 0; n < row.Amount; n++)
                {
                    plan.Stones.Add(stone);
                }
            }
        }

        private static float Percent(IReadOnlyList<float> perTier, int tier) =>
            tier >= 1 && tier <= perTier.Count ? perTier[tier - 1] / 100f : 0f;
    }
}
