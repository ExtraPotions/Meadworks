// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Reads the merged affix document (configuration.md section 6) into <see cref="AffixRules"/>: the root keys,
    /// every affix entry, the pool additions mods made through the API (<see cref="CodeLayer.ApplyPools"/>), then the
    /// cross-entry checks and lookups (<see cref="AffixIndex"/>). Null when any error.
    /// </summary>
    internal static class AffixFamilyParser
    {
        private static readonly string[] RootKeys =
        {
            RuleFormat.Key, FamilyBuilder.UseDefaultsKey, "caps", "inscriptions",
        };

        public static AffixRules? Parse(YamlMappingNode root, RuleIssues issues)
        {
            MapReader r = new MapReader(root, "", issues);
            r.Unknown(RootKeys);
            AffixRules rules = new AffixRules();
            rules.Caps = ReadCaps(r);
            rules.Affixes = ReadAffixes(r);
            if (issues.HasErrors)
            {
                return null;
            }
            CodeLayer.ApplyPools(rules.Affixes, issues);
            AffixIndex.Build(rules, issues);
            return issues.HasErrors ? null : rules;
        }

        private static Dictionary<string, float> ReadCaps(MapReader r)
        {
            Dictionary<string, float> caps = YamlLists.FloatMap(r, "caps");
            foreach (KeyValuePair<string, float> cap in new List<KeyValuePair<string, float>>(caps))
            {
                if (cap.Value <= 0f)
                {
                    r.Issues.Error($"caps.{cap.Key}", r.Node("caps"), "a cap must be above 0 (use null to remove one)");
                }
            }
            return caps;
        }

        private static List<AffixDef> ReadAffixes(MapReader r)
        {
            List<AffixDef> affixes = new List<AffixDef>();
            YamlSequenceNode? seq = r.Seq("inscriptions");
            if (seq == null)
            {
                return affixes;
            }
            foreach (YamlNode entry in seq.Children)
            {
                AffixDef? def = AffixParser.Parse(entry, r.Issues);
                if (def != null)
                {
                    affixes.Add(def);
                }
            }
            return affixes;
        }
    }
}
