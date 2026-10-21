// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Loot;
using ExtraPotions.Inscribe.Adopted.Rules;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// <c>SetCreatureLoot</c> (api.md section 5): a modded creature's loot profile in the fields of the economy YAML's
    /// <c>drops.bosses</c> and <c>drops.creatures</c> entries, plus <c>prefab</c> (required) and <c>boss</c> (default
    /// false). A boss gets a boss entry (<c>tier</c>, <c>rune_rolls</c>, <c>gear_rolls</c>, <c>bonus</c>) and, when a
    /// multiplier is given, a creature entry with the multipliers; anything else a creature entry (<c>tier</c>,
    /// <c>multiplier</c>, <c>rune_multiplier</c>, <c>gear_multiplier</c>, <c>bonus</c>). Both are read by the YAML's own
    /// parser (<see cref="BossDropParser"/>); bonus runes must be built-in rune ids. The entries go under the YAML
    /// (<see cref="CodeLoot"/>); a later call for the prefab replaces them.
    /// </summary>
    internal static class ApiLoot
    {
        private const string Endpoint = "SetCreatureLoot";

        private static readonly string[] Keys =
        {
            "prefab", "boss", "tier", "multiplier", "rune_multiplier", "gear_multiplier", "rune_rolls", "gear_rolls", "bonus",
        };

        private static readonly string[] BossKeys = { "tier", "rune_rolls", "gear_rolls", "bonus" };
        private static readonly string[] CreatureKeys = { "tier", "multiplier", "rune_multiplier", "gear_multiplier", "bonus" };
        private static readonly string[] Multipliers = { "multiplier", "rune_multiplier", "gear_multiplier" };

        public static bool Set(string? json)
        {
            RuleIssues issues = new RuleIssues();
            YamlMappingNode? map = ApiJson.Map(json, Endpoint, issues);
            string? prefab = map == null ? null : Prefab(new MapReader(map, "loot", issues));
            if (map == null || prefab == null)
            {
                ApiJson.Accept(issues, Endpoint);
                return false;
            }
            bool boss = new MapReader(map, "loot", issues).Bool("boss", false);
            BossDrop? bossDrop = boss ? ReadBoss(map, prefab, issues) : null;
            CreatureDrop? creature = !boss || AnyKey(map, Multipliers) ? ReadCreature(map, prefab, boss, issues) : null;
            WarnUnused(new MapReader(map, "loot", issues), boss);
            if (!ApiJson.Accept(issues, Endpoint))
            {
                return false;
            }
            CodeLoot.Set(prefab, bossDrop, creature);
            RuleRebuild.Request(inscriptions: false);
            return true;
        }

        // The key check, then the prefab name (an error when missing).
        private static string? Prefab(MapReader r)
        {
            r.Unknown(Keys);
            string? prefab = r.Str("prefab");
            if (string.IsNullOrEmpty(prefab))
            {
                r.Error("prefab", "is required: the creature's prefab name");
                return null;
            }
            return prefab;
        }

        private static BossDrop? ReadBoss(YamlMappingNode map, string prefab, RuleIssues issues)
        {
            MapReader drops = Wrap("bosses", prefab, Pick(map, BossKeys), issues);
            BossDropParser.ParseBosses(drops).TryGetValue(prefab, out BossDrop drop);
            CheckRunes(drop?.Bonus, issues);
            return drop;
        }

        // A boss's creature entry carries only its multipliers: its tier and bonus are the boss entry's.
        private static CreatureDrop? ReadCreature(YamlMappingNode map, string prefab, bool boss, RuleIssues issues)
        {
            MapReader drops = Wrap("creatures", prefab, Pick(map, boss ? Multipliers : CreatureKeys), issues);
            BossDropParser.ParseCreatures(drops).TryGetValue(prefab, out CreatureDrop drop);
            CheckRunes(drop?.Bonus, issues);
            return drop;
        }

        private static MapReader Wrap(string kind, string prefab, YamlMappingNode entry, RuleIssues issues)
        {
            YamlMappingNode entries = new YamlMappingNode { { prefab, entry } };
            return new MapReader(new YamlMappingNode { { kind, entries } }, "drops", issues);
        }

        private static YamlMappingNode Pick(YamlMappingNode map, string[] keys)
        {
            YamlMappingNode picked = new YamlMappingNode();
            foreach (string key in keys)
            {
                YamlNode? node = YamlNodes.Child(map, key);
                if (node != null)
                {
                    picked.Add(key, node);
                }
            }
            return picked;
        }

        private static bool AnyKey(YamlMappingNode map, string[] keys) => System.Array.Exists(keys, k => YamlNodes.Child(map, k) != null);

        private static void CheckRunes(IReadOnlyList<DropBonus>? bonus, RuleIssues issues)
        {
            foreach (DropBonus row in bonus ?? new List<DropBonus>())
            {
                if (!StoneCatalog.IsBuiltIn(row.Stone))
                {
                    issues.Error("loot.bonus", null, $"'{row.Stone}' is not a rune id ({string.Join(", ", StoneCatalog.BuiltInIds)})");
                }
            }
        }

        private static void WarnUnused(MapReader r, bool boss)
        {
            if (!boss && (r.Has("rune_rolls") || r.Has("gear_rolls")))
            {
                r.Warn("rune_rolls", "rune_rolls and gear_rolls are for bosses (\"boss\": true); ignored");
            }
        }
    }
}
