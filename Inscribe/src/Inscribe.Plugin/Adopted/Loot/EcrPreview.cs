// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Globalization;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// The Elite Creatures Reborn part of <c>LootPreview</c> for one creature (<c>ecraft ecr</c>, ecr-integration.md
    /// section 11): its ECR keys as stored, then the terms the drop roll would use. The terms come from the roll's own
    /// path (<see cref="DeathFacts"/>, <see cref="LootRoller.InputFor"/>, <see cref="EcrTerms"/>), so the command and the
    /// roll can never disagree. Reads replicated ZDO data and the synced rules only, so it works on any peer. English,
    /// not localized (LOC-3).
    /// </summary>
    public static class EcrPreview
    {
        /// <summary>Two lines: the keys, then the terms (or why the creature drops nothing from us).</summary>
        public static string Explain(Character creature)
        {
            if (!DeathFacts.TryRead(creature, out DeathFacts facts))
            {
                return "hovered: not a creature this mod rolls drops for";
            }
            string name = Utils.GetPrefabName(creature.gameObject);
            EcrFacts keys = EcrFacts.Read(creature.m_nview.GetZDO(), full: true);
            string tier = keys.HasTier ? keys.Tier.ToString(CultureInfo.InvariantCulture) : "-";
            return $"hovered: {name}  ecr_resolved={Bool(keys.Resolved)}  ecr_stars={keys.Stars}  "
                + $"worthless={(keys.Worthless ? "yes" : "no")}  ecr_tier={tier}\n" + Terms(name, facts);
        }

        private static string Terms(string prefab, DeathFacts facts)
        {
            RuleSet rules = ActiveRules.Current;
            DropRules drops = rules.Economy.Drops;
            if (facts.Ecr.Worthless && drops.Ecr.SkipWorthless)
            {
                return "drops nothing from ExtraPotions.Inscribe.Adopted: worthless (drops.ecr.skip_worthless)";
            }
            CreatureProfile profile = CreatureProfiles.Build(prefab, rules.Economy);
            LootInput input = LootRoller.InputFor(profile, facts, rules.Economy, LootModifiers.None);
            string table = input.Ecr.StarsFromEcr ? "drops.ecr" : "drops.star_multipliers, game level";
            return $"stars {input.Stars} -> x{Num(EcrTerms.StarMultiplier(drops, input))} ({table}); "
                + $"tier term x{Num(EcrTerms.StoneFactor(drops, input))}; rarity bonus +{Num(EcrTerms.RarityPercent(drops, input))}%";
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static string Num(float value) => value.ToString("0.0##", CultureInfo.InvariantCulture);
    }
}
