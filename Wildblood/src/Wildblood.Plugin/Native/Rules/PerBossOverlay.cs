// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// Reads the `per boss` list inside `aspects:`: each entry matched by boss prefab name, narrowing that boss's
    /// rotation with `aspects` and setting what Summoner calls with `summons`. An entry for a boss the defaults already
    /// know replaces only the keys it names, so listing a boss's rotation keeps its default summons.
    /// </summary>
    internal static class PerBossOverlay
    {
        public static void Apply(AspectRules rules, YamlNode? node, List<string> errors)
        {
            if (node == null)
            {
                return;
            }
            if (!(node is YamlSequenceNode seq))
            {
                YamlRead.AddError(errors, node, $"'{Fields.PerBoss}' should be a list of boss blocks");
                return;
            }
            foreach (YamlNode item in seq.Children)
            {
                ReadBoss(rules, item, errors);
            }
        }

        private static void ReadBoss(AspectRules rules, YamlNode item, List<string> errors)
        {
            if (!(YamlRead.Map(item, errors, "a per-boss entry") is YamlMappingNode block))
            {
                return;
            }
            string? name = YamlRead.Scalar(block, Fields.Match);
            if (string.IsNullOrEmpty(name))
            {
                YamlRead.AddError(errors, block, "a per-boss entry has no 'match' prefab name");
                return;
            }
            if (!rules.Bosses.TryGetValue(name!, out BossAspectRule rule))
            {
                rules.Bosses[name!] = rule = new BossAspectRule();
            }
            ReadRotation(rule, block, errors);
            ReadSummons(rule, block, errors);
        }

        private static void ReadRotation(BossAspectRule rule, YamlMappingNode block, List<string> errors)
        {
            if (!(YamlRead.Child(block, Fields.Aspects) is YamlNode node))
            {
                return;
            }
            List<Aspect> rotation = new List<Aspect>();
            foreach (YamlNode item in Items(node, Fields.Aspects, errors))
            {
                Aspect? aspect = AspectOverlay.Name(item, errors);
                if (aspect != null)
                {
                    rotation.Add(aspect.Value);
                }
            }
            rule.Rotation = rotation;
        }

        private static void ReadSummons(BossAspectRule rule, YamlMappingNode block, List<string> errors)
        {
            if (!(YamlRead.Child(block, Fields.Summons) is YamlNode node))
            {
                return;
            }
            List<string> summons = new List<string>();
            foreach (YamlNode item in Items(node, Fields.Summons, errors))
            {
                string? name = (item as YamlScalarNode)?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    summons.Add(name!);
                }
            }
            rule.Summons = summons;
        }

        private static IEnumerable<YamlNode> Items(YamlNode node, string key, List<string> errors)
        {
            if (node is YamlSequenceNode seq)
            {
                return seq.Children;
            }
            YamlRead.AddError(errors, node, $"'{key}' should be a list like [a, b]");
            return new List<YamlNode>();
        }
    }
}
