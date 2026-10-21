// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>What the pregnant parent's owner knows when a birth is due (design 10.4).</summary>
    public sealed class BirthInput
    {
        public BirthInput(string token, LineageRecord? mother, string motherName, int motherLevel, SireRecord? sire,
            int minOffspringLevel, double keeperLevel, int day, string breeder, bool recordLineage, string traits)
        {
            Token = token ?? ""; Mother = mother; MotherName = motherName ?? ""; MotherLevel = motherLevel; Sire = sire;
            MinOffspringLevel = minOffspringLevel; KeeperLevel = keeperLevel; Day = day; Breeder = breeder ?? "";
            RecordLineage = recordLineage; Traits = traits ?? "";
        }

        /// <summary>The newborn's C12 token.</summary>
        public string Token { get; }
        public LineageRecord? Mother { get; }
        public string MotherName { get; }
        public int MotherLevel { get; }
        /// <summary>The pending sire captured at conception; null for a parent without a partner.</summary>
        public SireRecord? Sire { get; }
        public int MinOffspringLevel { get; }
        public double KeeperLevel { get; }
        public int Day { get; }
        public string Breeder { get; }
        public bool RecordLineage { get; }
        public string Traits { get; }
    }

    /// <summary>The newborn record a birth arms in the claim slot.</summary>
    public sealed class BirthPlan
    {
        public BirthPlan(InheritanceResult inheritance, LineageRecord? lineage, EggRecord egg)
        {
            Inheritance = inheritance; Lineage = lineage; Egg = egg;
        }

        public InheritanceResult Inheritance { get; }
        public int Level => Inheritance.Level;
        /// <summary>Null when Record Lineage is off.</summary>
        public LineageRecord? Lineage { get; }
        public EggRecord Egg { get; }
        /// <summary>Eggs carry the level as quality, so eggs of different levels never stack (C6).</summary>
        public int EggQuality => Inheritance.Level;
    }

    /// <summary>Combines star inheritance (10.3) and lineage (10.6) into one birth.</summary>
    public static class BirthPlanner
    {
        public static BirthPlan Plan(BirthInput input, InheritanceSettings settings, IBreedingRandom random)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var inheritance = StarInheritance.Compute(
                new InheritanceInput(input.MotherLevel, input.Sire?.Level ?? 0, input.MinOffspringLevel, input.KeeperLevel), settings, random);
            LineageRecord? lineage = input.RecordLineage
                ? Breeding.Lineage.Newborn(input.Token, input.Mother, input.MotherName, input.Sire, input.Day, input.Breeder)
                : null;
            EggRecord egg = lineage != null
                ? EggRecord.FromNewborn(lineage, input.Traits)
                : new EggRecord("", "", "", "", 0, 0, "", input.Traits);
            return new BirthPlan(inheritance, lineage, egg);
        }
    }
}
