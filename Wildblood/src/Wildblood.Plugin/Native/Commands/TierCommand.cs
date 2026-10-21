// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// <c>wildblood tier</c>: prints the world tier, the difficulty and what it does to the rolls right now (on a preset, the
    /// biome the player stands in: its cap, starred share and mutation rate), and every listed boss with whether
    /// the world has its defeat key - then any boss the game knows that the list leaves out, with the key to add. The
    /// only read-only sub-command open to everyone: it reads the global keys this machine already holds, changes
    /// nothing, and a player on a locked server is exactly who needs to know why the world got harder.
    /// </summary>
    public static class TierCommand
    {
        public static void Run(Terminal.ConsoleEventArgs args)
        {
            TierRules rules = RuleState.Active.Tiers;
            if (!rules.Enabled)
            {
                CommandReply.Send(args, "wildblood tier: world tiers are off; the world stays at tier 0.");
                return;
            }
            int tier = WorldTier.Current();
            ReportDifficulty(args, rules, tier);
            Dictionary<string, string> known = WorldTier.KnownBosses();
            foreach (string key in rules.BossKeys)
            {
                string name = known.TryGetValue(key, out string found) ? found : "no known boss sets this";
                CommandReply.Send(args, $"  [{(WorldTier.IsDefeated(key) ? "x" : " ")}] {name} ({key})");
            }
            ReportUnlisted(args, rules, known);
        }

        private static void ReportDifficulty(Terminal.ConsoleEventArgs args, TierRules rules, int tier)
        {
            Difficulty difficulty = RuleState.Active.Difficulty;
            string head = $"wildblood tier: world tier {tier} of {WorldTier.Ceiling()}, difficulty {DifficultyNames.Name(difficulty)}";
            if (!RuleState.Active.CreatureStars)
            {
                head += " - creature stars off: no stars from this mod, creatures keep the game's level";
            }
            if (difficulty == Difficulty.Custom)
            {
                CommandReply.Send(args, $"{head} - star boost x{rules.StarBoostAt(tier):0.##}, mutation boost x{rules.MutationBoostAt(tier):0.##}");
                return;
            }
            CommandReply.Send(args, head);
            Player? player = Player.m_localPlayer;
            if (player != null)
            {
                ReportHere(args, difficulty, tier, Heightmap.FindBiome(player.transform.position));
            }
        }

        private static void ReportHere(Terminal.ConsoleEventArgs args, Difficulty difficulty, int tier, Heightmap.Biome biome)
        {
            PresetCell cell = PresetCell.For(difficulty, tier, PresetTables.Rank(biome));
            if (!RuleState.Active.CreatureStars)
            {
                CommandReply.Send(args, $"  here ({biome}): {cell.Mutation:0}% of creatures mutated");
                return;
            }
            CommandReply.Send(args, $"  here ({biome}): up to {cell.Cap} stars, {cell.StarredPercent:0}% starred, "
                + $"{cell.Mutation:0}% of plain creatures mutated (+25% of that per star)");
        }

        private static void ReportUnlisted(Terminal.ConsoleEventArgs args, TierRules rules, Dictionary<string, string> known)
        {
            foreach (KeyValuePair<string, string> boss in known)
            {
                if (!rules.BossKeys.Contains(boss.Key))
                {
                    CommandReply.Send(args, $"  not counted: {boss.Value} ({boss.Key})");
                }
            }
        }
    }
}
