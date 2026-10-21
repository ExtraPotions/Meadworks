// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>The economy validation table (economy-yaml.md section 9) for references between entries and sections.</summary>
    internal static class EconomyChecks
    {
        public static void Check(EconomyRules rules, RuleIssues issues)
        {
            foreach (StoneDef stone in rules.Stones)
            {
                CheckStone(rules, stone, issues);
            }
            CheckStoneNames(rules, issues);
            CheckRows(rules, rules.Drops.Stones.Keys, "drops.runes", issues, stone: true);
            CheckRows(rules, rules.Drops.RarityWeights.Keys, "drops.rarity_weights", issues, stone: false);
            CheckRows(rules, rules.Drops.BossRarityWeights.Keys, "drops.boss_rarity_weights", issues, stone: false);
            CheckBonuses(rules, issues);
        }

        private static void CheckStone(EconomyRules rules, StoneDef stone, RuleIssues issues)
        {
            string path = $"runes[{stone.Id}]";
            RaritiesExist(rules, stone.AppliesTo, path + ".applies_to", issues);
            RaritiesExist(rules, stone.Cost.Keys, path + ".cost", issues);
            if (!stone.Enabled)
            {
                return;
            }
            if (!Stones.StoneVerbs.IsImplemented(stone.Verb))
            {
                // economy-yaml.md section 9: treated as disabled, so it never drops and refuses as stone_disabled
                issues.Warn(path, null, $"verb '{EnumIds<StoneVerb>.Id(stone.Verb)}' is not in this version: the rune is treated as disabled");
                stone.Enabled = false;
                return;
            }
            if (stone.Verb == StoneVerb.Corrupt && AllZero(stone.Outcomes))
            {
                issues.Warn(path, null, "every weight is 0: the rune is treated as disabled");
                stone.Enabled = false;
            }
        }

        // IMP-56: the game stacks items by display name alone, so two stones sharing a name would merge into one stack
        // (and one named like a vanilla item would merge into that item's stack and become it).
        private static void CheckStoneNames(EconomyRules rules, RuleIssues issues)
        {
            Dictionary<string, string> seen = new Dictionary<string, string>();
            foreach (StoneDef stone in rules.Stones)
            {
                if (stone.Name.Length == 0)
                {
                    continue;
                }
                if (seen.TryGetValue(stone.Name, out string other))
                {
                    issues.Error($"runes[{stone.Id}].name", null, $"'{stone.Name}' is already the name of '{other}': runes with one name merge into one stack");
                    continue;
                }
                seen[stone.Name] = stone.Id;
                if (stone.Name.StartsWith("$item_", System.StringComparison.Ordinal))
                {
                    issues.Warn($"runes[{stone.Id}].name", null, $"'{stone.Name}' is a game item's name: the rune would stack with that item");
                }
            }
        }

        private static void RaritiesExist(EconomyRules rules, IEnumerable<string> ids, string path, RuleIssues issues)
        {
            foreach (string id in ids)
            {
                if (rules.Rarity(id) == null)
                {
                    issues.Error(path, null, $"'{id}' is not a rarity");
                }
            }
        }

        private static void CheckRows(EconomyRules rules, IEnumerable<string> ids, string path, RuleIssues issues, bool stone)
        {
            foreach (string id in ids)
            {
                bool known = stone ? rules.Stone(id) != null : rules.Rarity(id) != null;
                if (!known)
                {
                    issues.Error($"{path}.{id}", null, stone ? "is not a defined rune" : "is not a rarity");
                }
            }
        }

        private static void CheckBonuses(EconomyRules rules, RuleIssues issues)
        {
            List<KeyValuePair<string, IReadOnlyList<DropBonus>>> all = new List<KeyValuePair<string, IReadOnlyList<DropBonus>>>();
            foreach (KeyValuePair<string, BossDrop> boss in rules.Drops.Bosses)
            {
                all.Add(new KeyValuePair<string, IReadOnlyList<DropBonus>>("drops.bosses." + boss.Key, boss.Value.Bonus));
            }
            AddRows(all, "drops.creatures.", rules.Drops.Creatures);
            AddRows(all, "drops.chests.containers.", rules.Drops.Chests.Containers);
            foreach (KeyValuePair<string, IReadOnlyList<DropBonus>> entry in all)
            {
                foreach (DropBonus bonus in entry.Value)
                {
                    if (rules.Stone(bonus.Stone) == null)
                    {
                        issues.Error(entry.Key + ".bonus", null, $"'{bonus.Stone}' is not a defined rune");
                    }
                }
            }
        }

        private static void AddRows(List<KeyValuePair<string, IReadOnlyList<DropBonus>>> all, string prefix,
            IReadOnlyDictionary<string, CreatureDrop> entries)
        {
            foreach (KeyValuePair<string, CreatureDrop> entry in entries)
            {
                all.Add(new KeyValuePair<string, IReadOnlyList<DropBonus>>(prefix + entry.Key, entry.Value.Bonus));
            }
        }

        private static bool AllZero(IEnumerable<CorruptWeight> rows)
        {
            foreach (CorruptWeight row in rows)
            {
                if (row.Weight > 0f)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
