// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// Reads the rule file's `creatures:` list. One entry may carry loot keys and mutation keys side by side, so each
    /// kind goes to its own rule under the entry's prefab name: the loot keys to a <see cref="CreatureLootRule"/>, the
    /// mutation keys to a <see cref="CreatureMutationRule"/>. A second entry with the same `match` (in any case) is
    /// merged into the first rather than replacing it - its named keys override, its drop rows are appended - so
    /// uncommenting an example for a creature that already has an entry never silently drops the one above it.
    /// </summary>
    internal static class CreatureOverlay
    {
        public static void Apply(RuleSet set, YamlNode node, List<string> errors)
        {
            if (!(node is YamlSequenceNode seq))
            {
                YamlRead.AddError(errors, node, "'creatures' should be a list of creature blocks");
                return;
            }
            foreach (YamlNode item in seq.Children)
            {
                ReadCreature(set, item, errors);
            }
        }

        private static void ReadCreature(RuleSet set, YamlNode item, List<string> errors)
        {
            if (!(YamlRead.Map(item, errors, "a creature entry") is YamlMappingNode block))
            {
                return;
            }
            string? name = YamlRead.Scalar(block, "match");
            if (string.IsNullOrEmpty(name))
            {
                YamlRead.AddError(errors, block, "a creature entry has no 'match' prefab name");
                return;
            }
            LootOverlay.ReadRule(LootRuleFor(set, name!), block, errors);
            if (RuleOverlay.NamesMutations(block))
            {
                MutationRuleFor(set, name!).Read(block, errors);
            }
        }

        private static CreatureLootRule LootRuleFor(RuleSet set, string name)
        {
            if (!set.CreatureLoot.TryGetValue(name, out CreatureLootRule rule))
            {
                set.CreatureLoot[name] = rule = new CreatureLootRule();
            }
            return rule;
        }

        private static CreatureMutationRule MutationRuleFor(RuleSet set, string name)
        {
            if (!set.CreatureMutations.TryGetValue(name, out CreatureMutationRule rule))
            {
                set.CreatureMutations[name] = rule = new CreatureMutationRule();
            }
            return rule;
        }
    }
}
