// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from GrindstoneSkills 0.14.1, Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later (ValheimMods monorepo at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Husbandry/Breeding/BirthRolls.cs (the
// twin roll, never for a twin's own birth), GrindstoneSkills/GrindstoneSkills/Husbandry/Breeding/TwinPregnancy.cs (a
// twin's birth is marked and rolls no twins)). Full notice in THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
//
// Changed by expDARE, 2026: namespace Shepherd.Breeding; Unity-free; instead of re-conceiving the parent with a
// back-dated pregnancy, Shepherd keeps a due time (shepherd.twin_due, design 10.4) after a configurable Twin Delay
// (5 to 300 s; GrindstoneSkills used a fixed 30 s).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>Twins (design C9, 10.4).</summary>
    public static class TwinRules
    {
        public const double DefaultDelaySeconds = 30.0;
        public const double MinDelaySeconds = 5.0;
        public const double MaxDelaySeconds = 300.0;

        /// <summary>After a birth: the keeper's Twins At 100 roll. A twin's own birth never rolls (and consumes no roll).</summary>
        public static bool RollTwin(bool birthWasTwin, double twinsAt100, double keeperLevel, IBreedingRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            return !birthWasTwin && HusbandryShare.Rolls(twinsAt100, keeperLevel, random);
        }

        /// <summary>World time of the twin's birth: now plus the Twin Delay clamped to 5 to 300 s.</summary>
        public static double DueAt(double now, double delaySeconds)
        {
            double delay = double.IsNaN(delaySeconds) ? DefaultDelaySeconds : Math.Min(MaxDelaySeconds, Math.Max(MinDelaySeconds, delaySeconds));
            return now + delay;
        }

        /// <summary><c>shepherd.twin_due</c> of 0 means no twin is pending.</summary>
        public static bool IsDue(double twinDue, double now) => twinDue > 0 && now >= twinDue;
    }
}
