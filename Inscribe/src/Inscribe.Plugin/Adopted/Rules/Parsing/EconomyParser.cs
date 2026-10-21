// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Reads the merged economy document (economy-yaml.md) into <see cref="EconomyRules"/>: each section by its own
    /// parser, then the cross-references, lookups and per-tier draw tables (<see cref="EconomyIndex"/>). Null when any
    /// error.
    /// </summary>
    internal static class EconomyParser
    {
        private static readonly string[] RootKeys =
        {
            RuleFormat.Key, FamilyBuilder.UseDefaultsKey, "rarities", "rolling", "runes", "classes", "item_tiers", "biomes", "drops",
        };

        public static EconomyRules? Parse(YamlMappingNode root, RuleIssues issues)
        {
            MapReader r = new MapReader(root, "", issues);
            r.Unknown(RootKeys);
            EconomyRules rules = new EconomyRules
            {
                Rarities = RarityParser.Parse(r),
                Rolling = RarityParser.ParseRolling(r),
                Stones = StoneParser.Parse(r),
                Classes = ClassParser.Parse(r),
                ItemTiers = TierMapParser.ParseItemTiers(r),
                Biomes = TierMapParser.ParseBiomes(r),
                Drops = DropParser.Parse(r),
            };
            if (issues.HasErrors)
            {
                return null;
            }
            EconomyIndex.Build(rules, issues);
            return issues.HasErrors ? null : rules;
        }
    }
}
