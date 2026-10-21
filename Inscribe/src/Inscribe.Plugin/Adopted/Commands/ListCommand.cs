// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe list inscriptions|runes|rarities [&lt;filter&gt;]</c>: the running configuration, one line per entry
    /// (console-commands.md section 3). The filter matches, in this order: an item class id (inscriptions that roll on
    /// it), a category id, <c>prefix</c> or <c>suffix</c> (inscriptions), a rarity id (runes: <c>applies_to</c>), a verb id
    /// (runes), else an id prefix or an effect. The last column is the last file that touched the entry
    /// (<see cref="RuleOrigins"/>).
    /// </summary>
    internal static class ListCommand
    {
        public const string Grammar = "inscribe list inscriptions|runes|rarities [<filter>]";

        public static void Run(CommandCall call)
        {
            string what = call.Lower(0);
            string filter = call.Lower(1);
            switch (what)
            {
                case "inscriptions": Affixes(call, filter); break;
                case "runes": Stones(call, filter); break;
                case "rarities": Rarities(call, filter); break;
                default: call.Fail(what.Length == 0 ? "list what?" : $"cannot list '{what}'.", Grammar); break;
            }
        }

        private static void Affixes(CommandCall call, string filter)
        {
            IReadOnlyList<AffixDef> all = ActiveRules.Current.Affixes.Affixes;
            List<AffixDef> shown = new List<AffixDef>();
            foreach (AffixDef def in all)
            {
                if (filter.Length == 0 || AffixMatches(def, filter))
                {
                    shown.Add(def);
                }
            }
            call.Reply($"{shown.Count} of {all.Count} inscriptions" + (filter.Length > 0 ? $" matching '{filter}'" : ""));
            RuleOrigins origins = RuleOrigins.For(FamilySpec.Affixes, "inscriptions");
            shown.ForEach(def => call.Detail(AffixLine(def, origins.Of(def.Id))));
        }

        private static bool AffixMatches(AffixDef def, string filter)
        {
            if (ItemClasses.Get(filter) != null)
            {
                return def.FitFor(filter) != ClassFit.None;
            }
            if (EnumIds<AffixCategory>.TryParse(filter, out AffixCategory category))
            {
                return def.Category == category;
            }
            if (EnumIds<AffixKind>.TryParse(filter, out AffixKind kind))
            {
                return def.Kind == kind;
            }
            return def.Id.StartsWith(filter, System.StringComparison.Ordinal) || def.Effect == filter;
        }

        private static string AffixLine(AffixDef def, string origin)
        {
            return $"{def.Id} \"{Words.Localize(def.Name)}\" {EnumIds<AffixValueType>.Id(def.Value)} {EnumIds<AffixKind>.Id(def.Kind)} "
                + $"best [{string.Join(",", def.BestClasses)}] allowed [{string.Join(",", def.AllowedClasses)}] "
                + $"{EnumIds<AffixCategory>.Id(def.Category)} T1-T{def.TierCount} "
                + $"weight {Numbers.Format(def.Weight)} {(def.Enabled ? "enabled" : "disabled")} ({origin})";
        }

        private static void Stones(CommandCall call, string filter)
        {
            IReadOnlyList<StoneDef> all = ActiveRules.Current.Economy.Stones;
            List<StoneDef> shown = new List<StoneDef>();
            foreach (StoneDef stone in all)
            {
                if (filter.Length == 0 || StoneMatches(stone, filter))
                {
                    shown.Add(stone);
                }
            }
            call.Reply($"{shown.Count} of {all.Count} runes" + (filter.Length > 0 ? $" matching '{filter}'" : ""));
            RuleOrigins origins = RuleOrigins.For(FamilySpec.Economy, "runes");
            shown.ForEach(stone => call.Detail(StoneLine(stone, origins.Of(stone.Id))));
        }

        private static bool StoneMatches(StoneDef stone, string filter)
        {
            if (ActiveRules.Current.Economy.Rarity(filter) != null)
            {
                return stone.AppliesToRarity(filter);
            }
            if (EnumIds<StoneVerb>.TryParse(filter, out StoneVerb verb))
            {
                return stone.Verb == verb;
            }
            return stone.Id.StartsWith(filter, System.StringComparison.Ordinal);
        }

        private static string StoneLine(StoneDef stone, string origin)
        {
            string floor = stone.TierFloor > 0 ? $" floor: the best {stone.TierFloor} open tier(s)" : "";
            return $"{stone.Id} \"{Words.Localize(stone.Name)}\" {EnumIds<StoneVerb>.Id(stone.Verb)} "
                + $"on [{string.Join(",", stone.AppliesTo)}]{floor} prefab {stone.Prefab} "
                + $"{(stone.Enabled ? "enabled" : "disabled")} ({origin})";
        }

        private static void Rarities(CommandCall call, string filter)
        {
            IReadOnlyList<RarityDef> all = ActiveRules.Current.Economy.Rarities;
            RuleOrigins origins = RuleOrigins.For(FamilySpec.Economy, "rarities");
            List<RarityDef> shown = new List<RarityDef>();
            foreach (RarityDef rarity in all)
            {
                if (filter.Length == 0 || rarity.Id.StartsWith(filter, System.StringComparison.Ordinal))
                {
                    shown.Add(rarity);
                }
            }
            call.Reply($"{shown.Count} of {all.Count} rarities, lowest first");
            shown.ForEach(rarity => call.Detail(RarityLine(rarity, origins.Of(rarity.Id))));
        }

        private static string RarityLine(RarityDef rarity, string origin)
        {
            return $"{rarity.Id} \"{Words.Localize(rarity.Name)}\" {rarity.Color} inscriptions {rarity.MinAffixes}-{rarity.MaxAffixes} "
                + $"(at most {rarity.MaxPrefixes} prefixes, {rarity.MaxSuffixes} suffixes) "
                + $"glow {(rarity.Glow ? "yes" : "no")} drop_weight {Numbers.Format(rarity.DropWeight)} ({origin})";
        }
    }
}
