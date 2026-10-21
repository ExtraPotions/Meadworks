// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe inscribe &lt;inscription_id&gt; [tier] [value] [cursor|hover|&lt;slot&gt;]</c> (console-commands.md section 3):
    /// adds, or replaces in place, one specific affix on an item in the caller's own inventory, for testing an effect.
    /// The first number is the tier as shown (1 = the strongest; default: the strongest the affix defines), the second
    /// the value (default: a normal roll in that tier's range, scaled like a roll; any number is accepted as a test
    /// value). Appended even past the rarity's maximum and its prefix or suffix limit; exclusion groups, item classes,
    /// item levels and requirements are not enforced, each with a warning line. Never writes an item of a
    /// newer format. Spec gap, decided here: a Common target is made the ladder's first magic rarity, since an item
    /// holding affixes without a rarity is not a state the mod otherwise produces.
    /// </summary>
    internal static class AffixCommand
    {
        public const string Grammar = "inscribe inscribe <inscription_id> [tier] [value] [" + ItemTargets.InventoryWords + "]";

        public static void Run(CommandCall call)
        {
            AffixDef? def = ParseAffix(call);
            if (def == null || !AffixArgs.TryParse(call, def, Grammar, out AffixArgs args))
            {
                return;
            }
            ItemDrop.ItemData? item = ItemTargets.OwnMagicBase(call, args.Target, Grammar);
            ItemState state = ItemState.Read(item);
            if (item == null || ItemTargets.IsNewer(call, state))
            {
                return;
            }
            List<string> warnings = Warnings(def, args, item, state);
            AffixArgs.ScaleDefault(def, ref args, ItemClasses.Classify(item));
            ItemState changed = Apply(state, new AffixRoll(def.Id, args.Tier, args.Value), warnings);
            if (RollerCall.Commit(call, item, changed))
            {
                ItemReport.Write(call, item);
                warnings.ForEach(w => call.Detail("warning: " + w));
            }
        }

        private static AffixDef? ParseAffix(CommandCall call)
        {
            string id = call.Lower(0);
            AffixDef? def = ActiveRules.Current.Affix(id);
            if (def == null)
            {
                string problem = id.Length == 0 ? "which inscription?" : $"unknown inscription '{id}'. Closest: {Closest.To(id, ActiveRules.Current.Affixes.ById.Keys)}";
                call.Fail(problem, Grammar);
            }
            return def;
        }

        private static ItemState Apply(ItemState state, AffixRoll roll, List<string> warnings)
        {
            ItemStateBuilder builder = state.ToBuilder();
            if (!builder.ReplaceAffix(roll.Id, roll))
            {
                builder.AddAffix(roll);
            }
            if (!state.IsMagic)
            {
                IReadOnlyList<RarityDef> ladder = ActiveRules.Current.Economy.Rarities;
                string? first = ladder.Count > 1 ? ladder[1].Id : null;
                builder.SetRarity(first);
                warnings.Add($"the item was Common; it is now {first ?? "(no rarity above the base in the configuration)"}.");
            }
            return builder.Build();
        }

        private static List<string> Warnings(AffixDef def, AffixArgs args, ItemDrop.ItemData item, ItemState state)
        {
            List<string> warnings = new List<string>();
            ClassInfo info = ItemClasses.Classify(item);
            AffixTierDef row = def.TierRow(args.Tier)!;
            if (!def.Enabled) warnings.Add($"{def.Id} is disabled: it stays dormant.");
            if (def.FitFor(info.ClassId) == ClassFit.None) warnings.Add($"{def.Id} does not roll on {info.ClassId ?? "classless"} items.");
            if (row.Level > ItemTier.Of(item)) warnings.Add($"T{row.Shown} unlocks at item level {row.Level}; the item is level {ItemTier.Of(item)}.");
            if (!ItemClasses.Satisfies(info, def.Requires)) warnings.Add($"the item does not meet {def.Id}'s requires block.");
            if (args.OutsideRange) warnings.Add($"value {Numbers.Format(args.Value)} is outside T{row.Shown}'s range (test value).");
            string? clash = ExclusionClash(def, state);
            if (clash != null) warnings.Add($"shares exclusion group '{def.ExclusionGroup}' with {clash} on the item (ignored).");
            return warnings;
        }

        private static string? ExclusionClash(AffixDef def, ItemState state)
        {
            for (int i = 0; def.ExclusionGroup != null && i < state.AffixCount; i++)
            {
                AffixDef? other = state.DefinitionAt(i);
                if (other != null && other.Id != def.Id && other.ExclusionGroup == def.ExclusionGroup)
                {
                    return other.Id;
                }
            }
            return null;
        }
    }

    /// <summary>The optional arguments of <c>inscribe inscribe</c>: tier, value and target word, in any word position.</summary>
    internal readonly struct AffixArgs
    {
        private AffixArgs(int tier, float value, bool outside, string target, bool rolled)
        {
            Tier = tier;
            Value = value;
            OutsideRange = outside;
            Target = target;
            Rolled = rolled;
        }

        /// <summary>The strength grade (1 = the weakest tier).</summary>
        public int Tier { get; }
        public float Value { get; }
        public bool OutsideRange { get; }
        public string Target { get; }

        /// <summary>The value was drawn here, not given: a scaled affix's draw is then scaled like a roll.</summary>
        public bool Rolled { get; }

        /// <summary>A drawn value of a scaled affix times the item class's damage_scale, as <c>ItemRoller</c> stores it.</summary>
        public static void ScaleDefault(AffixDef def, ref AffixArgs args, ClassInfo info)
        {
            if (args.Rolled && def.Scaled)
            {
                int decimals = def.TierRow(args.Tier)?.Decimals ?? 0;
                args = new AffixArgs(args.Tier, RollMath.Scale(args.Value, info.DamageScale, decimals), args.OutsideRange, args.Target, true);
            }
        }

        public static bool TryParse(CommandCall call, AffixDef def, string grammar, out AffixArgs args)
        {
            args = default;
            List<string> numbers = new List<string>();
            string target = "";
            for (int i = 1; i < call.Count; i++)
            {
                string word = call.Lower(i);
                if (ItemTargets.IsWord(word, allowGround: false) && target.Length == 0)
                {
                    target = word;
                }
                else if (numbers.Count < 2 && Numbers.TryFloat(word, out _))
                {
                    numbers.Add(word);
                }
                else
                {
                    call.Fail($"unexpected '{call.Arg(i)}'.", grammar);
                    return false;
                }
            }
            return Build(call, def, numbers, target, grammar, out args);
        }

        private static bool Build(CommandCall call, AffixDef def, List<string> numbers, string target, string grammar, out AffixArgs args)
        {
            args = default;
            int tier = def.StrongestGrade;   // a strength grade: the strongest row unless a tier is given (1 = strongest)
            if (numbers.Count > 0)
            {
                tier = Numbers.TryInt(numbers[0], out int shown) ? def.GradeOf(shown) : 0;
            }
            if (def.TierRow(tier) == null)
            {
                call.Fail($"{def.Id} defines tiers {DefinedTiers(def)}; '{numbers[0]}' is not one of them.", grammar);
                return false;
            }
            AffixTierDef row = def.TierRow(tier)!;
            float value = numbers.Count > 1 ? Round(float.Parse(numbers[1], System.Globalization.CultureInfo.InvariantCulture), 2)
                : RollValue(def, row);
            bool outside = def.Value == AffixValueType.Flag ? value != 1f : value < row.Min || value > row.Max;
            args = new AffixArgs(tier, value, outside, target, numbers.Count < 2);
            return true;
        }

        private static string DefinedTiers(AffixDef def)
        {
            List<string> tiers = new List<string>();
            for (int i = def.Tiers.Count - 1; i >= 0; i--)
            {
                tiers.Add(Numbers.Format(def.Tiers[i].Shown));
            }
            return string.Join(",", tiers);
        }

        // A normal roll: uniform in [min, max], rounded to the tier's decimals; flags store 1 (AffixRoll).
        private static float RollValue(AffixDef def, AffixTierDef row)
        {
            if (def.Value == AffixValueType.Flag)
            {
                return 1f;
            }
            float raw = row.Min + (float)RollRandom.Create().NextDouble() * (row.Max - row.Min);
            return Math.Min(row.Max, Math.Max(row.Min, Round(raw, row.Decimals)));
        }

        private static float Round(float value, int decimals) =>
            (float)Math.Round(value, Math.Max(0, Math.Min(2, decimals)), MidpointRounding.AwayFromZero);
    }
}
