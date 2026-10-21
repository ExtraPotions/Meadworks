// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe inscription &lt;id&gt;</c> (classes-and-tiers.md section 9; the spec's <c>inscribe affix</c>, named in the
    /// players' word): one inscription in full - kind, family, effect, the classes it rolls on and how many top tiers an
    /// allowed class keeps closed, then every tier row, strongest first, with its range, weight and the item level that
    /// unlocks it. Read-only; the running rules (the server's on a bound player).
    /// </summary>
    internal static class InscriptionCommand
    {
        public const string Grammar = "inscribe inscription <inscription_id>";

        private static readonly string[] Biomes =
        {
            "Meadows", "Black Forest", "Swamp", "Mountain", "Plains", "Mistlands", "Ashlands", "Deep North",
        };

        public static void Run(CommandCall call)
        {
            string id = call.Lower(0);
            RuleSet rules = ActiveRules.Current;
            AffixDef? def = rules.Affix(id);
            if (def == null)
            {
                call.Fail(id.Length == 0 ? "which inscription?" : $"unknown inscription '{id}'. Closest: {Closest.To(id, rules.Affixes.ById.Keys)}", Grammar);
                return;
            }
            call.Reply(Headline(def));
            call.Detail(ClassLine(def, rules));
            for (int i = def.Tiers.Count - 1; i >= 0; i--)
            {
                call.Detail(RowLine(def, def.Tiers[i]));
            }
        }

        private static string Headline(AffixDef def)
        {
            string effect = def.Param == null ? def.Effect : $"{def.Effect}:{def.Param}";
            string scaled = def.Scaled ? ", scaled by the class's damage_scale" : "";
            return $"{def.Id} \"{Words.Localize(def.Name)}\": {EnumIds<AffixKind>.Id(def.Kind)}, family {def.Family}, "
                + $"{EnumIds<AffixCategory>.Id(def.Category)}, effect {effect} ({EnumIds<AffixValueType>.Id(def.Value)}{scaled}), "
                + $"weight {Numbers.Format(def.Weight)}, {(def.Enabled ? "enabled" : "disabled")}";
        }

        private static string ClassLine(AffixDef def, RuleSet rules)
        {
            int closed = TierEligibility.ClosedTiers(def, ClassFit.Allowed, rules.Economy.Rolling.AllowedClosedFraction);
            string allowed = def.AllowedClasses.Count == 0 ? "" : closed == 0 ? " (every tier open)" : $" (T1-T{closed} closed)";
            return $"best [{string.Join(", ", def.BestClasses)}], allowed [{string.Join(", ", def.AllowedClasses)}]{allowed}";
        }

        private static string RowLine(AffixDef def, AffixTierDef row)
        {
            string range = def.Value == AffixValueType.Flag ? "on"
                : row.Min == row.Max ? Numbers.Format(row.Min) : $"{Numbers.Format(row.Min)}-{Numbers.Format(row.Max)}";
            string biome = row.Level >= 1 && row.Level <= Biomes.Length ? Biomes[row.Level - 1] : "?";
            return $"T{row.Shown} {range}, unlocks at item level {row.Level} ({biome}), weight {Numbers.Format(row.Weight)}";
        }
    }
}
