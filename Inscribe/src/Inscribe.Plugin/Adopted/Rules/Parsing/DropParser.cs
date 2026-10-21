// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Reads <c>drops:</c> (economy-yaml.md section 8): globals, chances, the tier tables and the gear block. Tier lists
    /// have eight entries (1 Meadows ... 8 Deep North); seven still load (<see cref="YamlLists.TierFloats"/>).
    /// </summary>
    internal static class DropParser
    {
        public const int Tiers = 8;

        /// <summary>The format-1 key the classes' <c>drop_weight</c> replaced: a warning where a user file still has it.</summary>
        private const string RemovedSlotWeights = "slot_weights";

        private static readonly string[] Keys =
        {
            "tamed", "require_player", "max_runes_per_kill", "max_gear_per_kill", "star_multipliers", "star_step",
            "chances", "runes", "rarity_weights", "boss_rarity_weights", "gear", "bosses", "creatures", "chests", "ecr",
            "scrolls", // removed enhancement scroll rates (Inscribe 0.2.0): accepted and ignored in older files
        };

        public static DropRules Parse(MapReader root)
        {
            MapReader? sub = root.Sub("drops");
            if (sub == null)
            {
                return new DropRules();
            }
            MapReader r = sub.Value;
            r.Unknown(Keys);
            DropRules drops = ReadGlobals(r);
            ReadChances(r, drops);
            drops.Stones = TierRows(r, "runes");
            drops.RarityWeights = TierRows(r, "rarity_weights");
            drops.BossRarityWeights = TierRows(r, "boss_rarity_weights");
            drops.Gear = ReadGear(r);
            drops.Bosses = BossDropParser.ParseBosses(r);
            drops.Creatures = BossDropParser.ParseCreatures(r);
            drops.Chests = ReadChests(r);
            drops.Ecr = EcrDropParser.Parse(r);
            return drops;
        }

        private static DropRules ReadGlobals(MapReader r)
        {
            return new DropRules
            {
                Tamed = r.Bool("tamed", false),
                RequirePlayer = r.Bool("require_player", true),
                MaxStonesPerKill = r.Int("max_runes_per_kill", 5, 0),
                MaxGearPerKill = r.Int("max_gear_per_kill", 2, 0),
                StarMultipliers = r.Floats("star_multipliers") ?? new[] { 1f, 2f, 3f },
                StarStep = r.Float("star_step", 1f, 0f),
            };
        }

        private static void ReadChances(MapReader r, DropRules drops)
        {
            MapReader? chances = r.Sub("chances");
            if (chances == null)
            {
                return;
            }
            chances.Value.Unknown("rune", "gear");
            drops.StoneChance = TierList(chances.Value, "rune") ?? new float[Tiers];
            drops.GearChance = TierList(chances.Value, "gear") ?? new float[Tiers];
        }

        private static float[]? TierList(MapReader r, string key)
        {
            YamlNode? node = r.Node(key);
            return node == null ? null : YamlLists.TierFloats(node, r.At(key), r.Issues, Tiers);
        }

        /// <summary>A map of id → eight weights, tier 1 first.</summary>
        private static Dictionary<string, float[]> TierRows(MapReader r, string key)
        {
            Dictionary<string, float[]> rows = new Dictionary<string, float[]>(System.StringComparer.Ordinal);
            MapReader? sub = r.Sub(key);
            if (sub == null)
            {
                return rows;
            }
            foreach (KeyValuePair<string, YamlNode> pair in YamlLists.Pairs(sub.Value.Map))
            {
                float[]? row = YamlLists.TierFloats(pair.Value, sub.Value.At(pair.Key), r.Issues, Tiers);
                if (row != null)
                {
                    rows[pair.Key] = row;
                }
            }
            return rows;
        }

        private static GearDropRules ReadGear(MapReader parent)
        {
            MapReader? sub = parent.Sub("gear");
            if (sub == null)
            {
                return new GearDropRules();
            }
            MapReader r = sub.Value;
            r.Unknown("require_recipe", "tiers_below", "same_tier_weight", "lower_tier_weight", "exclude", "include", RemovedSlotWeights);
            if (r.Has(RemovedSlotWeights))
            {
                r.Warn(RemovedSlotWeights, "is no longer read: each item class's drop_weight (economy classes) weighs its bases");
            }
            return new GearDropRules
            {
                RequireRecipe = r.Bool("require_recipe", true),
                TiersBelow = r.Int("tiers_below", 1, 0, Tiers - 1),
                SameTierWeight = r.Float("same_tier_weight", 3f, 0f),
                LowerTierWeight = r.Float("lower_tier_weight", 1f, 0f),
                Exclude = r.Strings("exclude") ?? new List<string>(),
                Include = YamlLists.IntMap(r, "include", 1, Tiers),
            };
        }

        private static ChestDrops ReadChests(MapReader r)
        {
            MapReader? sub = r.Sub("chests");
            if (sub == null)
            {
                return new ChestDrops();
            }
            sub.Value.Unknown("rune_chance", "gear_chance", "containers");
            return new ChestDrops
            {
                StoneChance = sub.Value.Float("rune_chance", 30f, 0f, 100f),
                GearChance = sub.Value.Float("gear_chance", 10f, 0f, 100f),
                Containers = BossDropParser.ParseCreatures(sub.Value, "containers"),
            };
        }
    }
}
