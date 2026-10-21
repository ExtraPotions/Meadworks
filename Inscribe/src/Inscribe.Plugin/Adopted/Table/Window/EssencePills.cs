// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Display;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>One inscription shown as a pill: its name, and the hover box's topic and text.</summary>
    internal readonly struct Pill
    {
        public Pill(string name, string text)
        {
            Name = name;
            Text = text;
        }

        public string Name { get; }
        public string Text { get; }
    }

    /// <summary>
    /// What the Ascension Rune can add to an item with an essence chosen (user request 2026-10-07: "show the possible mods
    /// you can roll when selecting an essence"): exactly the candidates its draw would pick from (<see cref="AffixDraw"/>
    /// with the essence's inscriptions only, on the item as Ascension leaves it: one rarity up), each worded for its hover
    /// box: the line at its best value here, the range and tiers this item level can roll, prefix or suffix. An Epic or
    /// Legendary item, which Ascension does not raise, shows what the essence covers on it at its own rarity, so the pills
    /// always answer "what can this essence give this item".
    /// </summary>
    internal static class EssencePills
    {
        private static readonly List<PoolEntry> Entries = new List<PoolEntry>();
        private static readonly List<AffixTierDef> Tiers = new List<AffixTierDef>();

        public static List<Pill> Of(ItemDrop.ItemData item, Essence essence, StoneDef rune)
        {
            var pills = new List<Pill>();
            ItemState state = ItemState.Read(item);
            RuleSet rules = ActiveRules.Current;
            RarityDef? to = Raised(state, rules.Economy);
            if (to == null)
            {
                return pills;
            }
            RollContext context = RollContext.For(item, rune.TierFloor);
            context.Class = Items.ItemClasses.ClassifyForRune(item);
            context.Favoured = essence.Inscriptions;
            ItemStateBuilder builder = state.ToBuilder();
            builder.SetRarity(to.Id);
            new AffixDraw(builder, context).Candidates(Entries);
            foreach (PoolEntry entry in Entries)
            {
                TierEligibility.Eligible(entry.Def, entry.Fit, context, Tiers);
                pills.Add(Word(entry.Def, context.Class.DamageScale));
            }
            return pills;
        }

        // The next rarity (Ascension's own result); Epic and Legendary stay, since no rune reaches a source-only rarity.
        private static RarityDef? Raised(ItemState state, EconomyRules economy)
        {
            RarityDef? from = state.Rarity ?? economy.BaseRarity;
            RarityDef? next = from != null ? economy.Next(from) : null;
            return next != null && !next.SourceOnly ? next : from;
        }

        private static Pill Word(AffixDef def, float scale)
        {
            string name = DisplayWords.Name(def.Name, def.Id);
            if (Tiers.Count == 0)
            {
                return new Pill(name, "");
            }
            AffixTierDef weakest = Tiers[0];
            AffixTierDef strongest = Tiers[0];
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (AffixTierDef tier in Tiers)
            {
                weakest = tier.Grade < weakest.Grade ? tier : weakest;
                strongest = tier.Grade > strongest.Grade ? tier : strongest;
                min = System.Math.Min(min, Scaled(def, tier.Min, tier, scale));
                max = System.Math.Max(max, Scaled(def, tier.Max, tier, scale));
            }
            return new Pill(name, Describe(def, min, max, def.ShownTier(weakest.Grade), def.ShownTier(strongest.Grade)));
        }

        private static string Describe(AffixDef def, float min, float max, int weakest, int strongest)
        {
            string kind = Words.Localize(def.Kind == AffixKind.Prefix ? "$ecf_table_pill_prefix" : "$ecf_table_pill_suffix");
            string line = AffixLines.Sentence(def.Id, max, def);
            if (def.Value == AffixValueType.Flag)
            {
                return line + "\n" + kind;
            }
            string range = Words.Localize("$ecf_table_pill_rolls", DisplayWords.Plain(min), DisplayWords.Plain(max),
                "T" + weakest, "T" + strongest);
            return line + "\n" + range + "\n" + kind;
        }

        private static float Scaled(AffixDef def, float value, AffixTierDef tier, float scale) =>
            def.Scaled ? RollMath.Scale(value, scale, tier.Decimals) : value;
    }
}
