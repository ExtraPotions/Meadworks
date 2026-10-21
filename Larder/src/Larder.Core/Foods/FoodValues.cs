// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/ItemValues.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the value writes became pure functions over plain numbers (no item
// shared data, status effect or BepInEx entry): per-item values times the global multipliers, the write-once rule,
// the running-mead progress rescale and Vigor / Eitr Vigor; negative or non-finite multipliers read 0.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Larder.Foods
{
    /// <summary>A food's values (design A2): health, stamina, eitr, duration in seconds and health regen per tick.</summary>
    public readonly struct FoodStats : IEquatable<FoodStats>
    {
        public FoodStats(float health, float stamina, float eitr, float duration, float healthRegen)
        {
            Health = health;
            Stamina = stamina;
            Eitr = eitr;
            Duration = duration;
            HealthRegen = healthRegen;
        }

        public float Health { get; }
        public float Stamina { get; }
        public float Eitr { get; }
        public float Duration { get; }
        public float HealthRegen { get; }

        public bool Equals(FoodStats other) => Health == other.Health && Stamina == other.Stamina && Eitr == other.Eitr
            && Duration == other.Duration && HealthRegen == other.HealthRegen;

        public override bool Equals(object? obj) => obj is FoodStats other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Health, Stamina, Eitr, Duration, HealthRegen);

        public override string ToString() => $"{Health}/{Stamina}/{Eitr} {Duration}s regen {HealthRegen}";
    }

    /// <summary>The global food multipliers (design A4, section 4.2); foods only, never meads.</summary>
    public readonly struct FoodModifiers
    {
        public FoodModifiers(float health, float stamina, float eitr, float duration, float healthRegen)
        {
            Health = health;
            Stamina = stamina;
            Eitr = eitr;
            Duration = duration;
            HealthRegen = healthRegen;
        }

        public static FoodModifiers Vanilla => new FoodModifiers(1f, 1f, 1f, 1f, 1f);

        public float Health { get; }
        public float Stamina { get; }
        public float Eitr { get; }
        public float Duration { get; }
        public float HealthRegen { get; }

        /// <summary>Every multiplier at 1: the item's own values stand.</summary>
        public bool IsVanilla => Health == 1f && Stamina == 1f && Eitr == 1f && Duration == 1f && HealthRegen == 1f;
    }

    /// <summary>Food and mead value rules (design A1 to A7).</summary>
    public static class FoodValues
    {
        /// <summary>The values written into an item: each configured value times its global multiplier.</summary>
        public static FoodStats Apply(FoodStats configured, FoodModifiers modifiers)
        {
            return new FoodStats(
                configured.Health * LarderMath.NonNegative(modifiers.Health),
                configured.Stamina * LarderMath.NonNegative(modifiers.Stamina),
                configured.Eitr * LarderMath.NonNegative(modifiers.Eitr),
                configured.Duration * LarderMath.NonNegative(modifiers.Duration),
                configured.HealthRegen * LarderMath.NonNegative(modifiers.HealthRegen));
        }

        /// <summary>
        /// Whether an item's values are written (A7): once any of its entries or a global multiplier differs from its
        /// default; an item set back to its defaults is still written (with the defaults it was bound with), so it
        /// returns to them. An item never changed keeps whatever the game or another mod gave it.
        /// </summary>
        public static bool ShouldWrite(bool customizedNow, bool writtenBefore) => customizedNow || writtenBefore;

        /// <summary>
        /// A running mead's elapsed time after its duration changes (A5): the same share of the duration stays
        /// remaining, so elapsed is rescaled by new / old duration instead of reset.
        /// </summary>
        public static float KeepProgress(float elapsed, float oldDuration, float newDuration)
        {
            float share = oldDuration > 0f ? elapsed / oldDuration : 0f;
            return share * newDuration;
        }

        /// <summary>
        /// A food's Vigor in percent (C2): its own Vigor plus its configured stamina (times the stamina multiplier)
        /// times Vigor Per Stamina Point, all times Vigor Multiplier. Vigor does not fade with the food.
        /// </summary>
        public static float Vigor(float vigor, float configuredStamina, float staminaModifier, float perStaminaPoint, float multiplier)
        {
            float stamina = configuredStamina * LarderMath.NonNegative(staminaModifier);
            return (vigor + stamina * perStaminaPoint) * multiplier;
        }

        /// <summary>A food's Eitr Vigor in percent (C6), built like <see cref="Vigor"/> from its configured eitr.</summary>
        public static float EitrVigor(float eitrVigor, float configuredEitr, float eitrModifier, float perEitrPoint, float multiplier)
        {
            return Vigor(eitrVigor, configuredEitr, eitrModifier, perEitrPoint, multiplier);
        }
    }
}
