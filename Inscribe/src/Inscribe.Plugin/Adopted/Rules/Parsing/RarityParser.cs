// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; tier_floor and
// source_only.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using UnityEngine;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Reads <c>rarities:</c> (ladder order = list order) and <c>rolling:</c>, with the rarity.md section 7 checks and the
    /// prefix and suffix limits of classes-and-tiers.md section 6.
    /// </summary>
    internal static class RarityParser
    {
        private static readonly string[] Keys = { "id", "name", "color", "glow", "inscriptions", "drop_weight", "prefixes", "suffixes", "tier_floor", "source_only" };

        /// <summary>The format-1 key the item levels and tier ladders replaced: a warning where a user file still has it.</summary>
        private const string RemovedTierWindow = "tier_window";

        public static List<RarityDef> Parse(MapReader root)
        {
            List<RarityDef> rarities = new List<RarityDef>();
            YamlSequenceNode? seq = root.Seq("rarities");
            foreach (YamlNode node in seq?.Children ?? new List<YamlNode>())
            {
                RarityDef? rarity = ParseOne(node, rarities.Count, root.Issues);
                if (rarity != null)
                {
                    rarities.Add(rarity);
                }
            }
            CheckLadder(root, rarities);
            return rarities;
        }

        private static RarityDef? ParseOne(YamlNode node, int index, RuleIssues issues)
        {
            string? id = node is YamlMappingNode m ? new MapReader(m, "rarities[?]", issues).Id("id") : null;
            if (id == null)
            {
                issues.Error("rarities[?]", node, "a rarity entry needs a valid 'id'");
                return null;
            }
            MapReader r = new MapReader((YamlMappingNode)node, $"rarities[{id}]", issues);
            r.Unknown(Keys);
            RarityDef rarity = new RarityDef
            {
                Id = id, Index = index, Name = r.Str("name") ?? "$ecf_rarity_" + id,
                Glow = r.Bool("glow", index > 0),
                DropWeight = r.Float("drop_weight", 1f, 0f),
                TierFloor = r.Int("tier_floor", 0, 0),
                SourceOnly = r.Bool("source_only", false),
            };
            ReadColor(r, rarity);
            ReadCounts(r, rarity);
            ReadLimits(r, rarity);
            return rarity;
        }

        private static void ReadColor(MapReader r, RarityDef rarity)
        {
            string? text = r.Str("color");
            if (text == null)
            {
                r.Issues.Error(r.At("color"), r.Map, "is required (#RRGGBB)");
                return;
            }
            if (!Colors.TryParse(text, out Color32 color))
            {
                r.Error("color", $"'{text}' is not a #RRGGBB color");
                return;
            }
            rarity.Color = text.ToUpperInvariant();
            rarity.Color32 = color;
        }

        private static void ReadCounts(MapReader r, RarityDef rarity)
        {
            MapReader? counts = r.Sub("inscriptions");
            if (counts == null)
            {
                r.Issues.Error(r.At("inscriptions"), r.Map, "is required: { min: N, max: N }");
                return;
            }
            counts.Value.Unknown("min", "max");
            rarity.MinAffixes = counts.Value.Int("min", 0, 0);
            rarity.MaxAffixes = counts.Value.Int("max", 0, 0);
            if (rarity.MinAffixes > rarity.MaxAffixes)
            {
                counts.Value.Error("min", "min is above max");
            }
        }

        // prefixes / suffixes default to the maximum count: no limit beyond it.
        private static void ReadLimits(MapReader r, RarityDef rarity)
        {
            rarity.MaxPrefixes = r.Int("prefixes", rarity.MaxAffixes, 0);
            rarity.MaxSuffixes = r.Int("suffixes", rarity.MaxAffixes, 0);
            if (rarity.MaxPrefixes + rarity.MaxSuffixes < rarity.MinAffixes)
            {
                r.Warn("prefixes", $"prefixes + suffixes is below inscriptions.min ({rarity.MinAffixes}): a fresh roll can never reach it");
            }
        }

        private static void CheckLadder(MapReader root, List<RarityDef> rarities)
        {
            if (rarities.Count < 2)
            {
                root.Error("rarities", "at least two rarities are needed (the base rarity and one magic rarity)");
                return;
            }
            if (rarities[0].MaxAffixes != 0)
            {
                root.Error("rarities", $"the first rarity ('{rarities[0].Id}') is the base rarity: its inscriptions.max must be 0");
            }
            if (rarities[0].Glow)
            {
                root.Warn("rarities", $"glow on the base rarity '{rarities[0].Id}' is ignored: it never glows");
                rarities[0].Glow = false;
            }
        }

        public static RollingSettings ParseRolling(MapReader root)
        {
            MapReader? sub = root.Sub("rolling");
            if (sub == null)
            {
                return new RollingSettings();
            }
            MapReader r = sub.Value;
            r.Unknown("allowed_closed_fraction", "promote_adds_at_least", "count_weights", RemovedTierWindow);
            if (r.Has(RemovedTierWindow))
            {
                r.Warn(RemovedTierWindow, "is no longer read: an item rolls every tier its item level has unlocked (allowed_closed_fraction closes the top of allowed classes)");
            }
            return new RollingSettings
            {
                AllowedClosedFraction = r.Float("allowed_closed_fraction", 0.334f, 0f, 1f),
                PromoteAddsAtLeast = r.Int("promote_adds_at_least", 1, 0),
                CountWeights = ReadCountWeights(r),
            };
        }

        private static Dictionary<string, IReadOnlyDictionary<int, float>> ReadCountWeights(MapReader r)
        {
            Dictionary<string, IReadOnlyDictionary<int, float>> result = new Dictionary<string, IReadOnlyDictionary<int, float>>();
            MapReader? sub = r.Sub("count_weights");
            if (sub == null)
            {
                return result;
            }
            foreach (KeyValuePair<string, YamlNode> pair in YamlLists.Pairs(sub.Value.Map))
            {
                result[pair.Key] = CountRow(sub.Value, pair.Key);
            }
            return result;
        }

        private static Dictionary<int, float> CountRow(MapReader sub, string rarityId)
        {
            Dictionary<int, float> weights = new Dictionary<int, float>();
            foreach (KeyValuePair<string, float> w in YamlLists.FloatMap(sub, rarityId))
            {
                if (Numbers.TryInt(w.Key, out int count) && count >= 0)
                {
                    weights[count] = w.Value;
                }
                else
                {
                    sub.Issues.Error($"{sub.At(rarityId)}.{w.Key}", sub.Node(rarityId), "keys are inscription counts (whole numbers)");
                }
            }
            return weights;
        }
    }
}
