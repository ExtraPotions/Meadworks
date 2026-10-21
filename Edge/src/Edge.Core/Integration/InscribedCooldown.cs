// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Inscribe's meadworks.inscribe.secondary_cooldown applied to Edge cooldowns (batch 3, plan step 7).
using System;

namespace ExtraPotions.Edge.Integration
{
    /// <summary>
    /// Inscribe's <c>meadworks.inscribe.secondary_cooldown</c> v1 (<c>Func&lt;Character, ItemDrop.ItemData, float&gt;</c>):
    /// Quickened and Swift Casting on the weapon shorten its secondary cooldown. Edge applies the factor after its own
    /// skill reduction and trusts it only between <see cref="MinFactor"/> and 1, so another provider can never lengthen
    /// or remove a cooldown. Without Inscribe the cooldown is unchanged; No Secondary Cooldowns still bypasses everything.
    /// </summary>
    public static class InscribedCooldown
    {
        public const string Capability = "meadworks.inscribe.secondary_cooldown";
        public static readonly Version Version = new Version(1, 0);
        public const float MinFactor = 0.5f;

        /// <summary>The cooldown after the provider's factor; a missing, broken or out-of-range factor counts as 1 or its bound.</summary>
        public static float Apply(float cooldown, float? factor)
        {
            if (cooldown <= 0f || factor == null || float.IsNaN(factor.Value)) return Math.Max(0f, cooldown);
            return cooldown * Math.Min(1f, Math.Max(MinFactor, factor.Value));
        }
    }
}
