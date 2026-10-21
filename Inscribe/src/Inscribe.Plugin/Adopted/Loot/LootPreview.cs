// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Globalization;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Sockets;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// A console explanation of what a creature would drop if it died now (for an <c>ecraft</c> debug command): tier and
    /// its source, boss, stars, whether the death qualifies and why not, the chances and the stone and gear tables. Reads
    /// only replicated ZDO data and the synced rules, so it works on any peer. English, not localized (LOC-3).
    /// </summary>
    public static class LootPreview
    {
        public static string Explain(Character creature)
        {
            if (!DeathFacts.TryRead(creature, out DeathFacts facts))
            {
                return "not a creature this mod rolls drops for (a player, or no network object)";
            }
            RuleSet rules = ActiveRules.Current;
            CreatureProfile profile = CreatureProfiles.Build(Utils.GetPrefabName(creature.gameObject), rules.Economy);
            LootInput input = LootRoller.InputFor(profile, facts, rules.Economy, LootModifiers.None);
            StringBuilder text = new StringBuilder();
            text.Append(profile.Prefab).Append(": tier ").Append(input.Tier).Append(" (").Append(Source(profile, facts, rules)).Append(')');
            text.Append(", stars ").Append(input.Stars).Append(input.Ecr.StarsFromEcr ? " (Elite Creatures Reborn)" : "")
                .Append(input.IsBoss ? ", boss" : "").Append('\n');
            text.Append("  qualifies: ").Append(Qualifies(facts, profile, rules.Economy.Drops)).Append('\n');
            AppendChances(text, input, rules.Economy);
            AppendStones(text, rules.Economy, input.Tier);
            text.Append("  gear pool at tier ").Append(input.Tier).Append(": ").Append(GearPool.ForTier(input.Tier).Count).Append(" bases");
            return text.ToString();
        }

        private static string Source(CreatureProfile profile, DeathFacts facts, RuleSet rules)
        {
            if (profile.Tier > 0)
            {
                return profile.TierSource;
            }
            BiomeTiers.AtPosition(facts.Position, rules.Economy, out string biome);
            return "death position: " + biome;
        }

        private static string Qualifies(DeathFacts facts, CreatureProfile profile, DropRules drops)
        {
            string hits = $" (player hit {Yes(facts.PlayerHit)}, ally hit {Yes(facts.AllyHit)}, tamed or summoned {Yes(facts.Ally)})";
            if (profile.Blocked)
            {
                return "no, creature multiplier is 0" + hits;
            }
            return facts.Qualifies(drops, out string reason) ? "yes" + hits : "no, " + reason + hits;
        }

        private static void AppendChances(StringBuilder text, LootInput input, EconomyRules economy)
        {
            if (input.Boss != null)
            {
                float star = EcrTerms.StarMultiplier(economy.Drops, input);
                text.Append("  boss guarantees: ").Append(Num(input.Boss.StoneRolls * star)).Append(" runes, ")
                    .Append(Num(input.Boss.GearRolls * star)).Append(" gear, ").Append(input.Boss.Bonus.Count).Append(" bonus rows\n");
                return;
            }
            LootPlan plan = new LootPlan();
            LootPlanner.Plan(economy, input, new System.Random(0), plan);
            text.Append("  expected per kill: runes ").Append(Num(plan.StoneChance)).Append(" (max ")
                .Append(economy.Drops.MaxStonesPerKill).Append("), gear ").Append(Num(plan.GearChance))
                .Append(" (max ").Append(economy.Drops.MaxGearPerKill).Append(")\n");
        }

        // The table holds running totals only; each stone's share comes from its drops.stones row, which built it.
        private static void AppendStones(StringBuilder text, EconomyRules economy, int tier)
        {
            WeightedTable<StoneDef> table = economy.StoneDraw(tier, SocketSwitch.On);
            text.Append("  rune table:");
            foreach (StoneDef stone in table.Items)
            {
                float weight = economy.Drops.Stones.TryGetValue(stone.Id, out float[] row) ? row[tier - 1] : 0f;
                float share = table.Total <= 0f ? 0f : weight / table.Total * 100f;
                text.Append(' ').Append(stone.Id).Append(' ').Append(Num(share)).Append('%');
            }
            text.Append('\n');
        }

        private static string Yes(bool value) => value ? "yes" : "no";

        private static string Num(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
