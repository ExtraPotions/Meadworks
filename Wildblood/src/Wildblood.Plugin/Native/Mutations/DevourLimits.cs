// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// What a Devouring creature may eat. Three limits, all read from state every machine has, so the machine that runs
    /// a check - the devourer's owner choosing a target, the prey's owner resolving a bite - needs no one else:
    /// <list type="bullet">
    /// <item><b>What it is.</b> Never a player, a boss or a large creature (a troll, a bear, a lox: see
    /// <see cref="BodySize"/>): it never hunts one as prey, and a bite on one is an ordinary hit.</item>
    /// <item><b>Its health.</b> It eats only a creature whose current health is at most `max prey health` percent of its
    /// own current health (100 by default: no more than its own); healthier ones it neither hunts as prey nor devours.
    /// Both healths are read live, from each creature's ZDO, so a wounded devourer's reach shrinks and a hurt creature
    /// comes within it.</item>
    /// <item><b>Its appetite.</b> It eats one creature per star in its life, never fewer than `min meals` (1 by default,
    /// so an unstarred devourer still eats once), counted in its ZDO (<see cref="MealStore"/>). Once it has eaten
    /// them all it is sated: it devours nothing more and the game's own enmity rules it again.</item>
    /// </list>
    /// </summary>
    public static class DevourLimits
    {
        /// <summary>How many creatures it may devour in its life: one per star, never fewer than `min meals`.</summary>
        public static int Allowance(BiomeRules rules, CreatureTraits traits)
        {
            int floor = Mathf.Max(0, Mathf.RoundToInt(rules.PowerOf(Mutation.Devouring, Fields.MinMeals)));
            return Mathf.Max(traits.Stars, floor);
        }

        /// <summary>True once it has eaten its allowance and will devour nothing more.</summary>
        public static bool Sated(EliteController devourer) =>
            MealStore.Count(devourer.View.GetZDO()) >= Allowance(devourer.Rules, devourer.Traits);

        /// <summary>True when the creature can be prey at all: not a player, not a boss, not large, not a Cloning decoy.</summary>
        public static bool IsPrey(Character creature) =>
            !creature.IsPlayer() && !creature.IsBoss() && !BodySize.IsLarge(creature) && !CloneStore.IsDecoy(creature);

        /// <summary>
        /// True when it is prey and within reach: its current health at most `max prey health` percent of the devourer's
        /// own current health (100: no more than its own). `0` lifts the health limit, never the first one.
        /// </summary>
        public static bool FitsInMaw(EliteController devourer, Character prey)
        {
            float percent = devourer.Rules.PowerOf(Mutation.Devouring, Fields.MaxPreyHealth);
            return IsPrey(prey) && (percent <= 0f || prey.GetHealth() <= percent / 100f * devourer.Creature.GetHealth());
        }

        /// <summary>All three limits at once: it may devour this prey right now, its cooldown aside.</summary>
        public static bool MayEat(EliteController devourer, Character prey) =>
            !Sated(devourer) && FitsInMaw(devourer, prey);
    }
}
