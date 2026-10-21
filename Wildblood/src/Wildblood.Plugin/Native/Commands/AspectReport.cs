// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Display;
using ExtraPotions.Wildblood.Adopted.Loot;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// The boss-aspect lines of `wildblood inspect`: one line per aspect the boss carries (a Bountiful boss's extras each get
    /// their own) with what it does with the live numbers and what it pays, then the state a player cannot see - a Twin's
    /// or Tethered boss's partner, a Phantom copy's boss, the Summoner waves already called, the Phantom splits already
    /// made, the type an Adaptive boss resists, a Fixated boss's mark. Read from the ZDO on whichever machine the admin is
    /// looking from, so it is the replicated truth, not a local guess.
    /// </summary>
    internal static class AspectReport
    {
        public static List<string> Lines(EliteController controller)
        {
            CreatureTraits traits = controller.Traits;
            ZDO zdo = controller.View.GetZDO();
            List<string> lines = new List<string>();
            if (traits.PhantomCopy)
            {
                lines.Add($"phantom copy of {AspectStore.GetPhantomOf(zdo)}: no drops, no body, vanishes with its boss"
                    + (traits.ExtraAspects != 0 ? "; carries " + string.Join(", ", Carried(traits)) : ""));
                return lines;
            }
            AspectRules rules = RuleState.Active.Boss.Aspects;
            foreach (Aspect aspect in Carried(traits))
            {
                AddAspect(lines, aspect, rules, zdo);
            }
            if (traits.ExtraAspects != 0)
            {
                lines.Add($"loot x{AspectLoot.Factor(controller):0.##} for all its aspects together");
            }
            return lines;
        }

        /// <summary>The aspects to report, headline first; the plain fight reports as `none`.</summary>
        private static IEnumerable<Aspect> Carried(CreatureTraits traits) =>
            traits.Aspect == Aspect.None && traits.ExtraAspects == 0 ? new[] { Aspect.None } : traits.Aspects();

        private static void AddAspect(List<string> lines, Aspect aspect, AspectRules rules, ZDO zdo)
        {
            lines.Add($"aspect {AspectCatalog.Key(aspect)} (loot x{rules.LootOf(aspect):0.##}): "
                + AspectText.Describe(aspect, rules));
            string? state = StateLine(aspect, zdo);
            if (state != null)
            {
                lines.Add(state);
            }
        }

        /// <summary>The replicated state an aspect keeps that a player cannot see; null for an aspect that keeps none.</summary>
        private static string? StateLine(Aspect aspect, ZDO zdo) => aspect switch
        {
            Aspect.Twin => $"twin of {AspectStore.GetTwin(zdo)}; the two share one health pool",
            Aspect.Tethered => $"tethered to {AspectStore.GetTether(zdo)}",
            Aspect.Summoner => $"waves called: {AspectStore.GetWaves(zdo)}",
            Aspect.Phantom => $"splits made: {AspectStore.GetSplits(zdo)}",
            Aspect.Adaptive => $"resisting now: {Aspects.AdaptiveTypes.Describe(Aspects.AdaptiveTypes.Read(zdo))}",
            Aspect.Fixated => Aspects.FixatedMark.Report(zdo),
            _ => null,
        };
    }
}
