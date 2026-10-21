// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using System.IO;
using ExtraPotions.Wildblood.Adopted.Traits;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// Turns the rule file's text into a <see cref="RuleSet"/>, collecting every problem against its line rather than
    /// throwing. A syntax error leaves <see cref="Result.Rules"/> null; a merely questionable value (a number in the
    /// wrong place) is skipped and recorded. The caller decides whether to adopt the result or keep the last good one.
    /// </summary>
    internal static class RuleParser
    {
        public sealed class Result
        {
            public RuleSet? Rules;
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
        }

        public static Result Parse(string text)
        {
            Result result = new Result();
            YamlMappingNode? root = LoadRoot(text, result.Errors);
            if (root == null)
            {
                return result;
            }
            RuleSet set = ReadTopLevel(root, result);
            ReadMutationEnabled(set, root, result);
            ReadDefaults(set, root, result);
            ReadBosses(set, root, result);
            ReadRespawning(set, root, result);
            ReadWorld(set, root, result);
            ReadLoot(set, root, result);
            ReadCreatures(set, root, result);
            ReadBiomes(set, root, result);
            return Finish(set, result);
        }

        // The single lines at the top of the file.
        private static RuleSet ReadTopLevel(YamlMappingNode root, Result result)
        {
            return new RuleSet
            {
                Difficulty = ReadDifficulty(root, result),
                CreatureStars = YamlRead.Bool(root, "creature stars", true, result.Errors),
                LockToServer = YamlRead.Bool(root, "lock to server", true, result.Errors),
                MaxMutations = Math.Max(0, YamlRead.Int(root, "max mutations", 1, result.Errors)),
            };
        }

        // The steps that need the whole file read: the star fallback for unlisted biomes, Extreme's longer star lines.
        private static Result Finish(RuleSet set, Result result)
        {
            SeedStarFallback(set);
            PadStarPower(set);
            result.Rules = set;
            return result;
        }

        // features/difficulty.md: the `difficulty:` line at the top; none = Custom, so a file from before 3.14 plays as before.
        private static Difficulty ReadDifficulty(YamlMappingNode root, Result result)
        {
            YamlNode? node = YamlRead.Child(root, "difficulty");
            if (node == null)
            {
                return Difficulty.Custom;
            }
            string? text = YamlRead.Scalar(root, "difficulty");
            if (DifficultyNames.TryParse(text, out Difficulty difficulty))
            {
                return difficulty;
            }
            YamlRead.AddError(result.Errors, node, $"'{text}' is not a difficulty: {DifficultyNames.Choices}");
            return Difficulty.Custom;
        }

        // Extreme rolls up to 8 stars: star power lines written for five continue by the built-in steps.
        internal static void PadStarPower(RuleSet set)
        {
            if (set.Difficulty != Difficulty.Extreme)
            {
                return;
            }
            StarPower reference = RuleDefaults.BaselineStarPower();
            set.Defaults.Star.PadFrom(reference);
            foreach (BiomeRules biome in set.Biomes.Values)
            {
                biome.Star.PadFrom(reference);
            }
        }

        /// <summary>An unlisted biome takes the Meadows row for stars; copy it onto the defaults so the fallback uses it.</summary>
        private static void SeedStarFallback(RuleSet set)
        {
            if (set.Biomes.TryGetValue("Meadows", out BiomeRules meadows))
            {
                set.Defaults.StarChances = (float[])meadows.StarChances.Clone();
            }
        }

        private static YamlMappingNode? LoadRoot(string text, List<string> errors)
        {
            try
            {
                YamlStream stream = new YamlStream();
                stream.Load(new StringReader(text));
                if (stream.Documents.Count > 0 && stream.Documents[0].RootNode is YamlMappingNode root)
                {
                    return root;
                }
                errors.Add("the file is empty or is not a set of keyed values");
            }
            catch (YamlException ex)
            {
                errors.Add($"line {ex.Start.Line}: {ex.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"could not read the file: {ex.Message}");
            }
            return null;
        }

        private static void ReadMutationEnabled(RuleSet set, YamlMappingNode root, Result result)
        {
            foreach (Mutation mutation in MutationCatalog.InOrder)
            {
                set.MutationEnabled[mutation] = MutationCatalog.OnByDefault(mutation);
            }
            if (!(YamlRead.Child(root, "mutations enabled") is YamlMappingNode map))
            {
                return;
            }
            foreach (KeyValuePair<YamlNode, YamlNode> pair in map.Children)
            {
                string name = (pair.Key as YamlScalarNode)?.Value ?? "";
                Mutation? mutation = MutationCatalog.FromName(name);
                if (mutation == null)
                {
                    YamlRead.AddError(result.Errors, pair.Key, $"'{name}' is not a mutation");
                }
                else if (YamlRead.TryBool(pair.Value, out bool value))
                {
                    set.MutationEnabled[mutation.Value] = value;
                }
                else
                {
                    YamlRead.AddError(result.Errors, pair.Value, $"'{name}' should be true or false");
                }
            }
        }

        private static void ReadDefaults(RuleSet set, YamlMappingNode root, Result result)
        {
            set.Defaults = RuleDefaults.Baseline();
            if (YamlRead.Child(root, "defaults") is YamlMappingNode block)
            {
                RuleOverlay.Apply(set.Defaults, block, result.Errors, result.Warnings);
            }
        }

        private static void ReadBosses(RuleSet set, YamlMappingNode root, Result result)
        {
            set.Boss = RuleDefaults.BaselineBoss();
            if (YamlRead.Child(root, "bosses") is YamlMappingNode block)
            {
                BossOverlay.Apply(set.Boss, block, result.Errors, result.Warnings);
            }
        }

        private static void ReadRespawning(RuleSet set, YamlMappingNode root, Result result)
        {
            set.Respawn = new RespawnRules();
            if (YamlRead.Child(root, "respawning") is YamlMappingNode block)
            {
                RespawnOverlay.Apply(set.Respawn, block, result.Errors);
            }
        }

        private static void ReadWorld(RuleSet set, YamlMappingNode root, Result result)
        {
            if (YamlRead.Child(root, Fields.WorldTiers) is YamlNode tiers
                && YamlRead.Map(tiers, result.Errors, $"'{Fields.WorldTiers}'") is YamlMappingNode tierBlock)
            {
                WorldOverlay.ApplyTiers(set.Tiers, tierBlock, result.Errors);
            }
            if (YamlRead.Child(root, Fields.Breeding) is YamlNode breeding
                && YamlRead.Map(breeding, result.Errors, $"'{Fields.Breeding}'") is YamlMappingNode breedBlock)
            {
                WorldOverlay.ApplyBreeding(set.Breeding, breedBlock, result.Errors);
            }
        }

        private static void ReadLoot(RuleSet set, YamlMappingNode root, Result result)
        {
            set.Loot = new LootRules();
            if (YamlRead.Child(root, "loot") is YamlMappingNode block)
            {
                LootOverlay.Apply(set.Loot, block, result.Errors);
            }
        }

        /// <summary>Per-creature loot and mutation keys. Mutation keys are laid over a biome only at lookup time.</summary>
        private static void ReadCreatures(RuleSet set, YamlMappingNode root, Result result)
        {
            if (YamlRead.Child(root, "creatures") is YamlNode creatures)
            {
                CreatureOverlay.Apply(set, creatures, result.Errors);
            }
        }

        private static void ReadBiomes(RuleSet set, YamlMappingNode root, Result result)
        {
            YamlNode? node = YamlRead.Child(root, "biomes");
            if (node == null)
            {
                return;
            }
            if (!(node is YamlSequenceNode seq))
            {
                YamlRead.AddError(result.Errors, node, "'biomes' should be a list of biome blocks");
                return;
            }
            foreach (YamlNode item in seq.Children)
            {
                ReadBiome(set, item, result);
            }
        }

        private static void ReadBiome(RuleSet set, YamlNode item, Result result)
        {
            if (!(item is YamlMappingNode block))
            {
                YamlRead.AddError(result.Errors, item, "a biome entry should be a block of keyed values");
                return;
            }
            string? name = YamlRead.Scalar(block, "match");
            if (string.IsNullOrEmpty(name))
            {
                YamlRead.AddError(result.Errors, block, "a biome entry has no 'match' name");
                return;
            }
            BiomeRules rules = set.Defaults.Clone();
            RuleOverlay.Apply(rules, block, result.Errors, result.Warnings);
            set.Biomes[name!] = rules;
        }
    }
}
