// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>
    /// One animal's lineage (design 10.1 and 10.6): its C12 instance token, parent tokens and display names at birth
    /// ("" for wild or unknown), generation, world day of birth and the best keeper at birth.
    /// </summary>
    public sealed class LineageRecord
    {
        public LineageRecord(string token, string motherToken, string sireToken, string motherName, string sireName,
            int generation, int bornDay, string breeder)
        {
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            Token = token ?? ""; MotherToken = motherToken ?? ""; SireToken = sireToken ?? "";
            MotherName = motherName ?? ""; SireName = sireName ?? ""; Generation = generation;
            BornDay = Math.Max(0, bornDay); Breeder = breeder ?? "";
        }

        public string Token { get; }
        public string MotherToken { get; }
        public string SireToken { get; }
        public string MotherName { get; }
        public string SireName { get; }
        public int Generation { get; }
        /// <summary><c>EnvMan.GetDay</c> at birth; 0 for wild or unknown.</summary>
        public int BornDay { get; }
        public string Breeder { get; }

        public LineageRecord WithToken(string token) =>
            new LineageRecord(token, MotherToken, SireToken, MotherName, SireName, Generation, BornDay, Breeder);
    }

    /// <summary>The lineage rules of design 10.6.</summary>
    public static class Lineage
    {
        /// <summary>A tamed-from-wild animal first seen tamed: a token, generation 0, no parents.</summary>
        public static LineageRecord Wild(string token) => new LineageRecord(token, "", "", "", "", 0, 0, "");

        /// <summary>
        /// A newborn: generation max(mother, sire) + 1, where an untracked mother or a missing sire counts as generation
        /// 0. <paramref name="motherName"/> is the tame name or the species token.
        /// </summary>
        public static LineageRecord Newborn(string token, LineageRecord? mother, string motherName, SireRecord? sire, int day, string breeder)
        {
            int generation = Math.Max(mother?.Generation ?? 0, sire?.Generation ?? 0) + 1;
            return new LineageRecord(token, mother?.Token ?? "", sire?.Token ?? "", motherName ?? "", sire?.Name ?? "",
                generation, day, breeder ?? "");
        }

        /// <summary>Growing up keeps the same record and token: the young is destroyed right after the adult is created.</summary>
        public static LineageRecord GrowUp(LineageRecord young)
        {
            if (young == null) throw new ArgumentNullException(nameof(young));
            return young.WithToken(young.Token);
        }

        /// <summary>Hatching moves the egg's record onto the hatchling, which gets its own token.</summary>
        public static LineageRecord Hatch(EggRecord egg, string token)
        {
            if (egg == null) throw new ArgumentNullException(nameof(egg));
            return new LineageRecord(token, egg.MotherToken, egg.SireToken, egg.MotherName, egg.SireName, egg.Generation, egg.BornDay, egg.Breeder);
        }
    }

    /// <summary>
    /// A laid egg's record (design 10.1 egg <c>m_customData["shepherd.egg"]</c>): the chick's lineage and the Wildblood
    /// traits blob ("" without Wildblood). The egg's level travels as its <c>m_quality</c>, not in this record.
    /// </summary>
    public sealed class EggRecord
    {
        public EggRecord(string motherToken, string sireToken, string motherName, string sireName, int generation, int bornDay, string breeder, string traits)
        {
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (bornDay < 0) throw new ArgumentOutOfRangeException(nameof(bornDay));
            MotherToken = motherToken ?? ""; SireToken = sireToken ?? ""; MotherName = motherName ?? ""; SireName = sireName ?? "";
            Generation = generation; BornDay = bornDay; Breeder = breeder ?? ""; Traits = traits ?? "";
        }

        public static EggRecord FromNewborn(LineageRecord newborn, string traits)
        {
            if (newborn == null) throw new ArgumentNullException(nameof(newborn));
            return new EggRecord(newborn.MotherToken, newborn.SireToken, newborn.MotherName, newborn.SireName,
                newborn.Generation, newborn.BornDay, newborn.Breeder, traits);
        }

        public string MotherToken { get; }
        public string SireToken { get; }
        public string MotherName { get; }
        public string SireName { get; }
        public int Generation { get; }
        public int BornDay { get; }
        public string Breeder { get; }
        public string Traits { get; }
    }
}
