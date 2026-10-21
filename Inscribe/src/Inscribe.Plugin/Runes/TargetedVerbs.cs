// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Honing Rune (batch 3), which acts on one inscription the player picks; the Tempering Rune folded
// into it on 2026-10-19 (Honing already rolls the value again while it raises the tier).
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Display;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// <c>hone</c> (the Honing Rune): one inscription one tier stronger, never past the best tier the item's level opens on
    /// its class (closed tiers of an only-allowed class stay closed); its value is rolled fresh in the new tier.
    /// Flags have no value and refuse. With more than one candidate the player picks (<see cref="StoneResult.ChooseInscription"/>,
    /// <see cref="InscriptionChooser"/>); the roll happens once, in this dry run, on the owning client like every rune.
    /// </summary>
    internal abstract class TargetedVerb : IStoneVerb
    {
        protected abstract string Done { get; }

        public StoneResult Run(StoneJob job)
        {
            List<int> candidates = Candidates(job);
            if (candidates.Count == 0)
            {
                return StoneResult.Refuse("no_eligible_affix");
            }
            int index = job.AffixIndex;
            if (index < 0)
            {
                return candidates.Count == 1 ? Apply(job, candidates[0]) : StoneResult.ChooseInscription();
            }
            if (!candidates.Contains(index))
            {
                AffixDef? def = index < job.State.AffixCount ? job.State.DefinitionAt(index) : null;
                return StoneResult.Refuse(def != null && def.Value == AffixValueType.Flag ? "affix_is_flag" : "affix_at_top");
            }
            return Apply(job, index);
        }

        /// <summary>The indices of the item's inscriptions this rune can change.</summary>
        public List<int> Candidates(StoneJob job)
        {
            var list = new List<int>();
            for (int i = 0; i < job.State.AffixCount; i++)
            {
                if (Target(job, i) != null)
                {
                    list.Add(i);
                }
            }
            return list;
        }

        // The tier row the inscription would be rolled in, or null when it cannot change.
        private AffixTierDef? Target(StoneJob job, int index)
        {
            AffixDef? def = job.State.DefinitionAt(index);
            if (def == null || !def.Enabled || def.Value == AffixValueType.Flag)
            {
                return null;
            }
            AffixRoll roll = job.State.Affixes[index];
            AffixTierDef? next = def.TierRow(roll.Tier + 1);
            ClassFit fit = def.FitFor(job.Class.ClassId);
            int closed = TierEligibility.ClosedTiers(def, fit == ClassFit.Best ? ClassFit.Best : ClassFit.Allowed,
                job.Rules.Economy.Rolling.AllowedClosedFraction);
            return next != null && ExtraPotions.Inscribe.Runes.TargetedRules.CanHone(true, next.Weight, next.Level, next.Shown, Items.ItemTier.Of(job.Target), closed) ? next : null;
        }

        private StoneResult Apply(StoneJob job, int index)
        {
            AffixDef def = job.State.DefinitionAt(index)!;
            AffixTierDef row = Target(job, index)!;
            AffixRoll old = job.State.Affixes[index];
            float value = RollMath.RollValue(row.Min, row.Max, row.Decimals, RollRandom.Create());
            if (def.Scaled)
            {
                value = RollMath.Scale(value, job.Class.DamageScale, row.Decimals);
            }
            var roll = new AffixRoll(old.Id, row.Grade, value);
            ItemStateBuilder builder = job.State.ToBuilder();
            if (!builder.ReplaceAffix(old.Id, roll))
            {
                return StoneResult.Refuse("no_eligible_affix");
            }
            return StoneResult.Success(builder.Build(), Done, job.ItemName, Line(def, roll));
        }

        /// <summary>"+15% chain lightning  T3": the inscription as the tooltip words it, with its tier.</summary>
        public static string Line(AffixDef def, AffixRoll roll) =>
            AffixLines.Sentence(def.Id, roll.Value, def) + "  " + Words.Localize("$ecf_ui_tier", def.ShownTier(roll.Tier).ToString());
    }

    internal sealed class HoneVerb : TargetedVerb
    {
        protected override string Done => "honed";
    }
}
