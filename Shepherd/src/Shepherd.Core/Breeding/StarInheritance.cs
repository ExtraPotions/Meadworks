// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from EliteCreaturesReborn 3.20.0 and GrindstoneSkills 0.14.1, Copyright (C) 2026 Gerald Glasgow,
// GPL-3.0-or-later (ValheimMods monorepo at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// EliteCreaturesReborn/EliteCreaturesReborn/Breeding/Inheritance.cs Inheritance.Offspring (star part),
// EliteCreaturesReborn/EliteCreaturesReborn/Breeding/KeeperBonus.cs KeeperBonus.Apply (cap one above the stronger
// parent), GrindstoneSkills/GrindstoneSkills/Husbandry/Breeding/OffspringStar.cs OffspringStar.Begin (Max Offspring
// Level, the minimum offspring level floor)). Full notice in THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
//
// Changed by expDARE, 2026: namespace Shepherd.Breeding; Unity-free; levels instead of stars (level = stars + 1);
// mutation picking removed (Wildblood owns traits); three server modes (Vanilla, BestParentUniform, ParentMean with
// round half up, design 10.3 and user decision Q3); the keeper star and BreedingUpgrades' flat birth star applied once
// each in that order, capped at min(best parent + Max Above Best Parent, Max Offspring Level); no ZDO access.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>Server choice of the base level (design 4.5 <c>Star Inheritance</c>).</summary>
    public enum InheritanceMode
    {
        /// <summary>The game's rule: the mother's level.</summary>
        Vanilla,
        /// <summary>EliteCreaturesReborn: stars uniform from 0 to the stronger parent's stars.</summary>
        BestParentUniform,
        /// <summary>Default (Q3): the parents' mean level, rounded half up.</summary>
        ParentMean,
    }

    /// <summary>The breeding settings inheritance reads (design 4.5), validated to their ranges.</summary>
    public sealed class InheritanceSettings
    {
        public InheritanceSettings(InheritanceMode mode, int maxAboveBestParent, int maxOffspringLevel, double betterOffspringAt100, double birthStarChance)
        {
            if (!Enum.IsDefined(typeof(InheritanceMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (maxAboveBestParent < 0 || maxAboveBestParent > 2) throw new ArgumentOutOfRangeException(nameof(maxAboveBestParent), "0 to 2");
            if (maxOffspringLevel < 1 || maxOffspringLevel > 10) throw new ArgumentOutOfRangeException(nameof(maxOffspringLevel), "1 to 10");
            if (!(betterOffspringAt100 >= 0 && betterOffspringAt100 <= 100)) throw new ArgumentOutOfRangeException(nameof(betterOffspringAt100), "0 to 100");
            if (!(birthStarChance >= 0 && birthStarChance <= 100)) throw new ArgumentOutOfRangeException(nameof(birthStarChance), "0 to 100");
            Mode = mode;
            MaxAboveBestParent = maxAboveBestParent;
            MaxOffspringLevel = maxOffspringLevel;
            BetterOffspringAt100 = betterOffspringAt100;
            BirthStarChance = birthStarChance;
        }

        /// <summary>Design defaults: ParentMean, one above the best parent, level 3 (two stars), 25 percent, 0 percent.</summary>
        public static InheritanceSettings Default { get; } = new InheritanceSettings(InheritanceMode.ParentMean, 1, 3, 25, 0);

        public InheritanceMode Mode { get; }
        public int MaxAboveBestParent { get; }
        public int MaxOffspringLevel { get; }
        public double BetterOffspringAt100 { get; }
        public double BirthStarChance { get; }
    }

    /// <summary>One birth's facts: levels (1 = no star), sire level 0 when there is no sire, the best keeper's level.</summary>
    public readonly struct InheritanceInput
    {
        public InheritanceInput(int motherLevel, int sireLevel, int minOffspringLevel, double keeperLevel)
        {
            if (motherLevel < 1) throw new ArgumentOutOfRangeException(nameof(motherLevel), "levels start at 1");
            if (sireLevel < 0) throw new ArgumentOutOfRangeException(nameof(sireLevel), "0 means no sire");
            MotherLevel = motherLevel;
            SireLevel = sireLevel;
            MinOffspringLevel = Math.Max(0, minOffspringLevel);
            KeeperLevel = keeperLevel;
        }

        public int MotherLevel { get; }
        public int SireLevel { get; }
        public int MinOffspringLevel { get; }
        public double KeeperLevel { get; }
        public bool HasSire => SireLevel > 0;
    }

    public sealed class InheritanceResult
    {
        internal InheritanceResult(int @base, int best, int cap, int level, bool betterOffspring, bool birthStar)
        {
            Base = @base; Best = best; Cap = cap; Level = level; BetterOffspring = betterOffspring; BirthStar = birthStar;
        }

        /// <summary>The mode's base level, never below the minimum offspring level.</summary>
        public int Base { get; }
        /// <summary>max(mother, sire); the mother alone without a sire.</summary>
        public int Best { get; }
        /// <summary>min(Best + Max Above Best Parent, Max Offspring Level), never below the minimum offspring level.</summary>
        public int Cap { get; }
        /// <summary>The newborn's level (an egg's quality).</summary>
        public int Level { get; }
        /// <summary>The keeper's Better Offspring star was added.</summary>
        public bool BetterOffspring { get; }
        /// <summary>The flat Birth Star Chance star was added.</summary>
        public bool BirthStar { get; }
        /// <summary>The callout "Strong offspring!": the newborn is above its best parent.</summary>
        public bool StrongOffspring => Level > Best;
    }

    /// <summary>
    /// Level (star) inheritance, design 10.3 with user decision Q3. Bonuses only ever raise the level up to the cap;
    /// a base already at or above the cap (a parent above Max Offspring Level) is kept, never lowered, as GrindstoneSkills
    /// gives a parent already at Max Offspring Level no star and takes nothing away.
    /// </summary>
    public static class StarInheritance
    {
        public static InheritanceResult Compute(InheritanceInput input, InheritanceSettings settings, IBreedingRandom random)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (random == null) throw new ArgumentNullException(nameof(random));
            int mother = input.MotherLevel;
            int sire = input.HasSire ? input.SireLevel : mother;
            int best = Math.Max(mother, sire);
            int floor = Math.Max(1, input.MinOffspringLevel);
            int cap = Math.Max(floor, Math.Min(best + settings.MaxAboveBestParent, settings.MaxOffspringLevel));
            int level = Math.Max(floor, BaseLevel(settings.Mode, mother, sire, best, random));
            int @base = level;

            bool better = HusbandryShare.Rolls(settings.BetterOffspringAt100, input.KeeperLevel, random) && level < cap;
            if (better) level++;
            bool birthStar = HusbandryShare.RollsFlat(settings.BirthStarChance, random) && level < cap;
            if (birthStar) level++;
            return new InheritanceResult(@base, best, cap, level, better, birthStar);
        }

        private static int BaseLevel(InheritanceMode mode, int mother, int sire, int best, IBreedingRandom random)
        {
            switch (mode)
            {
                case InheritanceMode.Vanilla:
                    return mother;
                case InheritanceMode.BestParentUniform:
                    return 1 + random.Range(0, best); // stars uniform in [0, best stars]
                default:
                    return (mother + sire + 1) / 2; // round half up of the mean
            }
        }
    }
}
