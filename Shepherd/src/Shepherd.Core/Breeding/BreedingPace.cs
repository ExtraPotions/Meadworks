// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from GrindstoneSkills 0.14.1, Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later (ValheimMods monorepo at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Husbandry/Breeding/BreedingPace.cs
// Apply/Restore/SpeedFactor, Husbandry/Breeding/GrowingUp.cs Factor, Husbandry/Breeding/TwinPregnancy.cs Conceive).
// Full notice in THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
//
// Changed by expDARE, 2026: Unity-free value records instead of Procreation fields; the twin starts its pregnancy so it
// is due Twin Delay seconds later (GrindstoneSkills: due at once); growth never slows below the game's time.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>The three Procreation prefab fields Shepherd edits for one Procreate call.</summary>
    public readonly struct PaceFields : IEquatable<PaceFields>
    {
        public PaceFields(double pregnancyDuration, double pregnancyChance, int maxCreatures)
        {
            PregnancyDuration = pregnancyDuration; PregnancyChance = pregnancyChance; MaxCreatures = maxCreatures;
        }

        /// <summary><c>m_pregnancyDuration</c>, seconds.</summary>
        public double PregnancyDuration { get; }
        /// <summary><c>m_pregnancyChance</c>: the chance a check is skipped, so lower breeds faster.</summary>
        public double PregnancyChance { get; }
        /// <summary><c>m_maxCreatures</c>: the crowding limit.</summary>
        public int MaxCreatures { get; }

        public bool Equals(PaceFields other) => PregnancyDuration.Equals(other.PregnancyDuration)
            && PregnancyChance.Equals(other.PregnancyChance) && MaxCreatures == other.MaxCreatures;
        public override bool Equals(object? obj) => obj is PaceFields other && Equals(other);
        public override int GetHashCode() => unchecked((PregnancyDuration.GetHashCode() * 397 ^ PregnancyChance.GetHashCode()) * 397 ^ MaxCreatures);
        public override string ToString() => PregnancyDuration + "/" + PregnancyChance + "/" + MaxCreatures;
    }

    /// <summary>The edit for one call: the fields to set in the prefix and the originals the finalizer restores.</summary>
    public sealed class PacedCall
    {
        public PacedCall(bool paced, PaceFields fields, PaceFields original) { Paced = paced; Fields = fields; Original = original; }

        /// <summary>False: the prefix leaves the fields alone and the finalizer restores nothing.</summary>
        public bool Paced { get; }
        public PaceFields Fields { get; }
        public PaceFields Original { get; }
    }

    /// <summary>
    /// Breeding and growth speed (design 7, C8): temporary field edits made in a prefix and restored in a finalizer, so
    /// prefab values never change permanently and other mods read the originals outside the call.
    /// </summary>
    public static class BreedingPace
    {
        /// <summary>1 + keeper share of Breeding Speed At 100, plus Content Breeding Bonus (percent) while content.</summary>
        public static double SpeedFactor(double breedingSpeedAt100, double keeperLevel, bool content, double contentBonusPercent)
        {
            double factor = 1 + HusbandryShare.Share(breedingSpeedAt100, keeperLevel);
            if (content) factor += Math.Max(0, contentBonusPercent) / 100.0;
            return factor;
        }

        /// <summary>Pregnancy duration and skip chance divided by the factor; herd room added to the crowding limit.</summary>
        public static PacedCall Apply(PaceFields original, double factor, int room)
        {
            bool paced = factor > 1 || room > 0;
            if (!paced) return new PacedCall(false, original, original);
            double divide = Math.Max(1, factor);
            int max = room > 0 && original.MaxCreatures > int.MaxValue - room ? int.MaxValue : original.MaxCreatures + Math.Max(0, room);
            return new PacedCall(true, new PaceFields(original.PregnancyDuration / divide, original.PregnancyChance / divide, max), original);
        }

        /// <summary>1 + keeper share of Growth Speed At 100 (young grow and eggs hatch faster).</summary>
        public static double GrowthFactor(double growthSpeedAt100, double keeperLevel) => 1 + HusbandryShare.Share(growthSpeedAt100, keeperLevel);

        /// <summary>A grow or hatch time divided by the factor; a factor at or below 1 keeps the game's time.</summary>
        public static double ScaledTime(double time, double factor) => factor > 1 ? time / factor : time;

        /// <summary>
        /// Seconds (negative, from now) at which a twin's pregnancy starts so it is due <paramref name="twinDelay"/> seconds
        /// later; a delay longer than the pregnancy waits the full pregnancy.
        /// </summary>
        public static double TwinStartOffset(double pregnancyDuration, double twinDelay) => -Math.Max(0, pregnancyDuration - twinDelay);
    }
}
