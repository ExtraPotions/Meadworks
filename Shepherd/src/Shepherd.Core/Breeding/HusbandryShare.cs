// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from GrindstoneSkills 0.14.1, Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later (ValheimMods monorepo at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Husbandry/Core/HusbandrySkill.cs
// HusbandrySkill.Share, Husbandry/Breeding/BreedingPace.cs BreedingPace.ExtraRoom, Husbandry/Breeding/BirthRolls.cs
// BirthRolls.Rolls). Full notice in THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
//
// Changed by expDARE, 2026: namespace Shepherd.Breeding; Unity-free (doubles instead of Mathf, dice through
// IBreedingRandom); the settings values are passed in instead of read from GrindstoneSkills config entries.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>
    /// Husbandry perk scaling (design B1): every perk grows linearly from nothing at level 0 to its configured value at
    /// level 100 of the best keeper within Keeper Range.
    /// </summary>
    public static class HusbandryShare
    {
        public const double MaxLevel = 100.0;

        /// <summary>A percent-at-100 setting as a fraction at <paramref name="level"/>: 25 at level 50 is 0.125.</summary>
        public static double Share(double percentAt100, double level) =>
            Math.Max(0.0, percentAt100) / 100.0 * Clamp01(level / MaxLevel);

        /// <summary>A perk's roll: its share as a chance. A share of 0 never wins and consumes no roll.</summary>
        public static bool Rolls(double percentAt100, double level, IBreedingRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            double chance = Share(percentAt100, level);
            return chance > 0.0 && random.Value() < chance;
        }

        /// <summary>A flat percent chance (no keeper scaling): 0 never wins and consumes no roll.</summary>
        public static bool RollsFlat(double percent, IBreedingRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            double chance = Math.Max(0.0, percent) / 100.0;
            return chance > 0.0 && random.Value() < chance;
        }

        /// <summary>Herd Size At 100 times the keeper's level over 100, rounded half up: 4 gives 1 from level 13.</summary>
        public static int ExtraRoom(double herdSizeAt100, double level) =>
            (int)Math.Floor(Math.Max(0.0, herdSizeAt100) * Clamp01(level / MaxLevel) + 0.5);

        private static double Clamp01(double value) => double.IsNaN(value) ? 0.0 : Math.Min(1.0, Math.Max(0.0, value));
    }
}
