// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0, adapted from CreatureLevelManager.cs
// (GetDistanceScalingMultiplier and the spawn policy's allowStatDistanceScaling) at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Changed by expDARE, 2026: Wildblood namespace; the toggle is the Distance Scaling setting; non-finite or negative distances count as the centre.
// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Stars
{
    /// <summary>
    /// Health and damage that rise with distance from the world centre (design A5). A band is the rule file's
    /// <c>distanceScaling: [damageStep, healthStep, stepMeters, maxSteps]</c>: each full step of <c>stepMeters</c> (1000 by default)
    /// adds <c>step</c> to the multiplier, up to <c>maxSteps</c> steps (0 or missing means unlimited).
    /// </summary>
    public static class DistanceScaling
    {
        public const int Damage = 0;
        public const int Health = 1;

        /// <summary>CreatureManager's example band, shipped active in the default rules: [damageStep, healthStep, stepMeters, maxSteps].</summary>
        public static readonly float[] DefaultBand = { 0.03f, 0.08f, 1000f, 5f };

        /// <summary>The band as the default rule text writes it.</summary>
        public const string DefaultBandText = "[0.03, 0.08, 1000, 5]";
        const float DefaultStepMeters = 1000f;

        /// <summary>The multiplier for <paramref name="valueIndex"/> (<see cref="Damage"/> or <see cref="Health"/>) at <paramref name="distance"/> metres.</summary>
        public static float Multiplier(float distance, IReadOnlyList<float>? scaling, int valueIndex)
        {
            if (scaling == null || valueIndex < 0 || scaling.Count <= valueIndex) return 1f;
            var perStep = Math.Max(0f, scaling[valueIndex]);
            if (perStep <= 0f || float.IsNaN(perStep) || float.IsInfinity(perStep)) return 1f;
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0f) return 1f;
            var interval = scaling.Count >= 3 && scaling[2] > 0f ? scaling[2] : DefaultStepMeters;
            var steps = (int)Math.Floor(distance / interval);
            if (scaling.Count >= 4 && scaling[3] > 0f) steps = Math.Min(steps, (int)Math.Floor(scaling[3]));
            return Math.Max(0f, 1f + steps * perStep);
        }

        /// <summary>Distance from the world centre on the ground plane.</summary>
        public static float Horizontal(float x, float z) => (float)Math.Sqrt(x * x + z * z);

        /// <summary>
        /// Whether a creature that came by <paramref name="source"/> gets distance-scaled stats: the toggle is on and the source
        /// keeps its stats otherwise (CreatureManager: summons, births, eggs, grow-up and tamed restores are never scaled).
        /// </summary>
        public static bool Active(bool enabled, SpawnSource source)
        {
            if (!enabled) return false;
            switch (source)
            {
                case SpawnSource.PlayerSummon:
                case SpawnSource.Breeding:
                case SpawnSource.Egg:
                case SpawnSource.Growup:
                case SpawnSource.TamedRestore:
                    return false;
                default:
                    return true;
            }
        }
    }

    /// <summary>
    /// The Tamed Keep Traits setting (design B4). On is EliteCreaturesReborn's behaviour ("taming strips nothing"). Off, by the product
    /// owner's decision of 2026-10-09: while the creature is tamed its mutations and modifiers do not act; they stay stored, so turning
    /// the setting back on (or releasing the tame) restores them, and the stars are kept.
    /// </summary>
    public static class TamedTraits
    {
        /// <summary>True while the traits are withheld: the setting is off and the creature is tamed.</summary>
        public static bool Stripped(bool keepTraits, bool tamed) => !keepTraits && tamed;
    }
}
