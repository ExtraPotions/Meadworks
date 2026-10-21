// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: meadworks.inscribe.secondary_cooldown, Quickened and Swift Casting on Edge's secondary attacks (batch 3, plan step 7).
using System;

namespace ExtraPotions.Inscribe.Interop
{
    /// <summary>
    /// <c>meadworks.inscribe.secondary_cooldown</c> v1, <c>Func&lt;Character attacker, ItemDrop.ItemData weapon, float&gt;</c>:
    /// the factor Edge multiplies a secondary attack's cooldown by. Quickened (<c>attack_speed</c>) and Swift Casting
    /// (<c>cast_speed</c>) already speed every swing up, Edge's secondaries included (the swing's animation); the same
    /// share now also shortens the cooldown that follows, as a faster swing would. Heavy Hand slows the swing but never
    /// lengthens a cooldown. Damage, hit, leech, reach, projectile and cost inscriptions need no contract: Edge's secondaries
    /// are the game's own attacks and hits, which Inscribe's hooks already reach. Without Edge nothing calls it; without
    /// Inscribe, Edge keeps its own cooldowns.
    /// </summary>
    public static class SecondaryCooldown
    {
        public const string Capability = "meadworks.inscribe.secondary_cooldown";
        public static readonly Version Version = new Version(1, 0);

        /// <summary>A cooldown is never cut below half, however much speed the weapon holds.</summary>
        public const float MinMultiplier = 0.5f;

        /// <summary>The cooldown factor for a weapon's summed attack and cast speed shares (0.1 = 10%).</summary>
        public static float Multiplier(float attackSpeed, float castSpeed)
        {
            float faster = Clean(attackSpeed) + Clean(castSpeed);
            return faster <= 0f ? 1f : Math.Max(MinMultiplier, 1f / (1f + faster));
        }

        private static float Clean(float share) => float.IsNaN(share) || float.IsInfinity(share) ? 0f : Math.Max(0f, share);
    }
}
